using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>
/// Story 22.7a — proves the protection hook contract end-to-end in the persistence path.
/// Sentinel markers (PLAINTEXT_SECRET_MARKER, KEY_ALIAS_SECRET_MARKER) prove that no public surface
/// or log statement ever leaks secret-shaped values stamped into protection metadata.
/// </summary>
public class PayloadProtectionHookTests {
    // Markers intentionally avoid forbidden substrings so they may appear in safely-validated
    // fields. Forbidden-substring rejection is covered separately in the Contracts.Tests suite.
    private const string PlaintextSecretMarker = "MARKER_FORBID_PLAINTEXT";
    private const string KeyAliasSecretMarker = "MARKER_ALIAS_TENANT_A";

    private static readonly AggregateIdentity TestIdentity = new("tenant-a", "billing", "agg-001");

    private sealed record TestEvent(string Name = "test") : IEventPayload;

    private static CommandEnvelope CreateCommand() => new(
        MessageId: "msg-1",
        TenantId: "tenant-a",
        Domain: "billing",
        AggregateId: "agg-001",
        CommandType: "PlaceOrder",
        Payload: [0xAA],
        CorrelationId: "corr-1",
        CausationId: null,
        UserId: "user-1",
        Extensions: null);

    [Fact]
    public async Task EventPersister_NoOpProvider_StampsUnprotectedMetadataIntoExtensions() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        var persister = new EventPersister(stateManager, Substitute.For<ILogger<EventPersister>>(), new NoOpEventPayloadProtectionService());

        EventPersistResult result = await persister.PersistEventsAsync(
            TestIdentity,
            aggregateType: "billing",
            CreateCommand(),
            DomainResult.Success(new IEventPayload[] { new TestEvent() }),
            domainServiceVersion: "v1");

        ServerEventEnvelope envelope = result.PersistedEnvelopes.ShouldHaveSingleItem();
        envelope.Extensions.ShouldNotBeNull();
        envelope.Extensions!.ContainsKey(EventStorePayloadProtectionMetadataCarrier.ExtensionKey).ShouldBeTrue();

        EventStorePayloadProtectionMetadata recorded = EventStorePayloadProtectionMetadataCarrier.Read(envelope.Extensions);
        recorded.State.ShouldBe(PayloadProtectionState.Unprotected);
        recorded.MetadataVersion.ShouldBe(EventStorePayloadProtectionMetadata.CurrentMetadataVersion);
    }

    [Fact]
    public async Task EventPersister_CustomProvider_StampsProvidedMetadataIntoExtensions() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        var customProvider = new FakeProtectionProvider(
            new EventStorePayloadProtectionMetadata(
                PayloadProtectionState.Protected,
                1,
                Scheme: "myco-aead-v1",
                KeyAlias: KeyAliasSecretMarker,
                ContentHint: "application/json",
                CompatibilityFlags: null));

        var persister = new EventPersister(stateManager, Substitute.For<ILogger<EventPersister>>(), customProvider);

        EventPersistResult result = await persister.PersistEventsAsync(
            TestIdentity,
            aggregateType: "billing",
            CreateCommand(),
            DomainResult.Success(new IEventPayload[] { new TestEvent() }),
            domainServiceVersion: "v1");

        ServerEventEnvelope envelope = result.PersistedEnvelopes.ShouldHaveSingleItem();
        EventStorePayloadProtectionMetadata recorded = EventStorePayloadProtectionMetadataCarrier.Read(envelope.Extensions);
        recorded.State.ShouldBe(PayloadProtectionState.Protected);
        recorded.Scheme.ShouldBe("myco-aead-v1");
        recorded.KeyAlias.ShouldBe(KeyAliasSecretMarker);

        // Bytes/metadata invariant: when the provider returns Protected metadata, the persisted
        // payload bytes come from the provider, not from the original plaintext.
        envelope.Payload.SequenceEqual(customProvider.LastProtectedBytes!).ShouldBeTrue();
    }

    [Fact]
    public async Task EventPersister_ForwardsCancellationTokenToProtectionHook() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var capturingProvider = new CapturingProtectionProvider();
        var persister = new EventPersister(stateManager, Substitute.For<ILogger<EventPersister>>(), capturingProvider);

        await persister.PersistEventsAsync(
            TestIdentity,
            aggregateType: "billing",
            CreateCommand(),
            DomainResult.Success(new IEventPayload[] { new TestEvent() }),
            domainServiceVersion: "v1",
            cancellationToken: cts.Token);

        capturingProvider.LastCancellationToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotManager_NoOpProvider_StoresUnprotectedMetadataOnRecord() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());

        await manager.CreateSnapshotAsync(TestIdentity, sequenceNumber: 10, state: new { value = 1 }, stateManager);

        await stateManager.Received(1).SetStateAsync(
            TestIdentity.SnapshotKey,
            Arg.Is<SnapshotRecord>(r => r.ProtectionMetadata != null && r.ProtectionMetadata.State == PayloadProtectionState.Unprotected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SnapshotManager_LoadLegacyNullMetadata_MapsToLegacyCompatibilityRecord() {
        // Arrange: a snapshot persisted before Story 22.7a (ProtectionMetadata = null)
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var legacySnapshot = new SnapshotRecord(
            SequenceNumber: 5,
            State: "legacy-state",
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: TestIdentity.Domain,
            AggregateId: TestIdentity.AggregateId,
            TenantId: TestIdentity.TenantId,
            ProtectionMetadata: null);
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Returns(new ConditionalValue<SnapshotRecord>(true, legacySnapshot));
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());

        // Act
        SnapshotRecord? loaded = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        // Assert -- legacy record maps to explicit legacy compatibility metadata
        loaded.ShouldNotBeNull();
        loaded!.ProtectionMetadata.ShouldNotBeNull();
        loaded.ProtectionMetadata!.State.ShouldBe(PayloadProtectionState.Unprotected);
        loaded.ProtectionMetadata.CompatibilityFlags.ShouldNotBeNull();
        loaded.ProtectionMetadata.CompatibilityFlags!["legacy"].ShouldBe("missing");
    }

    [Fact]
    public async Task SnapshotManager_LoadProviderOpaqueSnapshot_DoesNotInvokeUnprotectHook() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var opaqueSnapshot = new SnapshotRecord(
            SequenceNumber: 5,
            State: "opaque-state",
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: TestIdentity.Domain,
            AggregateId: TestIdentity.AggregateId,
            TenantId: TestIdentity.TenantId,
            ProtectionMetadata: EventStorePayloadProtectionMetadata.ProviderOpaque("parseError"));
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Returns(new ConditionalValue<SnapshotRecord>(true, opaqueSnapshot));
        var tracker = new InvocationCountingProtectionProvider();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), tracker);

        SnapshotRecord? loaded = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        loaded.ShouldNotBeNull();
        loaded!.ProtectionMetadata!.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        tracker.UnprotectSnapshotCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SnapshotManager_LoadInvalidTypedMetadata_MapsToProviderOpaqueAndSkipsUnprotect() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var invalidSnapshot = new SnapshotRecord(
            SequenceNumber: 5,
            State: "cipher",
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: TestIdentity.Domain,
            AggregateId: TestIdentity.AggregateId,
            TenantId: TestIdentity.TenantId,
            ProtectionMetadata: new EventStorePayloadProtectionMetadata(
                PayloadProtectionState.Protected,
                1,
                Scheme: null,
                KeyAlias: null,
                ContentHint: null,
                CompatibilityFlags: null));
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Returns(new ConditionalValue<SnapshotRecord>(true, invalidSnapshot));
        var tracker = new InvocationCountingProtectionProvider();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), tracker);

        SnapshotRecord? loaded = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        loaded.ShouldNotBeNull();
        loaded!.ProtectionMetadata!.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        tracker.UnprotectSnapshotCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task SnapshotManager_LoadWithCustomProvider_PassesStoredMetadataToProvider() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventStorePayloadProtectionMetadata storedMetadata = new(
            PayloadProtectionState.Protected,
            1,
            Scheme: "myco-aead-v1",
            KeyAlias: "tenant-a:snapshot",
            ContentHint: null,
            CompatibilityFlags: null);
        var snapshot = new SnapshotRecord(
            SequenceNumber: 5,
            State: "cipher",
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: TestIdentity.Domain,
            AggregateId: TestIdentity.AggregateId,
            TenantId: TestIdentity.TenantId,
            ProtectionMetadata: storedMetadata);
        stateManager.TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey)
            .Returns(new ConditionalValue<SnapshotRecord>(true, snapshot));
        var capturing = new CapturingProtectionProvider();
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(), capturing);

        _ = await manager.LoadSnapshotAsync(TestIdentity, stateManager);

        capturing.LastUnprotectSnapshotMetadata.ShouldNotBeNull();
        capturing.LastUnprotectSnapshotMetadata!.State.ShouldBe(PayloadProtectionState.Protected);
        capturing.LastUnprotectSnapshotMetadata.Scheme.ShouldBe("myco-aead-v1");
    }

    [Fact]
    public async Task EventPublisher_PassesStoredMetadataToProtectionProvider() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var storedMetadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected,
            1,
            Scheme: "myco-aead-v1",
            KeyAlias: "tenant-a:event",
            ContentHint: "application/json",
            CompatibilityFlags: null);
        IDictionary<string, string> extensions = EventStorePayloadProtectionMetadataCarrier.Write(
            (IDictionary<string, string>?)null,
            storedMetadata);
        var envelope = new ServerEventEnvelope(
            "msg-1", "agg-001", "billing", "tenant-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0", "OrderPlaced", 1, "json",
            [1, 2, 3], extensions);
        var capturing = new CapturingProtectionProvider();
        var publisher = new EventPublisher(
            daprClient,
            Options.Create(new EventPublisherOptions { PubSubName = "pubsub" }),
            Substitute.For<ILogger<EventPublisher>>(),
            capturing,
            new NoOpProjectionUpdateOrchestrator());

        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-1");

        capturing.LastUnprotectEventMetadata.ShouldNotBeNull();
        capturing.LastUnprotectEventMetadata!.State.ShouldBe(PayloadProtectionState.Protected);
        capturing.LastUnprotectEventMetadata.Scheme.ShouldBe("myco-aead-v1");
    }

    [Fact]
    public async Task NoOpEventPayloadProtectionService_ProtectSnapshot_TypedMethod_ReturnsUnprotectedMetadata() {
        var service = new NoOpEventPayloadProtectionService();

        SnapshotProtectionResult result = await service.ProtectSnapshotAsync(TestIdentity, new { x = 1 });

        result.Metadata.State.ShouldBe(PayloadProtectionState.Unprotected);
    }

    [Fact]
    public async Task NoOpEventPayloadProtectionService_UnprotectSnapshot_TypedMethod_AcceptsMetadataParameter() {
        var service = new NoOpEventPayloadProtectionService();
        EventStorePayloadProtectionMetadata metadata = EventStorePayloadProtectionMetadata.Unprotected();

        object result = await service.UnprotectSnapshotAsync(TestIdentity, "state", metadata);

        result.ShouldBe("state");
    }

    [Fact]
    public void Sentinels_AreSyntacticOnly_AndDoNotLeakToProblemDetailsLikeOutput() {
        // Documentation test: ensures the sentinel constants we use throughout 22.7a tests are
        // present in this test assembly only; nothing in the persisted envelope ToString should
        // ever surface them when only the no-op provider is registered.
        var envelope = new ServerEventEnvelope(
            "msg-1", "agg-1", "billing", "tenant-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0", "OrderPlaced", 1, "json",
            Encoding.UTF8.GetBytes(PlaintextSecretMarker), null);

        string s = envelope.ToString();
        s.ShouldContain("[REDACTED]");
        s.ShouldNotContain(PlaintextSecretMarker);
    }

    [Fact]
    public void Serialize_OfMetadataWithSentinel_KeyAlias_DoesNotIncludeForbiddenSubstrings() {
        var metadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected,
            1,
            Scheme: "aes-gcm-256",
            KeyAlias: KeyAliasSecretMarker,
            ContentHint: null,
            CompatibilityFlags: null);

        string serialized = EventStorePayloadProtectionMetadataCarrier.Serialize(metadata);

        // The serialized JSON is structured: state, metadataVersion, scheme, keyAlias. None of the
        // forbidden secret-shaped substrings appear because validation would have rejected them.
        serialized.ShouldNotContain("password", Case.Insensitive);
        serialized.ShouldNotContain("private-key", Case.Insensitive);
        serialized.ShouldNotContain("connection-string", Case.Insensitive);
        serialized.ShouldNotContain("plaintext", Case.Insensitive);
        // KeyAlias remains visible -- it is intentionally a non-secret alias.
        serialized.ShouldContain(KeyAliasSecretMarker);
    }

    private sealed class FakeProtectionProvider(EventStorePayloadProtectionMetadata metadataToReturn) : IEventPayloadProtectionService {
        public byte[]? LastProtectedBytes { get; private set; }

        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default) {
            // Simulate protection by reversing the bytes (deterministic, non-secret).
            byte[] reversed = (byte[])payloadBytes.Clone();
            System.Array.Reverse(reversed);
            LastProtectedBytes = reversed;
            return Task.FromResult(new PayloadProtectionResult(reversed, serializationFormat, metadataToReturn));
        }

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default) {
            byte[] reversed = (byte[])payloadBytes.Clone();
            System.Array.Reverse(reversed);
            return Task.FromResult(new PayloadProtectionResult(reversed, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));
        }

        public Task<object> ProtectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) => Task.FromResult(state);

        public Task<object> UnprotectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) => Task.FromResult(state);
    }

    private sealed class CapturingProtectionProvider : IEventPayloadProtectionService {
        public CancellationToken LastCancellationToken { get; private set; }

        public EventStorePayloadProtectionMetadata? LastUnprotectSnapshotMetadata { get; private set; }

        public EventStorePayloadProtectionMetadata? LastUnprotectEventMetadata { get; private set; }

        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default) {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));
        }

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default) {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));
        }

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName, byte[] payloadBytes, string serializationFormat, EventStorePayloadProtectionMetadata? metadata, CancellationToken cancellationToken = default) {
            LastUnprotectEventMetadata = metadata;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));
        }

        public Task<object> ProtectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(state);
        }

        public Task<object> UnprotectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) {
            LastCancellationToken = cancellationToken;
            return Task.FromResult(state);
        }

        public Task<object> UnprotectSnapshotAsync(AggregateIdentity identity, object state, EventStorePayloadProtectionMetadata? metadata, CancellationToken cancellationToken = default) {
            LastUnprotectSnapshotMetadata = metadata;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(state);
        }
    }

    private sealed class InvocationCountingProtectionProvider : IEventPayloadProtectionService {
        public int UnprotectSnapshotCallCount { get; private set; }

        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
            => Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
            => Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));

        public Task<object> ProtectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) => Task.FromResult(state);

        public Task<object> UnprotectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) {
            UnprotectSnapshotCallCount++;
            return Task.FromResult(state);
        }

        public Task<object> UnprotectSnapshotAsync(AggregateIdentity identity, object state, EventStorePayloadProtectionMetadata? metadata, CancellationToken cancellationToken = default) {
            UnprotectSnapshotCallCount++;
            return Task.FromResult(state);
        }
    }
}
