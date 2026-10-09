using System.Text;
using System.Text.Json;
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

    /// <summary>Actual active event/snapshot decryption cannot release plaintext when final Read authority is withdrawn; pending, active and source evidence remain immutable.</summary>
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActiveReadWithdrawalAfterDecryptReleasesNoPayloadAndPreservesOriginal(bool snapshot)
    {
        var f = new InteractionOccurrenceProtectorFixture(); byte[] source = Payload();
        var identity = snapshot ? f.Identity with { Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = "hx-snapshot-v1:test" } : f.Identity;
        var sealedResult = snapshot ? await f.Adapter.ProtectSnapshotAsync(source, identity, "digest-v1", 1,
            InteractionOccurrenceProtectorFixture.Reference, TestContext.Current.CancellationToken) : await Write(f, source);
        sealedResult.ShouldNotBeNull();
        var pending = f.Record!; pending.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        byte[] pendingBytes = JsonSerializer.SerializeToUtf8Bytes(pending); byte[] cipher = sealedResult.PayloadBytes.ToArray();
        (await f.Adapter.CompleteWriterAsync(identity, sealedResult.KeyReference, "exact-committed-source", true,
            TestContext.Current.CancellationToken)).Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        byte[] activeBytes = JsonSerializer.SerializeToUtf8Bytes(f.Record); int finalChecks = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        f.Keys.Current = () => { Interlocked.Increment(ref finalChecks); entered.TrySetResult(); release.Wait(); return f.Current; };
        var reading = f.Adapter.UnprotectAsync(identity, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            f.Current = false; release.Set();
            var denied = await reading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            denied.IsReadable.ShouldBeFalse(); denied.PayloadBytes.ShouldBeNull(); Volatile.Read(ref finalChecks).ShouldBe(1);
            JsonSerializer.SerializeToUtf8Bytes(pending).ShouldBe(pendingBytes);
            JsonSerializer.SerializeToUtf8Bytes(f.Record).ShouldBe(activeBytes);
            f.Record!.Sealed!.PayloadBytes.ShouldBe(cipher); sealedResult.PayloadBytes.ShouldBe(cipher); source.ShouldBe(Payload());
            f.OwnedBuffers.All(buffer => buffer.All(value => value == 0)).ShouldBeTrue();
            f.Keys.WriteRoots.ShouldBe(1); f.Keys.Leases.ShouldBe(1);
        }
        finally { release.Set(); }
    }
    /// <summary>Only pure local path capture runs after abandonment; no late key, reservation, encryption or writer mutation occurs.</summary>
    [Theory]
    [InlineData("enumerator", false)]
    [InlineData("enumerator", true)]
    [InlineData("move", false)]
    [InlineData("move", true)]
    [InlineData("current", false)]
    [InlineData("current", true)]
    public async Task SuspendedPathTraversalIsBounded(string stage, bool deadlineExpires)
    {
        var f = new InteractionOccurrenceProtectorFixture();
        var (clock, advance) = CaptureClock();
        var adapter = new InteractionOccurrenceProtector(f.Registry, f.Keys, clock);
        using var release = new ManualResetEventSlim(); using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Suspend() { entered.TrySetResult(); release.Wait(); finished.TrySetResult(); }
        var paths = Substitute.For<IReadOnlyCollection<string>>();
        // The normative codec consumes IEnumerable; Count is deliberately not consulted.
        paths.Count.Returns(1);
        var iterator = Substitute.For<IEnumerator<string>>(); int moves = 0;
        paths.GetEnumerator().Returns(_ => { if (stage == "enumerator") { Suspend(); } return iterator; });
        iterator.MoveNext().Returns(_ => { if (++moves > 1) { return false; } if (stage == "move") { Suspend(); } return true; });
        iterator.Current.Returns(_ => { if (stage == "current") { Suspend(); } return "/ProtectedContent"; });
        byte[] source = Payload(); byte[] original = source.ToArray();
        var operation = Task.Run(() => adapter.ProtectEventAsync(source, paths, f.Identity, "digest-v1", 1,
            InteractionOccurrenceProtectorFixture.Reference, caller.Token), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        try
        {
            if (deadlineExpires)
            {
                advance(TimeSpan.FromSeconds(30));
                await Should.ThrowAsync<TimeoutException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            }
            else
            {
                caller.Cancel();
                var error = await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                error.CancellationToken.ShouldBe(caller.Token);
            }
            operation.IsCompleted.ShouldBeTrue();
            release.Set(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            source.ShouldBe(original); f.Record.ShouldBeNull(); f.Registry.ReceivedCalls().ShouldBeEmpty();
            f.OwnedBuffers.ShouldBeEmpty(); f.Keys.Leases.ShouldBe(0); f.Keys.WriteRoots.ShouldBe(0);
            _ = paths.DidNotReceive().Count;
        }
        finally { release.Set(); }
    }

    private static (TimeProvider Clock, Action<TimeSpan> Advance) CaptureClock()
    {
        var clock = Substitute.For<TimeProvider>(); long ticks = 0;
        var observations = new System.Collections.Concurrent.ConcurrentDictionary<int, long>();
        var timers = new System.Collections.Concurrent.ConcurrentDictionary<int, (long Due, Action Fire)>(); int next = 0;
        clock.TimestampFrequency.Returns(TimeSpan.TicksPerSecond);
        clock.GetTimestamp().Returns(_ => { long current = Interlocked.Read(ref ticks); observations[Environment.CurrentManagedThreadId] = current; return current; });
        clock.CreateTimer(Arg.Any<TimerCallback>(), Arg.Any<object?>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>()).Returns(call =>
        {
            int id = Interlocked.Increment(ref next); var timer = Substitute.For<ITimer>();
            timers[id] = (observations.GetValueOrDefault(Environment.CurrentManagedThreadId) + call.ArgAt<TimeSpan>(2).Ticks,
                () => call.Arg<TimerCallback>()(call.ArgAt<object?>(1)));
            timer.When(value => value.Dispose()).Do(callInfo => timers.TryRemove(id, out _));
            if (timers[id].Due <= Interlocked.Read(ref ticks)) { timers[id].Fire(); }
            return timer;
        });
        return (clock, elapsed => { long current = Interlocked.Add(ref ticks, elapsed.Ticks); foreach (var timer in timers.Values) { if (timer.Due <= current) { timer.Fire(); } } });
    }

}
