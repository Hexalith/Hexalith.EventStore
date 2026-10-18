using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Actual hook/core component tests through synthetic infrastructure; these are not Live or physical-provider evidence.</summary>
public sealed class InteractionPayloadProtectionServiceTests
{
    [Theory]
    [InlineData("ProtectedContent")]
    [InlineData("protectedcontent")]
    [InlineData("pRoTeCtEdCoNtEnT")]
    public async Task AdditionalNestedEnvelopeRejectsBeforeReservationOrLease(string reservedName)
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        string source = Encoding.UTF8.GetString(InteractionPayloadProtectionHookFixture.Payload());
        byte[] payload = Encoding.UTF8.GetBytes(source[..^1] + ",\"Other\":[{\"" + reservedName + "\":{\"Value\":\"must-not-persist\"}}]}");
        await Should.ThrowAsync<InvalidOperationException>(() => Protect(fixture, payload));
        fixture.Occurrences.Registry.ReceivedCalls().ShouldBeEmpty();
        fixture.Occurrences.Keys.Leases.ShouldBe(0);
        fixture.Occurrences.Record.ShouldBeNull();
    }

    [Fact]
    public async Task FieldProtectionRequiresWriterCommitAndExactRetryKeepsOriginalCiphertext()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        PayloadProtectionWriteResult write = await Protect(fixture);
        Encoding.UTF8.GetString(write.ProtectionResult.PayloadBytes).ShouldNotContain("confidential-canary");
        using (JsonDocument json = JsonDocument.Parse(write.ProtectionResult.PayloadBytes))
        {
            json.RootElement.GetProperty("Safe").GetInt32().ShouldBe(1);
            json.RootElement.GetProperty("ProtectedContent").GetProperty("Target").GetProperty("TargetProtectionKeyAlias").GetString().ShouldBe("alias-a");
        }
        (await Read(fixture, write)).IsUnreadable.ShouldBeTrue();
        PayloadProtectionWriteResult retry = await Protect(fixture);
        retry.ProtectionResult.PayloadBytes.ShouldBe(write.ProtectionResult.PayloadBytes);
        fixture.Occurrences.Keys.Leases.ShouldBe(1);
        await Commit(fixture, write);
        (await Read(fixture, write)).PayloadBytes.ShouldBe(InteractionPayloadProtectionHookFixture.Payload());
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.Active);
        fixture.Occurrences.OwnedBuffers.All(static buffer => buffer.All(static item => item == 0)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Safe", "Other")]
    [InlineData("alias-a", "alias-b")]
    public async Task SubstitutedStoredPayloadCannotReadOrDistribute(string original, string replacement)
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        PayloadProtectionWriteResult write = await Protect(fixture);
        await Commit(fixture, write);
        byte[] changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(write.ProtectionResult.PayloadBytes).Replace(original, replacement, StringComparison.Ordinal));
        (await Read(fixture, write, changed)).IsUnreadable.ShouldBeTrue();
        PayloadDistributionResult distribution = await fixture.Service.PrepareEventForDistributionAsync(fixture.Binding.Identity.Owner,
            fixture.Binding.Identity.PayloadTypeId, changed, write.ProtectionResult.SerializationFormat, write.ProtectionResult.Metadata,
            fixture.Occurrence, TestContext.Current.CancellationToken);
        distribution.Outcome.IsUnreadable.ShouldBeTrue();
        distribution.IsSealed.ShouldBeFalse();
    }

    [Fact]
    public async Task ErasedReplayNeedsPositiveTombstoneWhileExternalReadRemainsUnreadableAndDistributionStaysSealed()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        PayloadProtectionWriteResult write = await Protect(fixture);
        await Commit(fixture, write);
        fixture.Occurrences.Keys.RootResolver = _ => Task.FromResult<InteractionOccurrenceOwnedKey?>(null);
        PayloadUnprotectionOutcome read = await Read(fixture, write);
        read.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        read.PayloadBytes.ShouldBeNull();
        PayloadReplayResult unavailable = await Replay(fixture, write);
        unavailable.Outcome.IsUnreadable.ShouldBeTrue();
        unavailable.HasErasedContent.ShouldBeFalse();
        fixture.Erased = true;
        PayloadReplayResult replay = await Replay(fixture, write);
        replay.HasErasedContent.ShouldBeTrue();
        using JsonDocument json = JsonDocument.Parse(replay.Outcome.PayloadBytes!);
        json.RootElement.GetProperty("Safe").GetInt32().ShouldBe(1);
        json.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("State").GetString().ShouldBe(nameof(Erased));
        (await Read(fixture, write)).PayloadBytes.ShouldBeNull();
        PayloadDistributionResult distribution = await fixture.Service.PrepareEventForDistributionAsync(fixture.Binding.Identity.Owner,
            fixture.Binding.Identity.PayloadTypeId, write.ProtectionResult.PayloadBytes, write.ProtectionResult.SerializationFormat,
            write.ProtectionResult.Metadata, fixture.Occurrence, TestContext.Current.CancellationToken);
        distribution.IsSealed.ShouldBeTrue();
        distribution.Outcome.PayloadBytes.ShouldBe(write.ProtectionResult.PayloadBytes);
    }

    [Fact]
    public async Task DirectoryOutboxUsesExplicitInteractionTargetAndCrossTenantTargetCannotReserve()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        fixture.Binding = fixture.Binding with { Identity = fixture.Binding.Identity with { Owner = new AggregateIdentity("tenant-a", "agents", "directory") } };
        PayloadProtectionWriteResult write = await Protect(fixture);
        await Commit(fixture, write);
        (await Read(fixture, write)).PayloadBytes.ShouldBe(InteractionPayloadProtectionHookFixture.Payload());
        fixture.Occurrences.Record!.Request.Identity.Owner.AggregateId.ShouldBe("directory");
        fixture.Occurrences.Record.Request.Identity.Target.AgentInteractionId.ShouldBe("interaction-a");

        var other = new InteractionPayloadProtectionHookFixture();
        byte[] substituted = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(InteractionPayloadProtectionHookFixture.Payload())
            .Replace("tenant-a", "tenant-b", StringComparison.Ordinal));
        await Should.ThrowAsync<InvalidOperationException>(() => Protect(other, substituted));
        other.Occurrences.Record.ShouldBeNull();
        other.Occurrences.Keys.Leases.ShouldBe(0);
    }

    [Fact]
    public async Task UnknownPersistenceRetainsPendingAndChangedCompletionIdentityCannotActivate()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        PayloadProtectionWriteResult write = await Protect(fixture);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Unknown,
            TestContext.Current.CancellationToken);
        fixture.Occurrences.Record!.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Service.CompletePayloadProtectionAsync(
            write.CompletionContext! with { LifecycleEpoch = 2 }, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken));
        fixture.Occurrences.Record.WriterState.ShouldBe(InteractionOccurrenceWriterState.SealedPending);
    }

    [Fact]
    public async Task SnapshotAndCachedCopiesShareTheRootAndBecomeUnreadableWithoutIt()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        fixture.Binding = fixture.Binding with { Identity = fixture.Binding.Identity with {
            Kind = PayloadProtectionPayloadKind.Snapshot, PayloadTypeId = "hx-snapshot-v1:test" } };
        using JsonDocument source = JsonDocument.Parse("{\"Secret\":\"confidential-canary\"}");
        JsonElement state = source.RootElement.Clone();
        SnapshotProtectionWriteResult write = await fixture.Service.ProtectSnapshotAsync(fixture.Binding.Identity.Owner,
            state, Hexalith.EventStore.Server.Events.SnapshotStateJsonContext.Default.JsonElement, fixture.Occurrence, TestContext.Current.CancellationToken);
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
        SnapshotUnprotectionOutcome readable = await fixture.Service.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner,
            write.ProtectionResult.State, write.ProtectionResult.Metadata, Hexalith.EventStore.Server.Events.SnapshotStateJsonContext.Default.JsonElement,
            fixture.Occurrence, TestContext.Current.CancellationToken);
        readable.IsReadable.ShouldBeTrue();
        ((JsonElement)readable.State!).GetProperty("Secret").GetString().ShouldBe("confidential-canary");
        fixture.Occurrences.Keys.RootResolver = _ => Task.FromResult<InteractionOccurrenceOwnedKey?>(null);
        (await fixture.Service.TryUnprotectSnapshotAsync(fixture.Binding.Identity.Owner, write.ProtectionResult.State,
            write.ProtectionResult.Metadata, Hexalith.EventStore.Server.Events.SnapshotStateJsonContext.Default.JsonElement, fixture.Occurrence,
            TestContext.Current.CancellationToken)).IsUnreadable.ShouldBeTrue();
    }

    [Fact]
    public void IdentityReportsCompiledRevisionAndNeverAcceptsAConfiguredTarget()
    {
        var fixture = new InteractionPayloadProtectionHookFixture();
        InteractionProtectionEngineIdentity identity = fixture.Service.GetIdentity();
        identity.Name.ShouldBe("Hexalith.EventStore.Server");
        identity.SourceRevision.Length.ShouldBe(40);
        identity.InformationalVersion.ShouldContain(identity.SourceRevision);
    }

    private static Task<PayloadProtectionWriteResult> Protect(InteractionPayloadProtectionHookFixture fixture, byte[]? payload = null)
        => fixture.Service.ProtectEventPayloadAsync(fixture.Binding.Identity.Owner, Substitute.For<IEventPayload>(),
            fixture.Binding.Identity.PayloadTypeId, payload ?? InteractionPayloadProtectionHookFixture.Payload(), "json", fixture.Occurrence,
            TestContext.Current.CancellationToken);

    private static async Task Commit(InteractionPayloadProtectionHookFixture fixture, PayloadProtectionWriteResult write)
    {
        await fixture.Service.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, TestContext.Current.CancellationToken);
        await fixture.Service.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted,
            TestContext.Current.CancellationToken);
    }

    private static Task<PayloadUnprotectionOutcome> Read(InteractionPayloadProtectionHookFixture fixture, PayloadProtectionWriteResult write, byte[]? payload = null)
        => fixture.Service.TryUnprotectEventPayloadAsync(fixture.Binding.Identity.Owner, fixture.Binding.Identity.PayloadTypeId,
            payload ?? write.ProtectionResult.PayloadBytes, write.ProtectionResult.SerializationFormat, write.ProtectionResult.Metadata,
            fixture.Occurrence, TestContext.Current.CancellationToken);

    private static Task<PayloadReplayResult> Replay(InteractionPayloadProtectionHookFixture fixture, PayloadProtectionWriteResult write)
        => fixture.Service.TryReplayEventPayloadAsync(fixture.Binding.Identity.Owner, fixture.Binding.Identity.PayloadTypeId,
            write.ProtectionResult.PayloadBytes, write.ProtectionResult.SerializationFormat, write.ProtectionResult.Metadata,
            fixture.Occurrence, TestContext.Current.CancellationToken);
}
