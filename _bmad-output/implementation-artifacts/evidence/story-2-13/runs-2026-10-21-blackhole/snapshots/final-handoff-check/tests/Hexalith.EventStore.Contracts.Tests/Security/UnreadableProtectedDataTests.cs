using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>
/// Story 22.7b — unreadable-protected-data taxonomy contract tests.
/// </summary>
public class UnreadableProtectedDataTests {
    public static IEnumerable<object[]> AllReasons() {
        foreach (UnreadableProtectedDataReason reason in Enum.GetValues<UnreadableProtectedDataReason>()) {
            yield return new object[] { reason };
        }
    }

    [Theory]
    [MemberData(nameof(AllReasons))]
    public void ReasonCodes_From_ReturnsStableKebabCase(UnreadableProtectedDataReason reason) {
        string code = UnreadableProtectedDataReasonCodes.From(reason);

        code.ShouldNotBeNullOrWhiteSpace();
        code.ShouldBe(code.ToLowerInvariant());
        code.ShouldNotContain(" ");
    }

    [Fact]
    public void ReasonCodes_From_AllValuesUnique() {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (UnreadableProtectedDataReason reason in Enum.GetValues<UnreadableProtectedDataReason>()) {
            string code = UnreadableProtectedDataReasonCodes.From(reason);
            codes.Add(code).ShouldBeTrue($"Reason code {code} is duplicated");
        }
    }

    [Theory]
    [InlineData(UnreadableProtectedDataReason.ProviderUnavailable, true)]
    [InlineData(UnreadableProtectedDataReason.MissingKey, false)]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted, false)]
    [InlineData(UnreadableProtectedDataReason.ProviderDenied, false)]
    [InlineData(UnreadableProtectedDataReason.ConsistencyMismatch, false)]
    [InlineData(UnreadableProtectedDataReason.MalformedMetadata, false)]
    [InlineData(UnreadableProtectedDataReason.UnknownMetadataVersion, false)]
    [InlineData(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation, false)]
    [InlineData(UnreadableProtectedDataReason.BytesMetadataMismatch, false)]
    public void ReasonCodes_IsRetryable_OnlyProviderUnavailableIsRetryableByDefault(
        UnreadableProtectedDataReason reason,
        bool expectedRetryable)
        => UnreadableProtectedDataReasonCodes.IsRetryable(reason).ShouldBe(expectedRetryable);

    [Theory]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted, true)]
    [InlineData(UnreadableProtectedDataReason.MalformedMetadata, true)]
    [InlineData(UnreadableProtectedDataReason.ConsistencyMismatch, true)]
    [InlineData(UnreadableProtectedDataReason.BytesMetadataMismatch, true)]
    [InlineData(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation, true)]
    [InlineData(UnreadableProtectedDataReason.MissingKey, false)]
    [InlineData(UnreadableProtectedDataReason.ProviderUnavailable, false)]
    public void ReasonCodes_IsPermanent_ClassifiesCorrectly(
        UnreadableProtectedDataReason reason,
        bool expectedPermanent)
        => UnreadableProtectedDataReasonCodes.IsPermanent(reason).ShouldBe(expectedPermanent);

    [Fact]
    public void PayloadOutcome_Readable_HasBytesAndMetadataAndNoReason() {
        byte[] bytes = [1, 2, 3];
        var metadata = EventStorePayloadProtectionMetadata.Unprotected();

        var outcome = PayloadUnprotectionOutcome.Readable(bytes, "json", metadata);

        outcome.IsReadable.ShouldBeTrue();
        outcome.IsUnreadable.ShouldBeFalse();
        outcome.PayloadBytes.ShouldBe(bytes);
        outcome.SerializationFormat.ShouldBe("json");
        outcome.UnreadableReason.ShouldBeNull();
        outcome.Metadata.ShouldBe(metadata);
    }

    [Theory]
    [MemberData(nameof(AllReasons))]
    public void PayloadOutcome_Unreadable_HasReasonAndNoPayload(UnreadableProtectedDataReason reason) {
        var outcome = PayloadUnprotectionOutcome.Unreadable(reason);

        outcome.IsReadable.ShouldBeFalse();
        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(reason);
        outcome.PayloadBytes.ShouldBeNull();
        outcome.SerializationFormat.ShouldBeNull();
        outcome.Metadata.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        _ = outcome.Metadata.CompatibilityFlags.ShouldNotBeNull();
        outcome.Metadata.CompatibilityFlags!["reason"].ShouldBe(UnreadableProtectedDataReasonCodes.From(reason));
    }

    [Theory]
    [MemberData(nameof(AllReasons))]
    public void SnapshotOutcome_Unreadable_HasReasonAndNoState(UnreadableProtectedDataReason reason) {
        var outcome = SnapshotUnprotectionOutcome.Unreadable(reason);

        outcome.IsReadable.ShouldBeFalse();
        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(reason);
        outcome.State.ShouldBeNull();
    }

    [Fact]
    public void SnapshotOutcome_Readable_HasState() {
        var state = new { Counter = 5 };
        var metadata = EventStorePayloadProtectionMetadata.Unprotected();

        var outcome = SnapshotUnprotectionOutcome.Readable(state, metadata);

        outcome.IsReadable.ShouldBeTrue();
        outcome.State.ShouldBe(state);
    }

    [Fact]
    public void PayloadOutcome_FromResult_PromotesToReadableOutcome() {
        var result = new PayloadProtectionResult([1, 2, 3], "json", EventStorePayloadProtectionMetadata.Unprotected());

        var outcome = PayloadUnprotectionOutcome.FromResult(result);

        outcome.IsReadable.ShouldBeTrue();
        outcome.PayloadBytes.ShouldBe(result.PayloadBytes);
        outcome.SerializationFormat.ShouldBe(result.SerializationFormat);
        outcome.Metadata.ShouldBe(result.Metadata);
    }

    [Theory]
    [InlineData(UnreadableProtectedDataReason.ProviderUnavailable, 503)]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted, 410)]
    [InlineData(UnreadableProtectedDataReason.MissingKey, 422)]
    [InlineData(UnreadableProtectedDataReason.ProviderDenied, 422)]
    [InlineData(UnreadableProtectedDataReason.MalformedMetadata, 422)]
    [InlineData(UnreadableProtectedDataReason.UnknownMetadataVersion, 422)]
    [InlineData(UnreadableProtectedDataReason.ConsistencyMismatch, 422)]
    [InlineData(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation, 422)]
    [InlineData(UnreadableProtectedDataReason.BytesMetadataMismatch, 422)]
    public void UnreadableProtectedDataProblem_GetStatusCode_FollowsST0Policy(
        UnreadableProtectedDataReason reason,
        int expected)
        => Hexalith.EventStore.Contracts.Problems.UnreadableProtectedDataProblem.GetStatusCode(reason).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(AllReasons))]
    public void UnreadableProtectedDataProblem_GetSafeOperatorGuidance_ReturnsFixedSafeText(
        UnreadableProtectedDataReason reason) {
        string guidance = Hexalith.EventStore.Contracts.Problems.UnreadableProtectedDataProblem.GetSafeOperatorGuidance(reason);

        guidance.ShouldNotBeNullOrWhiteSpace();
        AssertNoForbiddenSubstrings(guidance);
    }

    [Fact]
    public void UnreadableProtectedDataProblem_TypeUri_IsStable() => Hexalith.EventStore.Contracts.Problems.UnreadableProtectedDataProblem.TypeUri
            .ShouldBe("https://hexalith.io/problems/unreadable-protected-data");

    [Fact]
    public async Task TryUnprotectEventPayloadAsync_DefaultImplementation_ReturnsReadableForNoOp() {
        IEventPayloadProtectionService service = new TestNoOpService();
        var identity = new AggregateIdentity("tenant-a", "domain-a", "agg-1");

        PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
            identity,
            "Type",
            [1, 2, 3],
            "json",
            EventStorePayloadProtectionMetadata.Unprotected(),
            CancellationToken.None);

        outcome.IsReadable.ShouldBeTrue();
        outcome.PayloadBytes.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task TryUnprotectEventPayloadAsync_DefaultImplementation_MapsNonCancellationFailureToProviderUnavailable() {
        IEventPayloadProtectionService service = new TestFailingService();
        var identity = new AggregateIdentity("tenant-a", "domain-a", "agg-1");

        PayloadUnprotectionOutcome outcome = await service.TryUnprotectEventPayloadAsync(
            identity,
            "Type",
            [1, 2, 3],
            "json",
            EventStorePayloadProtectionMetadata.Unprotected(),
            CancellationToken.None);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    [Fact]
    public async Task TryUnprotectEventPayloadAsync_DefaultImplementation_PropagatesCancellation() {
        IEventPayloadProtectionService service = new TestCancellingService();
        var identity = new AggregateIdentity("tenant-a", "domain-a", "agg-1");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await service.TryUnprotectEventPayloadAsync(
            identity,
            "Type",
            [1, 2, 3],
            "json",
            EventStorePayloadProtectionMetadata.Unprotected(),
            cts.Token));
    }

    [Fact]
    public async Task TryUnprotectSnapshotAsync_DefaultImplementation_MapsNonCancellationFailureToProviderUnavailable() {
        IEventPayloadProtectionService service = new TestFailingService();
        var identity = new AggregateIdentity("tenant-a", "domain-a", "agg-1");

        SnapshotUnprotectionOutcome outcome = await service.TryUnprotectSnapshotAsync(
            identity,
            new { value = 1 },
            EventStorePayloadProtectionMetadata.Unprotected(),
            CancellationToken.None);

        outcome.IsUnreadable.ShouldBeTrue();
        outcome.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    private static void AssertNoForbiddenSubstrings(string value) {
        string lowered = value.ToLowerInvariant();
        foreach (string forbidden in new[] { "password", "secret", "private-key", "connection-string", "plaintext", "dapr-secret", "vault-uri", "bearer " }) {
            lowered.ShouldNotContain(forbidden);
        }
    }

    private sealed class TestNoOpService : IEventPayloadProtectionService {
        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
            => Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
            => Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));

        public Task<object> ProtectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default)
            => Task.FromResult(state);

        public Task<object> UnprotectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default)
            => Task.FromResult(state);
    }

    private sealed class TestFailingService : IEventPayloadProtectionService {
        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Protection provider not configured.");

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Protection provider not configured.");

        public Task<object> ProtectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Protection provider not configured.");

        public Task<object> UnprotectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Protection provider not configured.");
    }

    private sealed class TestCancellingService : IEventPayloadProtectionService {
        public Task<PayloadProtectionResult> ProtectEventPayloadAsync(AggregateIdentity identity, IEventPayload eventPayload, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));
        }

        public Task<PayloadProtectionResult> UnprotectEventPayloadAsync(AggregateIdentity identity, string eventTypeName, byte[] payloadBytes, string serializationFormat, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PayloadProtectionResult(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()));
        }

        public Task<object> ProtectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(state);
        }

        public Task<object> UnprotectSnapshotStateAsync(AggregateIdentity identity, object state, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(state);
        }
    }
}
