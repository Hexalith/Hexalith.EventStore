using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>
/// Verifies the Story 8.2 contracts, compatibility defaults, and owner goldens under normative
/// digest <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public class PayloadProtectionV2ContractTests {
    private const string ApprovedDigest = "de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e";
    private const string EventTypeName = "Hexalith.Parties.Contracts.Events.PartyCreated";
    private const string GoldenFileHash = "a821f36321bd5a020b35f89b1e3d18e7dc3070e3dbf694db5c0413847a012610";
    private const string NistFileHash = "528a81472dc6bd4db653bf01dc52a16d5e092c7bcde2ef622f42878ba794a319";
    private const string OwnershipFileHash = "a3886eac22cf3b77c210ae2bb166f237980bc5e8cf1e9cbaf8c569aa6b4dc087";
    private const string V2Format = "json+pdenc-v2";
    private const string V2Scheme = "hexalith-pdenc-v2";

    private static readonly string _fixtureDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "PayloadProtectionV2");

    [Fact]
    public void PublicApi_ContainsExactAdditiveDefaultMembers() {
        Type serviceType = typeof(IEventPayloadProtectionService);
        MethodInfo[] additions =
        [
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.ProtectEventPayloadAsync),
                typeof(AggregateIdentity), typeof(IEventPayload), typeof(string), typeof(byte[]), typeof(string),
                typeof(PayloadProtectionOccurrenceContext), typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.ProtectSnapshotAsync),
                typeof(AggregateIdentity), typeof(object), typeof(JsonTypeInfo),
                typeof(PayloadProtectionOccurrenceContext), typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.TryUnprotectEventPayloadAsync),
                typeof(AggregateIdentity), typeof(string), typeof(byte[]), typeof(string),
                typeof(EventStorePayloadProtectionMetadata), typeof(PayloadProtectionOccurrenceContext),
                typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.TryUnprotectSnapshotAsync),
                typeof(AggregateIdentity), typeof(object), typeof(EventStorePayloadProtectionMetadata),
                typeof(JsonTypeInfo), typeof(PayloadProtectionOccurrenceContext), typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.AcquirePayloadProtectionCompletionLeaseAsync),
                typeof(PayloadProtectionCompletionContext), typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.CompletePayloadProtectionAsync),
                typeof(PayloadProtectionCompletionContext), typeof(PayloadProtectionPersistenceOutcome),
                typeof(CancellationToken)),
        ];

        MethodInfo[] discoveredAdditions = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(static method => method.Name is nameof(IEventPayloadProtectionService.AcquirePayloadProtectionCompletionLeaseAsync)
                or nameof(IEventPayloadProtectionService.CompletePayloadProtectionAsync)
                || method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(PayloadProtectionOccurrenceContext)))
            .ToArray();

        discoveredAdditions.Length.ShouldBe(6);
        discoveredAdditions.ShouldAllBe(method => additions.Contains(method));
        additions.ShouldAllBe(static method => !method.IsAbstract && method.IsVirtual);
        additions.Select(static method => method.ReturnType).ShouldBe([
            typeof(Task<PayloadProtectionWriteResult>),
            typeof(Task<SnapshotProtectionWriteResult>),
            typeof(Task<PayloadUnprotectionOutcome>),
            typeof(Task<SnapshotUnprotectionOutcome>),
            typeof(Task),
            typeof(Task),
        ]);
        additions.Select(static method => method.GetParameters().Select(static parameter => parameter.Name).ToArray()).ShouldBe([
            ["identity", "eventPayload", "eventTypeName", "payloadBytes", "serializationFormat", "occurrenceContext", "cancellationToken"],
            ["identity", "state", "stateTypeInfo", "occurrenceContext", "cancellationToken"],
            ["identity", "eventTypeName", "payloadBytes", "serializationFormat", "metadata", "occurrenceContext", "cancellationToken"],
            ["identity", "state", "metadata", "stateTypeInfo", "occurrenceContext", "cancellationToken"],
            ["completionContext", "cancellationToken"],
            ["completionContext", "persistenceOutcome", "cancellationToken"],
        ]);
        foreach (MethodInfo method in additions) {
            ParameterInfo[] parameters = method.GetParameters();
            parameters[^1].IsOptional.ShouldBeTrue();
            parameters[^1].HasDefaultValue.ShouldBeTrue();
            parameters.Take(parameters.Length - 1).ShouldAllBe(static parameter => !parameter.IsOptional);
        }

        var nullability = new NullabilityInfoContext();
        nullability.Create(additions[2].GetParameters()[4]).ReadState.ShouldBe(NullabilityState.Nullable);
        nullability.Create(additions[3].GetParameters()[2]).ReadState.ShouldBe(NullabilityState.Nullable);
    }

    [Fact]
    public void PublicApi_PreservesLegacyRequiredMembers() {
        Type serviceType = typeof(IEventPayloadProtectionService);
        MethodInfo[] legacyRequired =
        [
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.ProtectEventPayloadAsync),
                typeof(AggregateIdentity), typeof(IEventPayload), typeof(string), typeof(byte[]), typeof(string),
                typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.UnprotectEventPayloadAsync),
                typeof(AggregateIdentity), typeof(string), typeof(byte[]), typeof(string), typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.ProtectSnapshotStateAsync),
                typeof(AggregateIdentity), typeof(object), typeof(CancellationToken)),
            RequireMethod(serviceType, nameof(IEventPayloadProtectionService.UnprotectSnapshotStateAsync),
                typeof(AggregateIdentity), typeof(object), typeof(CancellationToken)),
        ];

        legacyRequired.ShouldAllBe(static method => method.IsAbstract);
    }

    [Fact]
    public void PolicyContracts_DefaultToVersionOneAndAttributeTargetsProperties() {
        IPersonalDataPolicy policy = new TestPersonalDataPolicy();
        ICanonicalPersonalDataPathPolicy pathPolicy = new TestCanonicalPathPolicy();
        AttributeUsageAttribute usage = typeof(PersonalDataAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>()
            .ShouldNotBeNull();

        policy.ContractVersion.ShouldBe(1);
        pathPolicy.ContractVersion.ShouldBe(1);
        usage.ValidOn.ShouldBe(AttributeTargets.Property);
        usage.AllowMultiple.ShouldBeFalse();
        usage.Inherited.ShouldBeTrue();
    }

    [Fact]
    public void PublicEnums_HaveExactApprovedDiscriminants() {
        Enum.GetValues<PayloadProtectionPayloadKind>().ShouldBe([
            PayloadProtectionPayloadKind.Event,
            PayloadProtectionPayloadKind.Snapshot,
        ]);
        Enum.GetValues<PersonalDataPolicyDecision>().ShouldBe([
            PersonalDataPolicyDecision.Abstain,
            PersonalDataPolicyDecision.Protect,
        ]);
        Enum.GetValues<PayloadErasureState>().ShouldBe([
            PayloadErasureState.Active,
            PayloadErasureState.Pending,
            PayloadErasureState.Invalidating,
            PayloadErasureState.Invalidated,
            PayloadErasureState.Deleted,
            PayloadErasureState.Unknown,
            PayloadErasureState.Unavailable,
            PayloadErasureState.Denied,
        ]);
        Enum.GetValues<PayloadErasureReasonCode>().ShouldBe([
            PayloadErasureReasonCode.None,
            PayloadErasureReasonCode.DomainPending,
            PayloadErasureReasonCode.DomainInvalidating,
            PayloadErasureReasonCode.DomainInvalidated,
            PayloadErasureReasonCode.DomainDeleted,
            PayloadErasureReasonCode.StateUnavailable,
            PayloadErasureReasonCode.StateDenied,
            PayloadErasureReasonCode.StateUnknown,
            PayloadErasureReasonCode.EpochInvalid,
            PayloadErasureReasonCode.EpochRegressed,
        ]);
        Enum.GetValues<PayloadProtectionPersistenceOutcome>().ShouldBe([
            PayloadProtectionPersistenceOutcome.Persisted,
            PayloadProtectionPersistenceOutcome.NotPersisted,
            PayloadProtectionPersistenceOutcome.Unknown,
        ]);

        ((int)PayloadProtectionPayloadKind.Event).ShouldBe(1);
        ((int)PayloadProtectionPayloadKind.Snapshot).ShouldBe(2);
        ((int)PersonalDataPolicyDecision.Abstain).ShouldBe(0);
        ((int)PersonalDataPolicyDecision.Protect).ShouldBe(1);
        Enum.GetValues<PayloadErasureState>().Select(static value => (int)value).ShouldBe(Enumerable.Range(0, 8));
        Enum.GetValues<PayloadErasureReasonCode>().Select(static value => (int)value).ShouldBe(Enumerable.Range(0, 10));
        Enum.GetValues<PayloadProtectionPersistenceOutcome>().Select(static value => (int)value).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void PublicContracts_HaveExactApprovedConstructorsPropertiesAndMethods() {
        AssertPublicConstructor<PayloadProtectionOccurrenceContext>(typeof(ulong), typeof(PayloadProtectionPayloadKind), typeof(string));
        AssertPublicProperties<PayloadProtectionOccurrenceContext>(
            ("RecordSequence", typeof(ulong)),
            ("PayloadKind", typeof(PayloadProtectionPayloadKind)),
            ("PayloadTypeId", typeof(string)));
        AssertPublicConstructor<PersonalDataPolicyContext>(
            typeof(AggregateIdentity), typeof(object), typeof(JsonElement), typeof(JsonTypeInfo), typeof(object),
            typeof(PropertyInfo), typeof(JsonElement), typeof(string), typeof(string), typeof(PayloadProtectionPayloadKind));
        AssertPublicConstructor<PayloadErasureStateRequest>(typeof(AggregateIdentity), typeof(string), typeof(uint?));
        AssertPublicProperties<PayloadErasureStateRequest>(
            ("Identity", typeof(AggregateIdentity)),
            ("KeyReference", typeof(string)),
            ("DekVersion", typeof(uint?)));
        AssertPublicConstructor<PayloadErasureStateResult>(typeof(PayloadErasureState), typeof(ulong), typeof(PayloadErasureReasonCode), typeof(DateTimeOffset));
        AssertPublicProperties<PayloadErasureStateResult>(
            ("State", typeof(PayloadErasureState)),
            ("LifecycleEpoch", typeof(ulong)),
            ("ReasonCode", typeof(PayloadErasureReasonCode)),
            ("ObservedAtUtc", typeof(DateTimeOffset)));
        AssertPublicConstructor<ProtectedSnapshotPayloadV2>(typeof(string), typeof(string), typeof(string));
        AssertPublicProperties<ProtectedSnapshotPayloadV2>(
            ("Format", typeof(string)),
            ("SnapshotTypeId", typeof(string)),
            ("Envelope", typeof(string)));
        AssertPublicConstructor<PayloadProtectionCompletionContext>(
            typeof(AggregateIdentity), typeof(PayloadProtectionOccurrenceContext), typeof(string), typeof(string), typeof(uint), typeof(ulong));
        AssertPublicProperties<PayloadProtectionCompletionContext>(
            ("Identity", typeof(AggregateIdentity)),
            ("Occurrence", typeof(PayloadProtectionOccurrenceContext)),
            ("OperationId", typeof(string)),
            ("KeyReference", typeof(string)),
            ("DekVersion", typeof(uint)),
            ("LifecycleEpoch", typeof(ulong)));
        AssertPublicConstructor<PayloadProtectionWriteResult>(typeof(PayloadProtectionResult), typeof(PayloadProtectionCompletionContext));
        AssertPublicProperties<PayloadProtectionWriteResult>(
            ("ProtectionResult", typeof(PayloadProtectionResult)),
            ("CompletionContext", typeof(PayloadProtectionCompletionContext)));
        AssertPublicConstructor<SnapshotProtectionWriteResult>(typeof(SnapshotProtectionResult), typeof(PayloadProtectionCompletionContext));
        AssertPublicProperties<SnapshotProtectionWriteResult>(
            ("ProtectionResult", typeof(SnapshotProtectionResult)),
            ("CompletionContext", typeof(PayloadProtectionCompletionContext)));

        AssertInterfaceProperty<IPersonalDataPolicy>("ContractVersion", typeof(int), hasDefault: true);
        AssertInterfaceProperty<IPersonalDataPolicy>("PolicyId", typeof(string), hasDefault: false);
        AssertInterfaceProperty<IPersonalDataPolicy>("PolicyVersion", typeof(int), hasDefault: false);
        AssertInterfaceProperty<IPersonalDataPolicy>("Order", typeof(int), hasDefault: false);
        RequireMethod(typeof(IPersonalDataPolicy), "Evaluate", typeof(PersonalDataPolicyContext)).ReturnType.ShouldBe(typeof(PersonalDataPolicyDecision));

        AssertInterfaceProperty<ICanonicalPersonalDataPathPolicy>("ContractVersion", typeof(int), hasDefault: true);
        AssertInterfaceProperty<ICanonicalPersonalDataPathPolicy>("PolicyId", typeof(string), hasDefault: false);
        AssertInterfaceProperty<ICanonicalPersonalDataPathPolicy>("PolicyVersion", typeof(int), hasDefault: false);
        AssertInterfaceProperty<ICanonicalPersonalDataPathPolicy>("Order", typeof(int), hasDefault: false);
        RequireMethod(
            typeof(ICanonicalPersonalDataPathPolicy),
            "SelectCanonicalJsonPointers",
            typeof(AggregateIdentity),
            typeof(JsonElement),
            typeof(JsonTypeInfo),
            typeof(PayloadProtectionPayloadKind)).ReturnType.ShouldBe(typeof(IReadOnlyList<string>));

        AssertInterfaceProperty<IErasureStateProvider>("ContractVersion", typeof(int), hasDefault: true);
        MethodInfo erasureMethod = RequireMethod(
            typeof(IErasureStateProvider),
            "GetStateAsync",
            typeof(PayloadErasureStateRequest),
            typeof(CancellationToken));
        erasureMethod.ReturnType.ShouldBe(typeof(ValueTask<PayloadErasureStateResult>));
        erasureMethod.GetParameters()[1].IsOptional.ShouldBeTrue();
    }

    [Fact]
    public void WriteResults_RequireCompletionContextExactlyForV2() {
        PayloadProtectionCompletionContext completion = CompletionContext(EventOccurrence());
        var legacyEvent = new PayloadProtectionResult([1], "json");
        var v2Event = new PayloadProtectionResult([1], V2Format, V2Metadata());
        var legacySnapshot = new SnapshotProtectionResult(new TestSnapshot("safe"), EventStorePayloadProtectionMetadata.Unprotected());
        var v2Snapshot = new SnapshotProtectionResult(
            new ProtectedSnapshotPayloadV2(V2Format, "hx-snapshot-v1:party-state", "SFhQMg"),
            V2Metadata());

        _ = new PayloadProtectionWriteResult(legacyEvent, null);
        _ = new PayloadProtectionWriteResult(v2Event, completion);
        _ = new SnapshotProtectionWriteResult(legacySnapshot, null);
        _ = new SnapshotProtectionWriteResult(v2Snapshot, completion);
        _ = Should.Throw<ArgumentException>(() => new PayloadProtectionWriteResult(legacyEvent, completion));
        _ = Should.Throw<ArgumentException>(() => new PayloadProtectionWriteResult(v2Event, null));
        _ = Should.Throw<ArgumentException>(() => new SnapshotProtectionWriteResult(legacySnapshot, completion));
        _ = Should.Throw<ArgumentException>(() => new SnapshotProtectionWriteResult(v2Snapshot, null));
    }

    [Fact]
    public void ProtectedSnapshotCarrier_SerializesExactDurableMemberNamesAndOrder() {
        var carrier = new ProtectedSnapshotPayloadV2(V2Format, "hx-snapshot-v1:party-state", "SFhQMg");

        string json = JsonSerializer.Serialize(carrier);
        ProtectedSnapshotPayloadV2 roundTrip = JsonSerializer.Deserialize<ProtectedSnapshotPayloadV2>(json).ShouldNotBeNull();
        using JsonDocument document = JsonDocument.Parse(json);

        json.ShouldBe("{\"Format\":\"json\\u002Bpdenc-v2\",\"SnapshotTypeId\":\"hx-snapshot-v1:party-state\",\"Envelope\":\"SFhQMg\"}");
        document.RootElement.EnumerateObject().Select(static property => property.Name).ShouldBe([
            "Format",
            "SnapshotTypeId",
            "Envelope",
        ]);
        roundTrip.ShouldBe(carrier);
    }

    [Fact]
    public async Task ContextProtect_DefaultDelegatesLegacyAndReturnsNoCompletionContext() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        AggregateIdentity identity = Identity();
        var eventPayload = new TestEventPayload();
        byte[] payloadBytes = [1, 2, 3];
        PayloadProtectionOccurrenceContext occurrence = EventOccurrence();
        using var cancellation = new CancellationTokenSource();

        PayloadProtectionWriteResult result = await service.ProtectEventPayloadAsync(
            identity,
            eventPayload,
            EventTypeName,
            payloadBytes,
            "json",
            occurrence,
            cancellation.Token);

        provider.EventProtectCalls.ShouldBe(1);
        provider.LastIdentity.ShouldBeSameAs(identity);
        provider.LastEventPayload.ShouldBeSameAs(eventPayload);
        provider.LastEventTypeName.ShouldBeSameAs(EventTypeName);
        provider.LastPayloadBytes.ShouldBeSameAs(payloadBytes);
        provider.LastSerializationFormat.ShouldBeSameAs("json");
        provider.LastCancellationToken.ShouldBe(cancellation.Token);
        result.ProtectionResult.ShouldBeSameAs(provider.EventProtectResult);
        result.CompletionContext.ShouldBeNull();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ContextProtect_DefaultRejectsEveryV2Claim(bool v2Format, bool v2Metadata) {
        EventStorePayloadProtectionMetadata metadata = v2Metadata
            ? V2Metadata()
            : EventStorePayloadProtectionMetadata.Unprotected();
        var provider = new LegacyProvider {
            EventProtectResult = new PayloadProtectionResult([1], v2Format ? V2Format : "json", metadata),
        };
        IEventPayloadProtectionService service = provider;

        NotSupportedException exception = await Should.ThrowAsync<NotSupportedException>(async () =>
            await service.ProtectEventPayloadAsync(
                Identity(),
                new TestEventPayload(),
                EventTypeName,
                [1],
                "json",
                EventOccurrence(),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The legacy payload-protection adapter cannot process pdenc-v2.");
        provider.EventProtectCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ContextProtect_DefaultRejectsMismatchedOccurrenceBeforeLegacyCall() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        var occurrence = new PayloadProtectionOccurrenceContext(1, PayloadProtectionPayloadKind.Event, "Wrong.Type");

        _ = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.ProtectEventPayloadAsync(
                Identity(),
                new TestEventPayload(),
                EventTypeName,
                [1],
                "json",
                occurrence,
                TestContext.Current.CancellationToken));

        provider.EventProtectCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ContextEventDefaults_RejectNullEventTypeBeforeLegacyCall() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        var occurrence = new PayloadProtectionOccurrenceContext(1, PayloadProtectionPayloadKind.Event, EventTypeName);

        _ = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.ProtectEventPayloadAsync(
                Identity(), new TestEventPayload(), null!, [1], "json", occurrence, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.TryUnprotectEventPayloadAsync(
                Identity(), null!, [1], "json", null, occurrence, TestContext.Current.CancellationToken));

        provider.EventProtectCalls.ShouldBe(0);
        provider.EventUnprotectCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ContextUnprotect_DefaultReturnsTypedUnsupportedForEveryIndependentV2Signal(
        bool v2Format,
        bool v2Metadata) {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;

        PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
            Identity(),
            EventTypeName,
            [1],
            v2Format ? V2Format : "json",
            v2Metadata ? V2Metadata() : EventStorePayloadProtectionMetadata.Unprotected(),
            EventOccurrence(),
            TestContext.Current.CancellationToken);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        outcome.PayloadBytes.ShouldBeNull();
        provider.EventUnprotectCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ContextUnprotect_DefaultDelegatesNonV2WithSameCancellationToken() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        AggregateIdentity identity = Identity();
        byte[] payloadBytes = [1];
        using var cancellation = new CancellationTokenSource();

        PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
            identity,
            EventTypeName,
            payloadBytes,
            "json",
            EventStorePayloadProtectionMetadata.Unprotected(),
            EventOccurrence(),
            cancellation.Token);

        outcome.IsReadable.ShouldBeTrue();
        outcome.PayloadBytes.ShouldBeSameAs(payloadBytes);
        outcome.SerializationFormat.ShouldBeSameAs("json");
        provider.EventUnprotectCalls.ShouldBe(1);
        provider.LastIdentity.ShouldBeSameAs(identity);
        provider.LastEventTypeName.ShouldBeSameAs(EventTypeName);
        provider.LastPayloadBytes.ShouldBeSameAs(payloadBytes);
        provider.LastSerializationFormat.ShouldBeSameAs("json");
        provider.LastCancellationToken.ShouldBe(cancellation.Token);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ContextSnapshotUnprotect_DefaultRejectsEveryIndependentV2Signal(
        bool v2Carrier,
        bool v2Metadata) {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        JsonTypeInfo typeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(TestSnapshot));

        SnapshotUnprotectionOutcome v2Outcome = await service.TryUnprotectSnapshotAsync(
            Identity(),
            v2Carrier
                ? new ProtectedSnapshotPayloadV2(V2Format, "hx-snapshot-v1:party-state", "SFhQMg")
                : new TestSnapshot("safe"),
            v2Metadata ? V2Metadata() : EventStorePayloadProtectionMetadata.Unprotected(),
            typeInfo,
            SnapshotOccurrence(),
            TestContext.Current.CancellationToken);

        v2Outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        provider.SnapshotUnprotectCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ContextSnapshotProtect_DefaultDelegatesOrdinaryState() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        var state = new TestSnapshot("safe");

        SnapshotProtectionWriteResult result = await service.ProtectSnapshotAsync(
            Identity(),
            state,
            JsonSerializerOptions.Default.GetTypeInfo(typeof(TestSnapshot)),
            SnapshotOccurrence(),
            TestContext.Current.CancellationToken);

        result.ProtectionResult.State.ShouldBeSameAs(state);
        result.ProtectionResult.Metadata.State.ShouldBe(PayloadProtectionState.Unprotected);
        result.CompletionContext.ShouldBeNull();
        provider.SnapshotProtectCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ContextSnapshotProtect_DefaultRejectsEveryLegacyV2Claim(
        bool v2Carrier,
        bool v2Metadata) {
        var provider = new LegacyProvider {
            SnapshotProtectResult = new SnapshotProtectionResult(
                v2Carrier
                    ? new ProtectedSnapshotPayloadV2(V2Format, "hx-snapshot-v1:party-state", "SFhQMg")
                    : new TestSnapshot("safe"),
                v2Metadata ? V2Metadata() : EventStorePayloadProtectionMetadata.Unprotected()),
        };
        IEventPayloadProtectionService service = provider;

        NotSupportedException exception = await Should.ThrowAsync<NotSupportedException>(async () =>
            await service.ProtectSnapshotAsync(
                Identity(),
                new TestSnapshot("safe"),
                JsonSerializerOptions.Default.GetTypeInfo(typeof(TestSnapshot)),
                SnapshotOccurrence(),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The legacy payload-protection adapter cannot process pdenc-v2.");
        provider.SnapshotProtectCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ContextSnapshotUnprotect_DefaultDelegatesNonV2WithSameCancellationToken() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        using var cancellation = new CancellationTokenSource();
        var state = new TestSnapshot("safe");

        SnapshotUnprotectionOutcome outcome = await service.TryUnprotectSnapshotAsync(
            Identity(),
            state,
            EventStorePayloadProtectionMetadata.Unprotected(),
            JsonSerializerOptions.Default.GetTypeInfo(typeof(TestSnapshot)),
            SnapshotOccurrence(),
            cancellation.Token);

        outcome.IsReadable.ShouldBeTrue();
        outcome.State.ShouldBeSameAs(state);
        provider.SnapshotUnprotectCalls.ShouldBe(1);
        provider.LastCancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task NewDefaults_RejectUndefinedOrMismatchedOccurrenceBeforeProvider() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;

        _ = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.ProtectEventPayloadAsync(
                Identity(),
                new TestEventPayload(),
                EventTypeName,
                [1],
                "json",
                new PayloadProtectionOccurrenceContext(1, 0, EventTypeName),
                TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.ProtectSnapshotAsync(
                Identity(),
                new TestSnapshot("safe"),
                JsonSerializerOptions.Default.GetTypeInfo(typeof(TestSnapshot)),
                EventOccurrence(),
                TestContext.Current.CancellationToken));

        provider.EventProtectCalls.ShouldBe(0);
        provider.SnapshotProtectCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AllNewDefaults_ObserveCancellationBeforeArgumentOrProviderHandling() {
        var provider = new LegacyProvider();
        IEventPayloadProtectionService service = provider;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await AssertCanceledAsync(() => service.ProtectEventPayloadAsync(
            null!, null!, null!, null!, null!, null!, cancellation.Token));
        await AssertCanceledAsync(() => service.ProtectSnapshotAsync(
            null!, null!, null!, null!, cancellation.Token));
        await AssertCanceledAsync(() => service.TryUnprotectEventPayloadAsync(
            null!, null!, null!, null!, null, null!, cancellation.Token));
        await AssertCanceledAsync(() => service.TryUnprotectSnapshotAsync(
            null!, null!, null, null!, null!, cancellation.Token));
        await AssertCanceledAsync(() => service.AcquirePayloadProtectionCompletionLeaseAsync(
            null!, cancellation.Token));
        await AssertCanceledAsync(() => service.CompletePayloadProtectionAsync(
            null!, 0, cancellation.Token));

        provider.EventProtectCalls.ShouldBe(0);
        provider.EventUnprotectCalls.ShouldBe(0);
        provider.SnapshotProtectCalls.ShouldBe(0);
        provider.SnapshotUnprotectCalls.ShouldBe(0);
    }

    [Fact]
    public async Task CompletionDefaults_AreExplicitAndValidatePersistenceOutcome() {
        IEventPayloadProtectionService service = new LegacyProvider();
        PayloadProtectionCompletionContext completion = CompletionContext(EventOccurrence());

        NotSupportedException acquire = await Should.ThrowAsync<NotSupportedException>(async () =>
            await service.AcquirePayloadProtectionCompletionLeaseAsync(
                completion,
                TestContext.Current.CancellationToken));
        NotSupportedException complete = await Should.ThrowAsync<NotSupportedException>(async () =>
            await service.CompletePayloadProtectionAsync(
                completion,
                PayloadProtectionPersistenceOutcome.Persisted,
                TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await service.CompletePayloadProtectionAsync(
                completion,
                (PayloadProtectionPersistenceOutcome)0,
                TestContext.Current.CancellationToken));

        acquire.Message.ShouldBe("The payload-protection provider does not support persistence completion.");
        complete.Message.ShouldBe(acquire.Message);
    }

    [Fact]
    public async Task CompletionDefaults_RejectNullNonCanonicalAndZeroFields() {
        IEventPayloadProtectionService service = new LegacyProvider();
        PayloadProtectionCompletionContext valid = CompletionContext(EventOccurrence());
        PayloadProtectionCompletionContext[] invalid = [
            valid with { OperationId = null! },
            valid with { OperationId = "01j00000000000000000000001" },
            valid with { OperationId = "81J00000000000000000000001" },
            valid with { KeyReference = "01I00000000000000000000000" },
            valid with { DekVersion = 0 },
            valid with { Occurrence = new PayloadProtectionOccurrenceContext(1, 0, EventTypeName) },
        ];

        _ = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.AcquirePayloadProtectionCompletionLeaseAsync(
                null!,
                TestContext.Current.CancellationToken));
        foreach (PayloadProtectionCompletionContext context in invalid) {
            _ = await Should.ThrowAsync<ArgumentException>(async () =>
                await service.AcquirePayloadProtectionCompletionLeaseAsync(
                    context,
                    TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task ContextAwareOverride_ReceivesOccurrenceLosslessly() {
        var provider = new ContextAwareProvider();
        IEventPayloadProtectionService service = provider;
        PayloadProtectionOccurrenceContext occurrence = EventOccurrence(ulong.MaxValue);

        PayloadProtectionWriteResult result = await service.ProtectEventPayloadAsync(
            Identity(),
            new TestEventPayload(),
            EventTypeName,
            [1],
            "json",
            occurrence,
            TestContext.Current.CancellationToken);

        provider.SeenOccurrence.ShouldBeSameAs(occurrence);
        result.CompletionContext.ShouldBeNull();
        provider.EventProtectCalls.ShouldBe(0);
    }

    [Fact]
    public void CallScopedRecords_ToStringIsConstructivelyBounded() {
        const string canary = "CANARY_PERSONAL_VALUE_7281";
        using JsonDocument document = JsonDocument.Parse($"{{\"value\":\"{canary}\"}}");
        JsonElement root = document.RootElement.Clone();
        var policyContext = new PersonalDataPolicyContext(
            Identity(),
            new TestSnapshot(canary),
            root,
            JsonSerializerOptions.Default.GetTypeInfo(typeof(TestSnapshot)),
            null,
            null,
            root.GetProperty("value"),
            $"/{canary}",
            $"Type.{canary}",
            PayloadProtectionPayloadKind.Event);
        var carrier = new ProtectedSnapshotPayloadV2(V2Format, $"hx-snapshot-v1:{canary.ToLowerInvariant()}", canary);
        var occurrence = new PayloadProtectionOccurrenceContext(
            1,
            PayloadProtectionPayloadKind.Event,
            $"Type.{canary}");
        PayloadProtectionCompletionContext completion = CompletionContext(occurrence);
        var erasureRequest = new PayloadErasureStateRequest(Identity(), $"KEY-{canary}", 1);
        var eventWrite = new PayloadProtectionWriteResult(
            new PayloadProtectionResult(Encoding.UTF8.GetBytes(canary), V2Format, V2Metadata()),
            completion);
        var snapshotWrite = new SnapshotProtectionWriteResult(
            new SnapshotProtectionResult(carrier, V2Metadata()),
            completion);

        object[] values = [policyContext, carrier, completion, occurrence, erasureRequest, eventWrite, snapshotWrite];
        values.ShouldAllBe(value => !value.ToString()!.Contains(canary, StringComparison.Ordinal));
        values.ShouldAllBe(value => !value.ToString()!.Contains(completion.KeyReference, StringComparison.Ordinal));
    }

    [Fact]
    public void GoldenV001V002_ReconstructsExactManifestAadEnvelopeAndRoundTrip() {
        using JsonDocument fixture = ReadFixture("g-001.json");
        HashHex(File.ReadAllBytes(Path.Combine(_fixtureDirectory, "g-001.json"))).ShouldBe(GoldenFileHash);
        fixture.RootElement.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
        fixture.RootElement.GetProperty("vectorId").GetString().ShouldBe("V001");
        fixture.RootElement.GetProperty("normativeDigest").GetString().ShouldBe(ApprovedDigest);
        RecomputeNormativeDigest().ShouldBe(ApprovedDigest);
        JsonElement input = fixture.RootElement.GetProperty("input");
        JsonElement expected = fixture.RootElement.GetProperty("expected");
        string pathText = input.GetProperty("propertyPath").GetProperty("text").GetString()!;
        byte[] manifest = BuildManifest([Encoding.UTF8.GetBytes(pathText)]);
        string manifestHash = HashHex(manifest);

        Hex(manifest).ShouldBe(fixture.RootElement.GetProperty("pathManifest").GetProperty("encodedHex").GetString());
        manifestHash.ShouldBe(fixture.RootElement.GetProperty("pathManifest").GetProperty("sha256").GetString());

        byte[][] aadValues =
        [
            ReadText(input, "tenant"),
            ReadText(input, "domain"),
            ReadText(input, "aggregate"),
            ReadText(input, "payloadType"),
            ReadText(input, "propertyPath"),
            ReadText(input, "keyReference"),
            U32(input.GetProperty("dekVersion").GetUInt32()),
            ReadText(input, "serializationFormat"),
            U32(input.GetProperty("fieldOrdinal").GetUInt32()),
            U64(ulong.Parse(input.GetProperty("recordSequence").GetString()!, System.Globalization.CultureInfo.InvariantCulture)),
            SHA256.HashData(manifest),
        ];
        byte[] aadTypes = [1, 1, 1, 1, 1, 1, 2, 1, 2, 3, 4];
        byte[] aad = BuildAad(aadValues, aadTypes);
        Hex(aad).ShouldBe(expected.GetProperty("aadHex").GetString());
        HashHex(aad).ShouldBe(expected.GetProperty("aadSha256").GetString());

        byte[] key = Convert.FromHexString(input.GetProperty("dekHex").GetString()!);
        byte[] nonce = Convert.FromHexString(input.GetProperty("nonceHex").GetString()!);
        byte[] plaintext = ReadText(input, "plaintext");
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using (var aes = new AesGcm(key, tag.Length)) {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        }

        Hex(ciphertext).ShouldBe(expected.GetProperty("ciphertextHex").GetString());
        Hex(tag).ShouldBe(expected.GetProperty("tagHex").GetString());

        byte[] envelope = BuildEnvelope(
            input.GetProperty("dekVersion").GetUInt32(),
            input.GetProperty("fieldOrdinal").GetUInt32(),
            ReadText(input, "keyReference"),
            nonce,
            ciphertext,
            tag);
        string envelopeText = Base64Url(envelope);
        string wrapper = $"{{\"$pdenc\":\"{envelopeText}\"}}";
        Hex(envelope).ShouldBe(expected.GetProperty("envelopeHex").GetString());
        envelopeText.ShouldBe(expected.GetProperty("envelopeBase64Url").GetString());
        HashHex(envelope).ShouldBe(expected.GetProperty("envelopeSha256").GetString());
        wrapper.ShouldBe(expected.GetProperty("wrapper").GetString());
        HashHex(Encoding.UTF8.GetBytes(wrapper)).ShouldBe(expected.GetProperty("wrapperSha256").GetString());

        byte[] decodedEnvelope = FromBase64Url(expected.GetProperty("envelopeBase64Url").GetString()!);
        Base64Url(decodedEnvelope).ShouldBe(expected.GetProperty("envelopeBase64Url").GetString());
        Hex(decodedEnvelope).ShouldBe(expected.GetProperty("envelopeHex").GetString());
        decodedEnvelope.AsSpan(0, 4).SequenceEqual("HXP2"u8).ShouldBeTrue();
        decodedEnvelope.AsSpan(4, 4).SequenceEqual(new byte[] { 2, 1, 1, 1 }).ShouldBeTrue();
        BinaryPrimitives.ReadUInt16BigEndian(decodedEnvelope.AsSpan(8, 2)).ShouldBe((ushort)28);
        BinaryPrimitives.ReadUInt16BigEndian(decodedEnvelope.AsSpan(10, 2)).ShouldBe((ushort)26);
        BinaryPrimitives.ReadUInt32BigEndian(decodedEnvelope.AsSpan(12, 4)).ShouldBe(input.GetProperty("dekVersion").GetUInt32());
        BinaryPrimitives.ReadUInt32BigEndian(decodedEnvelope.AsSpan(16, 4)).ShouldBe(input.GetProperty("fieldOrdinal").GetUInt32());
        decodedEnvelope.AsSpan(20, 4).SequenceEqual(new byte[] { 12, 16, 0, 0 }).ShouldBeTrue();
        int decodedCiphertextLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(decodedEnvelope.AsSpan(24, 4)));
        decodedEnvelope.Length.ShouldBe(28 + 26 + 12 + decodedCiphertextLength + 16);
        decodedEnvelope.AsSpan(28, 26).SequenceEqual(ReadText(input, "keyReference")).ShouldBeTrue();
        ReadOnlySpan<byte> decodedNonce = decodedEnvelope.AsSpan(54, 12);
        ReadOnlySpan<byte> decodedCiphertext = decodedEnvelope.AsSpan(66, decodedCiphertextLength);
        ReadOnlySpan<byte> decodedTag = decodedEnvelope.AsSpan(66 + decodedCiphertextLength, 16);
        byte[] decrypted = new byte[decodedCiphertextLength];
        using (var aes = new AesGcm(key, tag.Length)) {
            aes.Decrypt(decodedNonce, decodedCiphertext, decodedTag, decrypted, aad);
        }

        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void GoldenV001V002_EachAuthenticatedInputMutationFailsClosed() {
        using JsonDocument fixture = ReadFixture("g-001.json");
        JsonElement input = fixture.RootElement.GetProperty("input");
        JsonElement expected = fixture.RootElement.GetProperty("expected");
        byte[] manifest = BuildManifest([ReadText(input, "propertyPath")]);
        byte[][] aadValues = [
            ReadText(input, "tenant"),
            ReadText(input, "domain"),
            ReadText(input, "aggregate"),
            ReadText(input, "payloadType"),
            ReadText(input, "propertyPath"),
            ReadText(input, "keyReference"),
            U32(input.GetProperty("dekVersion").GetUInt32()),
            ReadText(input, "serializationFormat"),
            U32(input.GetProperty("fieldOrdinal").GetUInt32()),
            U64(ulong.Parse(input.GetProperty("recordSequence").GetString()!, System.Globalization.CultureInfo.InvariantCulture)),
            SHA256.HashData(manifest),
        ];
        byte[] aadTypes = [1, 1, 1, 1, 1, 1, 2, 1, 2, 3, 4];
        byte[] key = Convert.FromHexString(input.GetProperty("dekHex").GetString()!);
        byte[] nonce = Convert.FromHexString(input.GetProperty("nonceHex").GetString()!);
        byte[] ciphertext = Convert.FromHexString(expected.GetProperty("ciphertextHex").GetString()!);
        byte[] tag = Convert.FromHexString(expected.GetProperty("tagHex").GetString()!);

        for (int index = 0; index < aadValues.Length; index++) {
            byte[][] mutation = aadValues.Select(static value => value.ToArray()).ToArray();
            mutation[index][0] ^= 1;
            AssertAuthenticationFailure(key, nonce, ciphertext, tag, BuildAad(mutation, aadTypes));
        }

        byte[] expectedAad = BuildAad(aadValues, aadTypes);
        byte[] ciphertextMutation = ciphertext.ToArray();
        ciphertextMutation[0] ^= 1;
        AssertAuthenticationFailure(key, nonce, ciphertextMutation, tag, expectedAad);

        byte[] tagMutation = tag.ToArray();
        tagMutation[0] ^= 1;
        AssertAuthenticationFailure(key, nonce, ciphertext, tagMutation, expectedAad);
    }

    [Fact]
    public void GoldenV003_ReproducesNistAes256GcmControl() {
        using JsonDocument fixture = ReadFixture("nist-gcm-256-count0.json");
        HashHex(File.ReadAllBytes(Path.Combine(_fixtureDirectory, "nist-gcm-256-count0.json"))).ShouldBe(NistFileHash);
        JsonElement root = fixture.RootElement;
        root.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
        root.GetProperty("vectorId").GetString().ShouldBe("V003");
        root.GetProperty("source").GetString().ShouldBe("NIST CAVP gcmEncryptExtIV256.rsp Count 0");
        root.GetProperty("profile").GetRawText().ShouldBe("{\"keyBits\": 256, \"ivBits\": 96, \"plaintextBits\": 0, \"aadBits\": 0, \"tagBits\": 128}");
        root.GetProperty("keyHex").GetString().ShouldBe("b52c505a37d78eda5dd34f20c22540ea1b58963cf8e5bf8ffa85f9f2492505b4");
        root.GetProperty("ivHex").GetString().ShouldBe("516c33929df5a3284ff463d7");
        root.GetProperty("tagHex").GetString().ShouldBe("bdc1ac884d332457a1d2664f168c76f0");
        byte[] key = Convert.FromHexString(root.GetProperty("keyHex").GetString()!);
        byte[] nonce = Convert.FromHexString(root.GetProperty("ivHex").GetString()!);
        byte[] tag = new byte[16];

        using (var aes = new AesGcm(key, tag.Length)) {
            aes.Encrypt(nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, tag, ReadOnlySpan<byte>.Empty);
        }

        Hex(tag).ShouldBe(root.GetProperty("tagHex").GetString());
    }

    [Fact]
    public void FixtureManifest_HashesEveryInputAndGatesAllFutureVectors() {
        using JsonDocument manifest = ReadFixture("manifest.json");
        string[] requiredPaths = [
            "../../../../../scripts/payload-protection/requirements.txt",
            "../../../../../scripts/payload-protection/verify-golden-vectors.mjs",
            "../../../../../scripts/payload-protection/verify-golden-vectors.py",
            "g-001.json",
            "nist-gcm-256-count0.json",
            "vector-ownership.json",
        ];
        JsonElement.ArrayEnumerator fileEntries = manifest.RootElement.GetProperty("files").EnumerateArray();
        string[] manifestPaths = fileEntries.Select(static file => file.GetProperty("path").GetString()!).ToArray();
        manifestPaths.Distinct(StringComparer.Ordinal).Count().ShouldBe(manifestPaths.Length);
        manifestPaths.Order(StringComparer.Ordinal).ShouldBe(requiredPaths.Order(StringComparer.Ordinal));

        foreach (JsonElement file in manifest.RootElement.GetProperty("files").EnumerateArray()) {
            string relativePath = file.GetProperty("path").GetString()!;
            string expectedHash = file.GetProperty("sha256").GetString()!;
            string fullPath = relativePath.StartsWith("../../../../../scripts/", StringComparison.Ordinal)
                ? Path.Combine(_fixtureDirectory, "verifiers", Path.GetFileName(relativePath))
                : Path.Combine(_fixtureDirectory, relativePath);
            Path.GetFullPath(fullPath).StartsWith(Path.GetFullPath(_fixtureDirectory) + Path.DirectorySeparatorChar, StringComparison.Ordinal).ShouldBeTrue();
            HashHex(File.ReadAllBytes(fullPath)).ShouldBe(expectedHash);
        }

        using JsonDocument ownership = ReadFixture("vector-ownership.json");
        HashHex(File.ReadAllBytes(Path.Combine(_fixtureDirectory, "vector-ownership.json"))).ShouldBe(OwnershipFileHash);
        ownership.RootElement.GetProperty("schemaVersion").GetInt32().ShouldBe(1);
        ownership.RootElement.GetProperty("normativeDigest").GetString().ShouldBe(ApprovedDigest);
        ownership.RootElement.GetProperty("normativeRegistry").GetString().ShouldBe("spec-shared-payload-protection-engine.md#15.4");
        manifest.RootElement.GetProperty("normativeDigest").GetString().ShouldBe(ApprovedDigest);
        var ids = new List<int>();
        var executed = new List<int>();
        foreach (JsonElement assignment in ownership.RootElement.GetProperty("assignments").EnumerateArray()) {
            int first = assignment.GetProperty("first").GetInt32();
            int last = assignment.GetProperty("last").GetInt32();
            (first >= 1 && last <= 138 && first <= last).ShouldBeTrue();
            assignment.GetProperty("state").GetString().ShouldBe(first <= 3 ? "executed" : "predecessor-gated");
            for (int id = first; id <= last; id++) {
                ids.Add(id);
                if (assignment.GetProperty("state").GetString() == "executed") {
                    executed.Add(id);
                }
            }
        }

        ids.ShouldBe(Enumerable.Range(1, 138));
        executed.ShouldBe([1, 2, 3]);
        (ids.Count - executed.Count).ShouldBe(135);

        JsonElement expected = manifest.RootElement.GetProperty("expected");
        expected.GetProperty("executedVectors").EnumerateArray().Select(static value => value.GetString()).ShouldBe([
            "V001",
            "V002",
            "V003",
        ]);
        expected.GetProperty("predecessorGatedVectorCount").GetInt32().ShouldBe(135);
        expected.GetProperty("outcome").GetString().ShouldBe("PASS");
    }

    private static void AssertAuthenticationFailure(
        byte[] key,
        byte[] nonce,
        byte[] ciphertext,
        byte[] tag,
        byte[] aad) {
        byte[] destination = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, tag.Length);

        _ = Should.Throw<CryptographicException>(() => aes.Decrypt(nonce, ciphertext, tag, destination, aad));
        CryptographicOperations.ZeroMemory(destination);
    }

    private static async Task AssertCanceledAsync(Func<Task> action)
        => _ = await Should.ThrowAsync<OperationCanceledException>(action);

    private static void AssertInterfaceProperty<T>(string name, Type propertyType, bool hasDefault) {
        PropertyInfo property = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Required public property {name} was not found.");
        property.PropertyType.ShouldBe(propertyType);
        property.CanRead.ShouldBeTrue();
        property.CanWrite.ShouldBeFalse();
        property.GetMethod.ShouldNotBeNull().IsAbstract.ShouldBe(!hasDefault);
    }

    private static void AssertPublicConstructor<T>(params Type[] parameterTypes) {
        ConstructorInfo constructor = typeof(T).GetConstructor(parameterTypes)
            ?? throw new InvalidOperationException($"Required public constructor for {typeof(T).Name} was not found.");
        constructor.IsPublic.ShouldBeTrue();
        typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length.ShouldBe(1);
    }

    private static void AssertPublicProperties<T>(params (string Name, Type Type)[] expected) {
        PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        properties
            .Select(static property => (property.Name, property.PropertyType))
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ShouldBe(expected.OrderBy(static property => property.Name, StringComparer.Ordinal));
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value) {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch {
            0 => padded,
            2 => padded + "==",
            3 => padded + "=",
            _ => throw new FormatException("The base64url value has an invalid length."),
        };
        return Convert.FromBase64String(padded);
    }

    private static byte[] BuildAad(IReadOnlyList<byte[]> values, IReadOnlyList<byte> types) {
        using var stream = new MemoryStream();
        stream.Write("HXAD"u8);
        stream.Write([1, 1, 11, 0]);
        for (int index = 0; index < values.Count; index++) {
            stream.WriteByte((byte)(index + 1));
            stream.WriteByte(types[index]);
            stream.Write(U32((uint)values[index].Length));
            stream.Write(values[index]);
        }

        return stream.ToArray();
    }

    private static byte[] BuildEnvelope(
        uint dekVersion,
        uint ordinal,
        byte[] keyReference,
        byte[] nonce,
        byte[] ciphertext,
        byte[] tag) {
        byte[] header = new byte[28];
        "HXP2"u8.CopyTo(header);
        header[4] = 2;
        header[5] = 1;
        header[6] = 1;
        header[7] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8), 28);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10), 26);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(12), dekVersion);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), ordinal);
        header[20] = 12;
        header[21] = 16;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(24), (uint)ciphertext.Length);
        return [.. header, .. keyReference, .. nonce, .. ciphertext, .. tag];
    }

    private static byte[] BuildManifest(IReadOnlyList<byte[]> paths) {
        using var stream = new MemoryStream();
        stream.Write("HXPM"u8);
        stream.WriteByte(1);
        stream.Write(U32((uint)paths.Count));
        foreach (byte[] path in paths.OrderBy(static value => value, ByteArrayComparer.Instance)) {
            stream.Write(U32((uint)path.Length));
            stream.Write(path);
        }

        return stream.ToArray();
    }

    private static PayloadProtectionCompletionContext CompletionContext(PayloadProtectionOccurrenceContext occurrence)
        => new(
            Identity(),
            occurrence,
            "01J00000000000000000000001",
            "01J00000000000000000000000",
            1,
            1);

    private static PayloadProtectionOccurrenceContext EventOccurrence(ulong sequence = 1)
        => new(sequence, PayloadProtectionPayloadKind.Event, EventTypeName);

    private static EventStorePayloadProtectionMetadata V2Metadata()
        => new(
            PayloadProtectionState.Protected,
            1,
            V2Scheme,
            null,
            "application/json",
            new Dictionary<string, string>(StringComparer.Ordinal) {
                ["format"] = V2Format,
                ["envelope"] = "pdenc-v2",
            });

    private static string HashHex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(bytes);

    private static AggregateIdentity Identity() => new("tenant-a", "parties", "party-01");

    private static JsonDocument ReadFixture(string name)
        => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_fixtureDirectory, name)));

    private static string RecomputeNormativeDigest() {
        byte[] bytes = File.ReadAllBytes(Path.Combine(_fixtureDirectory, "spec-shared-payload-protection-engine.md"));
        byte[] beginMarker = "<!-- HX-PP-V2-NORMATIVE-BEGIN -->\n"u8.ToArray();
        byte[] endMarker = "<!-- HX-PP-V2-NORMATIVE-END -->\n"u8.ToArray();
        int begin = bytes.AsSpan().IndexOf(beginMarker);
        int end = bytes.AsSpan().IndexOf(endMarker);

        begin.ShouldBeGreaterThanOrEqualTo(0);
        end.ShouldBeGreaterThan(begin);
        bytes.AsSpan(begin + beginMarker.Length).IndexOf(beginMarker).ShouldBe(-1);
        bytes.AsSpan(end + endMarker.Length).IndexOf(endMarker).ShouldBe(-1);
        bytes.Contains((byte)'\r').ShouldBeFalse();
        bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }).ShouldBeFalse();

        ReadOnlySpan<byte> normativeRange = bytes.AsSpan(begin + beginMarker.Length, end - begin - beginMarker.Length);
        return Convert.ToHexStringLower(SHA256.HashData(normativeRange));
    }

    private static byte[] ReadText(JsonElement input, string name)
        => Encoding.UTF8.GetBytes(input.GetProperty(name).GetProperty("text").GetString()!);

    private static MethodInfo RequireMethod(Type declaringType, string name, params Type[] parameterTypes)
        => declaringType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, parameterTypes)
            ?? throw new InvalidOperationException($"Required public method {name} was not found.");

    private static PayloadProtectionOccurrenceContext SnapshotOccurrence()
        => new(7, PayloadProtectionPayloadKind.Snapshot, "hx-snapshot-v1:party-state");

    private static byte[] U32(uint value) {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] U64(ulong value) {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private sealed class ByteArrayComparer : IComparer<byte[]> {
        public static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right)
            => left.AsSpan().SequenceCompareTo(right);
    }

    private sealed class ContextAwareProvider : LegacyProvider, IEventPayloadProtectionService {
        public PayloadProtectionOccurrenceContext? SeenOccurrence { get; private set; }

        public Task<PayloadProtectionWriteResult> ProtectEventPayloadAsync(
            AggregateIdentity identity,
            IEventPayload eventPayload,
            string eventTypeName,
            byte[] payloadBytes,
            string serializationFormat,
            PayloadProtectionOccurrenceContext occurrenceContext,
            CancellationToken cancellationToken = default) {
            SeenOccurrence = occurrenceContext;
            return Task.FromResult(new PayloadProtectionWriteResult(
                new PayloadProtectionResult(payloadBytes, serializationFormat),
                CompletionContext: null));
        }
    }

    private class LegacyProvider : IEventPayloadProtectionService {
        public int EventProtectCalls { get; private set; }

        public PayloadProtectionResult EventProtectResult { get; set; } = new([1, 2, 3], "json");

        public int EventUnprotectCalls { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public IEventPayload? LastEventPayload { get; private set; }

        public string? LastEventTypeName { get; private set; }

        public AggregateIdentity? LastIdentity { get; private set; }

        public byte[]? LastPayloadBytes { get; private set; }

        public string? LastSerializationFormat { get; private set; }

        public int SnapshotProtectCalls { get; private set; }

        public SnapshotProtectionResult? SnapshotProtectResult { get; init; }

        public int SnapshotUnprotectCalls { get; private set; }

        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(
            AggregateIdentity identity,
            IEventPayload eventPayload,
            string eventTypeName,
            byte[] payloadBytes,
            string serializationFormat,
            CancellationToken cancellationToken = default) {
            EventProtectCalls++;
            LastIdentity = identity;
            LastEventPayload = eventPayload;
            LastEventTypeName = eventTypeName;
            LastPayloadBytes = payloadBytes;
            LastSerializationFormat = serializationFormat;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(EventProtectResult);
        }

        public Task<SnapshotProtectionResult> ProtectSnapshotAsync(
            AggregateIdentity identity,
            object state,
            CancellationToken cancellationToken = default) {
            SnapshotProtectCalls++;
            LastIdentity = identity;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(SnapshotProtectResult
                ?? new SnapshotProtectionResult(state, EventStorePayloadProtectionMetadata.Unprotected()));
        }

        public Task<object> ProtectSnapshotStateAsync(
            AggregateIdentity identity,
            object state,
            CancellationToken cancellationToken = default) {
            SnapshotProtectCalls++;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(state);
        }

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
            AggregateIdentity identity,
            string eventTypeName,
            byte[] payloadBytes,
            string serializationFormat,
            CancellationToken cancellationToken = default) {
            EventUnprotectCalls++;
            LastIdentity = identity;
            LastEventTypeName = eventTypeName;
            LastPayloadBytes = payloadBytes;
            LastSerializationFormat = serializationFormat;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat));
        }

        public Task<object> UnprotectSnapshotStateAsync(
            AggregateIdentity identity,
            object state,
            CancellationToken cancellationToken = default) {
            SnapshotUnprotectCalls++;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(state);
        }
    }

    private sealed class TestCanonicalPathPolicy : ICanonicalPersonalDataPathPolicy {
        public int Order => 0;

        public string PolicyId => "test-canonical-path";

        public int PolicyVersion => 1;

        public IReadOnlyList<string> SelectCanonicalJsonPointers(
            AggregateIdentity identity,
            JsonElement serializedRoot,
            JsonTypeInfo rootTypeInfo,
            PayloadProtectionPayloadKind payloadKind)
            => [];
    }

    private sealed class TestEventPayload : IEventPayload;

    private sealed class TestPersonalDataPolicy : IPersonalDataPolicy {
        public int Order => 0;

        public string PolicyId => "test-personal-data";

        public int PolicyVersion => 1;

        public PersonalDataPolicyDecision Evaluate(PersonalDataPolicyContext context)
            => PersonalDataPolicyDecision.Abstain;
    }

    private sealed record TestSnapshot(string Value);
}
