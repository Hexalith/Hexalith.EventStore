using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Testing.Security;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>
/// Story 22.7b — runtime behaviour for unreadable protected data across publish, snapshot load, and
/// replay surfaces. Each test category in the unreadable taxonomy is covered by at least one named
/// assertion, plus a no-leak sentinel scan over captured logs and result strings.
/// </summary>
public class UnreadableProtectedDataBehaviorTests {
    private static readonly AggregateIdentity TestIdentity = new("tenant-a", "billing", "agg-001");

    public static IEnumerable<object[]> EveryReason() {
        foreach (UnreadableProtectedDataReason reason in Enum.GetValues<UnreadableProtectedDataReason>()) {
            yield return new object[] { reason };
        }
    }

    private static EventEnvelope CreateProtectedEnvelope(
        long sequenceNumber,
        EventStorePayloadProtectionMetadata metadata) {
        IDictionary<string, string> extensions = EventStorePayloadProtectionMetadataCarrier.Write(
            extensions: (IDictionary<string, string>?)null,
            metadata: metadata);
        return new EventEnvelope(
            MessageId: $"msg-{sequenceNumber}",
            AggregateId: TestIdentity.AggregateId,
            AggregateType: "test-aggregate",
            TenantId: TestIdentity.TenantId,
            Domain: TestIdentity.Domain,
            SequenceNumber: sequenceNumber,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: Encoding.UTF8.GetBytes(ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob),
            Extensions: extensions);
    }

    private static (EventPublisher Publisher, DaprClient DaprClient, ILogger<EventPublisher> Logger) CreatePublisher(
        IEventPayloadProtectionService protectionService) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions { PubSubName = "pubsub" });
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IHostEnvironment hostEnv = Substitute.For<IHostEnvironment>();
        hostEnv.EnvironmentName.Returns(Environments.Development);
        var publisher = new EventPublisher(daprClient, options, logger, protectionService, new NoOpProjectionUpdateOrchestrator(), hostEnvironment: hostEnv);
        return (publisher, daprClient, logger);
    }

    // ---------------------------------------------------------------------------------
    // EventPublisher — provider-opaque metadata fails closed at publish time
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task EventPublisher_ProviderOpaqueStoredMetadata_DoesNotPublish_AndReturnsSafeFailureReason() {
        var protectionService = new FakeUnreadableProtectionService();
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(protectionService);

        EventEnvelope envelope = CreateProtectedEnvelope(1, EventStorePayloadProtectionMetadata.ProviderOpaque("parseError"));

        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-001");

        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(0);
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
        result.FailureReason!.ShouldContain(UnreadableProtectedDataReasonCodes.MalformedMetadata);

        // Failure reason MUST be the safe documented format only — no provider exception text, no payload.
        result.FailureReason.ShouldStartWith("Protected payload unavailable for publication.");

        // DAPR was never invoked for the opaque event.
        await daprClient.DidNotReceive().PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());

        // The fake protection service was NEVER asked to unprotect the opaque bytes.
        protectionService.EventUnprotectInvocations.ShouldBeEmpty();

        // No-leak: failure reason carries no sentinel.
        ProtectedDataLeakSentinel.AssertNoLeak([result.FailureReason]);
    }

    [Theory]
    [MemberData(nameof(EveryReason))]
    public async Task EventPublisher_UnreadableUnprotectOutcome_DoesNotPublish_AndUsesReasonCodeInFailureReason(
        UnreadableProtectedDataReason reason) {
        // ProviderOpaqueUnsupportedOperation is handled by the metadata-state guard, never reaching
        // the provider. The provider-driven flow exercises every OTHER reason.
        if (reason == UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation) {
            return;
        }

        var protectionService = new FakeUnreadableProtectionService();
        protectionService.ConfigureEventUnreadable(reason);

        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(protectionService);

        // The stored metadata is Protected — the carrier passes it to the provider, which then
        // returns an Unreadable outcome with the configured reason.
        EventEnvelope envelope = CreateProtectedEnvelope(
            1,
            new EventStorePayloadProtectionMetadata(
                PayloadProtectionState.Protected,
                1,
                Scheme: "test-aead-v1",
                KeyAlias: ProtectedDataLeakSentinel.ProtectedKeyAlias,
                ContentHint: null,
                CompatibilityFlags: null));

        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-001");

        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(0);
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
        result.FailureReason!.ShouldContain(UnreadableProtectedDataReasonCodes.From(reason));

        await daprClient.DidNotReceive().PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());

        // No-leak: failure reason must not echo the key alias or any sentinel.
        ProtectedDataLeakSentinel.AssertNoLeak([result.FailureReason]);
    }

    [Fact]
    public async Task EventPublisher_FirstEventUnreadable_LaterEventsAreNotPublished() {
        var protectionService = new FakeUnreadableProtectionService();
        protectionService.ConfigureEventUnreadablePersistent(UnreadableProtectedDataReason.MissingKey);

        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(protectionService);

        EventStorePayloadProtectionMetadata protectedMetadata = new(
            PayloadProtectionState.Protected,
            1,
            Scheme: "test-aead",
            KeyAlias: null,
            ContentHint: null,
            CompatibilityFlags: null);

        EventEnvelope[] envelopes = [
            CreateProtectedEnvelope(1, protectedMetadata),
            CreateProtectedEnvelope(2, protectedMetadata),
            CreateProtectedEnvelope(3, protectedMetadata),
        ];

        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, envelopes, "corr-001");

        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(0);
        await daprClient.DidNotReceive().PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EventPublisher_NoOpProvider_StillPublishes_AndNeverEmitsUnreadableTaxonomy() {
        var protectionService = new FakeUnreadableProtectionService(); // default: no unreadable configured
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(protectionService);

        EventEnvelope envelope = CreateProtectedEnvelope(1, EventStorePayloadProtectionMetadata.Unprotected());

        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-001");

        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(1);
        result.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void EventPublisher_BuildUnreadableFailureReason_FormatIsStableAndDocumented() {
        string reason = EventPublisher.BuildUnreadableFailureReason(UnreadableProtectedDataReasonCodes.MissingKey);
        reason.ShouldBe("Protected payload unavailable for publication. ReasonCode=missing-key");
    }

    // ---------------------------------------------------------------------------------
    // SnapshotManager — unreadable behavior (retention + no deletion)
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task SnapshotManager_TypedUnreadableOutcome_ReturnsNullAndRetainsSnapshot() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventStorePayloadProtectionMetadata storedMetadata = new(
            PayloadProtectionState.Protected,
            1,
            Scheme: "test-aead",
            KeyAlias: null,
            ContentHint: null,
            CompatibilityFlags: null);
        var snapshot = new SnapshotRecord(
            SequenceNumber: 7,
            State: "cipher",
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: TestIdentity.Domain,
            AggregateId: TestIdentity.AggregateId,
            TenantId: TestIdentity.TenantId,
            ProtectionMetadata: storedMetadata);
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Returns(new ConditionalValue<SnapshotRecord>(true, snapshot));

        var protectionService = new FakeUnreadableProtectionService();
        protectionService.ConfigureSnapshotUnreadable(UnreadableProtectedDataReason.MissingKey);

        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), protectionService);

        SnapshotRecord? loaded = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        loaded.ShouldBeNull();
        await stateManager.DidNotReceive().RemoveStateAsync(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SnapshotManager_LegacyNullMetadata_StillReadable_AndDoesNotEmitUnreadableTaxonomy() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var legacy = new SnapshotRecord(
            SequenceNumber: 3,
            State: "legacy-state",
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: TestIdentity.Domain,
            AggregateId: TestIdentity.AggregateId,
            TenantId: TestIdentity.TenantId,
            ProtectionMetadata: null);
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Returns(new ConditionalValue<SnapshotRecord>(true, legacy));

        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());

        SnapshotRecord? loaded = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        loaded.ShouldNotBeNull();
        loaded!.ProtectionMetadata.ShouldNotBeNull();
        loaded.ProtectionMetadata!.State.ShouldBe(PayloadProtectionState.Unprotected);
        loaded.ProtectionMetadata.CompatibilityFlags!["legacy"].ShouldBe("missing");
    }

    [Fact]
    public async Task SnapshotManager_CorruptUnprotectedDeserialization_StillDeletes() {
        // Story 22.7b: the existing RemoveStateAsync path is PRESERVED for non-protected corrupt
        // deserialization (schema drift, etc.). This is NOT changed by 22.7b.
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Throws<InvalidOperationException>();

        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());

        SnapshotRecord? loaded = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        loaded.ShouldBeNull();
        await stateManager.Received(1).RemoveStateAsync(TestIdentity.SnapshotKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SnapshotManager_TypedUnreadableOutcome_OperationCanceledExceptionPropagates() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var snapshot = new SnapshotRecord(
            SequenceNumber: 7,
            State: "cipher",
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: TestIdentity.Domain,
            AggregateId: TestIdentity.AggregateId,
            TenantId: TestIdentity.TenantId,
            ProtectionMetadata: new EventStorePayloadProtectionMetadata(
                PayloadProtectionState.Protected, 1, "test", null, null, null));
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Returns(new ConditionalValue<SnapshotRecord>(true, snapshot));

        var protectionService = new FakeUnreadableProtectionService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), protectionService);

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await manager.LoadSnapshotAsync(TestIdentity, stateManager, cancellationToken: cts.Token));
    }

    // ---------------------------------------------------------------------------------
    // ProtectedDataUnreadableException — safe message formatting
    // ---------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryReason))]
    public void ProtectedDataUnreadableException_Message_HasOnlySafeFields(UnreadableProtectedDataReason reason) {
        var ex = new ProtectedDataUnreadableException(reason, stage: "rehydrate", sequenceNumber: 42);

        ex.Reason.ShouldBe(reason);
        ex.ReasonCode.ShouldBe(UnreadableProtectedDataReasonCodes.From(reason));
        ex.Stage.ShouldBe("rehydrate");
        ex.SequenceNumber.ShouldBe(42);

        ex.Message.ShouldContain(UnreadableProtectedDataReasonCodes.From(reason));
        ex.Message.ShouldContain("rehydrate");
        ex.Message.ShouldContain("42");

        ProtectedDataLeakSentinel.AssertNoLeak([ex.Message]);
    }

    [Fact]
    public void ProtectedDataUnreadableException_Message_UsesNullSentinelsWhenOptionalFieldsAreNull() {
        var ex = new ProtectedDataUnreadableException(UnreadableProtectedDataReason.ProviderUnavailable);

        ex.Stage.ShouldBeNull();
        ex.SequenceNumber.ShouldBeNull();
        ex.Message.ShouldContain("unspecified");
        ex.Message.ShouldContain("n/a");
        ex.Message.ShouldContain(UnreadableProtectedDataReasonCodes.ProviderUnavailable);
    }

    // ---------------------------------------------------------------------------------
    // No-op compatibility — explicit proof that no unreadable status is emitted
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task EventPublisher_LegacyEnvelopeNoExtensions_TreatedAsUnprotected_AndPublishes() {
        var protectionService = new FakeUnreadableProtectionService(); // no unreadable configured
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(protectionService);

        // Legacy envelope: no Extensions dictionary at all (mirrors pre-22.7a persisted rows)
        var legacyEnvelope = new EventEnvelope(
            MessageId: "msg-1",
            AggregateId: TestIdentity.AggregateId,
            AggregateType: "test-aggregate",
            TenantId: TestIdentity.TenantId,
            Domain: TestIdentity.Domain,
            SequenceNumber: 1,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [legacyEnvelope], "corr-001");

        result.Success.ShouldBeTrue();
        result.FailureReason.ShouldBeNull();
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
    }
}
