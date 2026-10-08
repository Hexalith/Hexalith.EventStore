using System.Text;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Actual candidate registry/HKDF/v2 composition through synthetic custody and lease seams; no live backend/host qualification.</summary>
public sealed class InteractionOccurrenceProtectorTests
{
    private static byte[] Payload() => "{\"ProtectedContent\":\"private-synthetic\",\"Safe\":1}"u8.ToArray();
    private static Task<InteractionOccurrenceSealedResult?> Write(InteractionOccurrenceProtectorFixture fixture, byte[]? payload = null, string reference = InteractionOccurrenceProtectorFixture.Reference)
        => fixture.Adapter.ProtectEventAsync(payload ?? Payload(), new[] { "/ProtectedContent" }, fixture.Identity, "digest-v1", 1, reference, TestContext.Current.CancellationToken);

    /// <summary>Exact retry returns original retained cipher without another root/encryption invocation; caller mutation cannot alter retained bytes.</summary>
    [Fact]
    public async Task OriginalSealedRetryNeverEncryptsAgainAndZerosActualOwnedKeys()
    {
        var f = new InteractionOccurrenceProtectorFixture(); byte[] original = Payload(); var first = (await Write(f, original))!;
        Encoding.UTF8.GetString(first.PayloadBytes).ShouldNotContain("private-synthetic"); original.ShouldBe(Payload());
        byte[] retained = first.PayloadBytes.ToArray(); first.PayloadBytes[0] = 0;
        var retry = (await Write(f, reference: "01ARZ3NDEKTSV4RRFFQ69G5FAW"))!; retry.KeyReference.ShouldBe(InteractionOccurrenceProtectorFixture.Reference); retry.PayloadBytes.ShouldBe(retained);
        f.Keys.WriteRoots.ShouldBe(1);
        f.Keys.Leases.ShouldBe(1);
        f.OwnedBuffers.All(buffer => buffer.All(value => value == 0)).ShouldBeTrue(); f.Record!.Request.ContentIntentHmac.Length.ShouldBe(64);
    }
    /// <summary>Different plaintext/path/digest version cannot reuse the original reservation or release its ciphertext.</summary>
    [Theory]
    [InlineData("content")]
    [InlineData("paths")]
    [InlineData("digest")]
    public async Task ChangedPlaintextIntentConflictsWithoutSecondEncryption(string vector)
    {
        var f = new InteractionOccurrenceProtectorFixture(); (await Write(f)).ShouldNotBeNull();
        var result = await f.Adapter.ProtectEventAsync(vector == "content" ? "{\"ProtectedContent\":\"changed\",\"Safe\":1}"u8.ToArray() : Payload(),
            vector == "paths" ? new[] { "/Safe" } : new[] { "/ProtectedContent" }, f.Identity, vector == "digest" ? "digest-v2" : "digest-v1", 1,
            InteractionOccurrenceProtectorFixture.Reference, TestContext.Current.CancellationToken);
        result.ShouldBeNull(); f.Keys.Leases.ShouldBe(1);
    }
    /// <summary>Unknown ciphertext retention after an invocation never grants a second invocation with the same derived key/reference.</summary>
    [Fact]
    public async Task UnknownSealCannotReinvokeEncryptionWithSameOccurrenceKey()
    {
        var f = new InteractionOccurrenceProtectorFixture();
        f.Registry.RetainSealedAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>())
            .Returns(new InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus.Unavailable, null));
        (await Write(f)).ShouldBeNull(); (await Write(f)).ShouldBeNull();
        f.Keys.WriteRoots.ShouldBe(1);
        f.OwnedBuffers.All(buffer => buffer.All(value => value == 0)).ShouldBeTrue();
    }
    /// <summary>Retention and independent source completion differ; pending ciphertext does not release plaintext or activate caches.</summary>
    [Fact]
    public async Task OnlyExactCommittedWriterProofAllowsReplay()
    {
        var f = new InteractionOccurrenceProtectorFixture(); (await Write(f)).ShouldNotBeNull();
        (await f.Adapter.UnprotectAsync(f.Identity, TestContext.Current.CancellationToken)).IsReadable.ShouldBeFalse();
        (await f.Adapter.CompleteWriterAsync(f.Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAW", "commit-proof", true, TestContext.Current.CancellationToken)).Status.ShouldBe(InteractionOccurrenceReservationStatus.Conflict);
        (await f.Adapter.CompleteWriterAsync(f.Identity, InteractionOccurrenceProtectorFixture.Reference, "commit-proof", true, TestContext.Current.CancellationToken)).Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        var read = await f.Adapter.UnprotectAsync(f.Identity, TestContext.Current.CancellationToken); read.PayloadBytes.ShouldBe(Payload());
        (await f.Adapter.UnprotectAsync(f.Identity with { Target = f.Identity.Target with { TargetProtectionKeyAlias = "wrong" } }, TestContext.Current.CancellationToken)).IsReadable.ShouldBeFalse();
        f.OwnedBuffers.All(buffer => buffer.All(value => value == 0)).ShouldBeTrue();
    }
    /// <summary>Whole snapshot uses a distinct occurrence subkey and actual unchanged v2 snapshot codec, preserving plaintext only after source completion.</summary>
    [Fact]
    public async Task SnapshotUsesActualCoreAndExactWriterCompletion()
    {
        var f = new InteractionOccurrenceProtectorFixture(); var identity = f.Identity with { Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = "hx-snapshot-v1:test" };
        var sealedResult = await f.Adapter.ProtectSnapshotAsync(Payload(), identity, "digest-v1", 1, InteractionOccurrenceProtectorFixture.Reference, TestContext.Current.CancellationToken);
        sealedResult.ShouldNotBeNull(); Encoding.UTF8.GetString(sealedResult.PayloadBytes).ShouldNotContain("private-synthetic");
        await f.Adapter.CompleteWriterAsync(identity, sealedResult.KeyReference, "snapshot-commit", true, TestContext.Current.CancellationToken);
        (await f.Adapter.UnprotectAsync(identity, TestContext.Current.CancellationToken)).PayloadBytes.ShouldBe(Payload());
    }
    /// <summary>Authority withdrawal after durable seal retains immutable evidence while withholding release.</summary>
    [Fact]
    public async Task CurrentAuthorityWithdrawalWithholdsSealedResult()
    {
        var f = new InteractionOccurrenceProtectorFixture(); f.Current = false;
        (await Write(f)).ShouldBeNull(); f.Record!.Sealed.ShouldNotBeNull(); f.OwnedBuffers.All(buffer => buffer.All(value => value == 0)).ShouldBeTrue();
    }
    /// <summary>Caller completes promptly while a private root resolver is stalled; its actual late owned key is zeroed without late registry mutation.</summary>
    [Fact]
    public async Task CancelledStalledRootClearsLateOwnedBufferAndPreservesOriginalToken()
    {
        var f = new InteractionOccurrenceProtectorFixture(); using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pending = new TaskCompletionSource<InteractionOccurrenceOwnedKey?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Keys.RootResolver = _ => { entered.SetResult(); return pending.Task; };
        var operation = f.Adapter.ProtectEventAsync(Payload(), new[] { "/ProtectedContent" }, f.Identity, "digest-v1", 1, InteractionOccurrenceProtectorFixture.Reference, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cancellation.Cancel();
        var exception = await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)); exception.CancellationToken.ShouldBe(cancellation.Token);
        var owned = f.Owned(f.Identity, "interaction-root", "root-v1", 17); pending.SetResult(owned);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5)); while (owned.Bytes.Any(value => value != 0)) { await Task.Delay(10, watchdog.Token); }
        f.Record!.Sealed.ShouldBeNull(); await f.Registry.DidNotReceive().RetainSealedAsync(Arg.Any<InteractionOccurrenceIdentity>(), Arg.Any<InteractionOccurrenceSealedResult>(), Arg.Any<CancellationToken>());
    }
}
