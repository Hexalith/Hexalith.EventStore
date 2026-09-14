using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Optional infrastructure hook for protecting event payloads and snapshot state before storage,
/// and unprotecting them before publication or replay.
/// </summary>
public interface IEventPayloadProtectionService {
    /// <summary>
    /// Applies optional protection to an event payload before it is persisted.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventPayload">The domain event payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The serialized payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="PayloadProtectionResult"/> with transformed bytes, format, and metadata.</returns>
    Task<PayloadProtectionResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies optional protection with the authenticated occurrence required by <c>pdenc-v2</c>.
    /// The default delegates only non-v2 behavior to the legacy protect member and never fabricates
    /// a completion context. Implements Story 8.1 sections 9 and 10.3 under normative digest
    /// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventPayload">The domain event payload.</param>
    /// <param name="eventTypeName">The fully-qualified persisted event type name.</param>
    /// <param name="payloadBytes">The serialized payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="occurrenceContext">The trusted aggregate-local occurrence context.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The transformed payload and optional v2 completion context.</returns>
    async Task<PayloadProtectionWriteResult> ProtectEventPayloadAsync(
        AggregateIdentity identity,
        IEventPayload eventPayload,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        PayloadProtectionOccurrenceContext occurrenceContext,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ValidateOccurrenceContext(occurrenceContext, PayloadProtectionPayloadKind.Event, eventTypeName);

        PayloadProtectionResult result = await ProtectEventPayloadAsync(
            identity,
            eventPayload,
            eventTypeName,
            payloadBytes,
            serializationFormat,
            cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(result);

        if (IsV2Format(result.SerializationFormat) || IsV2Metadata(result.Metadata)) {
            throw CreateUnsupportedV2Exception();
        }

        return new PayloadProtectionWriteResult(result, CompletionContext: null);
    }

    /// <summary>
    /// Removes optional protection from an event payload before it is published or replayed.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The protected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="PayloadProtectionResult"/> with transformed bytes, format, and metadata.</returns>
    Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes optional protection from an event payload before it is published or replayed, using
    /// the persisted metadata recorded alongside the payload. The default implementation delegates
    /// to the legacy event unprotect method for backward compatibility with providers that do not
    /// need metadata.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The protected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="metadata">The protection metadata persisted alongside the payload.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="PayloadProtectionResult"/> with transformed bytes, format, and metadata.</returns>
    Task<PayloadProtectionResult> UnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => UnprotectEventPayloadAsync(identity, eventTypeName, payloadBytes, serializationFormat, cancellationToken);

    /// <summary>
    /// Applies optional protection to snapshot state before it is written to storage.
    /// Object-based legacy entry point kept for backward compatibility; new callers should use
    /// <see cref="ProtectSnapshotAsync(AggregateIdentity, object, CancellationToken)"/> so state and metadata travel together.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The snapshot state object.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The (possibly protected) snapshot state object.</returns>
    Task<object> ProtectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes optional protection from snapshot state after it is loaded from storage.
    /// Object-based legacy entry point kept for backward compatibility; new callers should use
    /// <see cref="UnprotectSnapshotAsync"/> so the protection metadata recorded at write time can
    /// be passed back to the provider.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The (possibly protected) snapshot state object.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The unprotected snapshot state object.</returns>
    Task<object> UnprotectSnapshotStateAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Typed snapshot protection entry point. Returns the snapshot state together with the
    /// protection metadata that describes how it was produced. The default implementation
    /// delegates to <see cref="ProtectSnapshotStateAsync"/> and wraps the result in
    /// <see cref="PayloadProtectionState.Unprotected"/> metadata for backward compatibility with
    /// implementers that have not yet migrated.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The snapshot state object.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A <see cref="SnapshotProtectionResult"/> with state and metadata.</returns>
    async Task<SnapshotProtectionResult> ProtectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        CancellationToken cancellationToken = default) {
        object protectedState = await ProtectSnapshotStateAsync(identity, state, cancellationToken).ConfigureAwait(false);
        return new SnapshotProtectionResult(protectedState, EventStorePayloadProtectionMetadata.Unprotected());
    }

    /// <summary>
    /// Applies optional snapshot protection with exact source-generated type and occurrence context.
    /// The default delegates only non-v2 behavior to the existing typed snapshot member and never
    /// fabricates a completion context. Implements Story 8.1 sections 6, 9, and 10.3 under normative
    /// digest <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The snapshot state object.</param>
    /// <param name="stateTypeInfo">The exact source-generated JSON type information.</param>
    /// <param name="occurrenceContext">The trusted snapshot occurrence and stable type identifier.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The transformed snapshot and optional v2 completion context.</returns>
    async Task<SnapshotProtectionWriteResult> ProtectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        JsonTypeInfo stateTypeInfo,
        PayloadProtectionOccurrenceContext occurrenceContext,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(stateTypeInfo);
        ValidateOccurrenceContext(occurrenceContext, PayloadProtectionPayloadKind.Snapshot);

        SnapshotProtectionResult result = await ProtectSnapshotAsync(identity, state, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(result);

        if (result.State is ProtectedSnapshotPayloadV2 || IsV2Metadata(result.Metadata)) {
            throw CreateUnsupportedV2Exception();
        }

        return new SnapshotProtectionWriteResult(result, CompletionContext: null);
    }

    /// <summary>
    /// Typed snapshot unprotection entry point. Accepts the protection metadata recorded at
    /// write time so providers can route to the correct key or scheme. The default
    /// implementation delegates to <see cref="UnprotectSnapshotStateAsync"/> and ignores the
    /// metadata for backward compatibility.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The (possibly protected) snapshot state object.</param>
    /// <param name="metadata">The protection metadata persisted alongside the snapshot, or <see langword="null"/> for legacy records.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The unprotected snapshot state object.</returns>
    Task<object> UnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default)
        => UnprotectSnapshotStateAsync(identity, state, cancellationToken);

    /// <summary>
    /// Story 22.7b — typed unprotection that returns a <see cref="PayloadUnprotectionOutcome"/>
    /// classifying the result as readable or unreadable. Providers that need to signal "missing
    /// key", "provider unavailable", "consistency mismatch" or other unreadable conditions MUST
    /// use this entry point: exceptions are reserved for cancellation or unexpected infrastructure
    /// failure and must not be parsed for public unreadable-data classification. The default
    /// implementation delegates to the metadata-aware <see cref="UnprotectEventPayloadAsync(AggregateIdentity, string, byte[], string, EventStorePayloadProtectionMetadata?, CancellationToken)"/>
    /// overload and reports any non-cancellation exception as a generic provider-unavailable
    /// readable failure mapped to <see cref="UnreadableProtectedDataReason.ProviderUnavailable"/>.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventTypeName">The fully-qualified event type name.</param>
    /// <param name="payloadBytes">The protected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="metadata">The protection metadata persisted alongside the payload.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A typed <see cref="PayloadUnprotectionOutcome"/>.</returns>
    async Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        try {
            PayloadProtectionResult result = await UnprotectEventPayloadAsync(
                identity,
                eventTypeName,
                payloadBytes,
                serializationFormat,
                metadata,
                cancellationToken).ConfigureAwait(false);
            return PayloadUnprotectionOutcome.FromResult(result);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            // Default implementation is conservative: any non-cancellation failure becomes a
            // transient provider-unavailable outcome. Custom providers should override this
            // entry point and return the precise unreadable category they observe.
            return PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata);
        }
    }

    /// <summary>
    /// Attempts event unprotection with the authenticated occurrence required by <c>pdenc-v2</c>.
    /// The default returns the existing typed unsupported outcome for v2 and delegates other formats
    /// without dropping cancellation. Implements Story 8.1 sections 9 and 12 under normative digest
    /// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="eventTypeName">The fully-qualified persisted event type name.</param>
    /// <param name="payloadBytes">The protected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format identifier.</param>
    /// <param name="metadata">The persisted provider-neutral protection metadata.</param>
    /// <param name="occurrenceContext">The trusted aggregate-local occurrence context.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A typed readable or unreadable outcome.</returns>
    Task<PayloadUnprotectionOutcome> TryUnprotectEventPayloadAsync(
        AggregateIdentity identity,
        string eventTypeName,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata? metadata,
        PayloadProtectionOccurrenceContext occurrenceContext,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);
        ValidateOccurrenceContext(occurrenceContext, PayloadProtectionPayloadKind.Event, eventTypeName);

        return IsV2Format(serializationFormat) || IsV2Metadata(metadata)
            ? Task.FromResult(PayloadUnprotectionOutcome.Unreadable(
                UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation,
                metadata))
            : TryUnprotectEventPayloadAsync(
                identity,
                eventTypeName,
                payloadBytes,
                serializationFormat,
                metadata,
                cancellationToken);
    }

    /// <summary>
    /// Story 22.7b — typed snapshot unprotection entry point with explicit unreadable handling.
    /// Mirrors <see cref="TryUnprotectEventPayloadAsync(AggregateIdentity, string, byte[], string, EventStorePayloadProtectionMetadata?, CancellationToken)"/>
    /// for snapshot state.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The (possibly protected) snapshot state object.</param>
    /// <param name="metadata">The protection metadata persisted alongside the snapshot, or <see langword="null"/> for legacy records.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A typed <see cref="SnapshotUnprotectionOutcome"/>.</returns>
    async Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        CancellationToken cancellationToken = default) {
        try {
            object unprotected = await UnprotectSnapshotAsync(identity, state, metadata, cancellationToken)
                .ConfigureAwait(false);
            return SnapshotUnprotectionOutcome.Readable(unprotected, metadata ?? EventStorePayloadProtectionMetadata.Unprotected());
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch {
            return SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata);
        }
    }

    /// <summary>
    /// Attempts snapshot unprotection with exact source-generated type and occurrence context.
    /// The default returns the existing typed unsupported outcome for v2 and delegates other states
    /// without dropping cancellation. Implements Story 8.1 sections 9 and 12 under normative digest
    /// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
    /// </summary>
    /// <param name="identity">The aggregate identity owning the snapshot.</param>
    /// <param name="state">The protected or unprotected snapshot state.</param>
    /// <param name="metadata">The persisted provider-neutral protection metadata.</param>
    /// <param name="stateTypeInfo">The exact source-generated JSON type information.</param>
    /// <param name="occurrenceContext">The trusted snapshot occurrence and stable type identifier.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A typed readable or unreadable snapshot outcome.</returns>
    Task<SnapshotUnprotectionOutcome> TryUnprotectSnapshotAsync(
        AggregateIdentity identity,
        object state,
        EventStorePayloadProtectionMetadata? metadata,
        JsonTypeInfo stateTypeInfo,
        PayloadProtectionOccurrenceContext occurrenceContext,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(stateTypeInfo);
        ValidateOccurrenceContext(occurrenceContext, PayloadProtectionPayloadKind.Snapshot);

        return state is ProtectedSnapshotPayloadV2 || IsV2Metadata(metadata)
            ? Task.FromResult(SnapshotUnprotectionOutcome.Unreadable(
                UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation,
                metadata))
            : TryUnprotectSnapshotAsync(identity, state, metadata, cancellationToken);
    }

    /// <summary>
    /// Acquires the exact writer-completion lease immediately before actor persistence.
    /// Existing providers fault explicitly because they never produce a v2 completion context.
    /// Implements Story 8.1 sections 9 and 10.3 under normative digest
    /// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
    /// </summary>
    /// <param name="completionContext">The exact reserved-write completion context.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A task that completes after lease acquisition.</returns>
    Task AcquirePayloadProtectionCompletionLeaseAsync(
        PayloadProtectionCompletionContext completionContext,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCompletionContext(completionContext);
        return Task.FromException(CreateUnsupportedCompletionException());
    }

    /// <summary>
    /// Completes or reconciles the exact reserved write after actor persistence is observed.
    /// Existing providers fault explicitly because they never produce a v2 completion context.
    /// Implements Story 8.1 sections 9 and 10.3 under normative digest
    /// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
    /// </summary>
    /// <param name="completionContext">The exact reserved-write completion context.</param>
    /// <param name="persistenceOutcome">Whether persistence is confirmed durable, absent, or ambiguous.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A task that completes after activation, release, or reconciliation scheduling.</returns>
    Task CompletePayloadProtectionAsync(
        PayloadProtectionCompletionContext completionContext,
        PayloadProtectionPersistenceOutcome persistenceOutcome,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCompletionContext(completionContext);
        if (persistenceOutcome is not PayloadProtectionPersistenceOutcome.Persisted
            and not PayloadProtectionPersistenceOutcome.NotPersisted
            and not PayloadProtectionPersistenceOutcome.Unknown) {
            throw new ArgumentOutOfRangeException(nameof(persistenceOutcome));
        }

        return Task.FromException(CreateUnsupportedCompletionException());
    }

    private static NotSupportedException CreateUnsupportedCompletionException()
        => new("The payload-protection provider does not support persistence completion.");

    private static NotSupportedException CreateUnsupportedV2Exception()
        => new("The legacy payload-protection adapter cannot process pdenc-v2.");

    private static bool IsCanonicalUlid(string? value) {
        if (value is null || value.Length != 26) {
            return false;
        }

        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        return value[0] is >= '0' and <= '7'
            && value.All(static character => alphabet.Contains(character));
    }

    private static bool IsV2Format(string? serializationFormat)
        => string.Equals(serializationFormat, "json+pdenc-v2", StringComparison.Ordinal);

    private static bool IsV2Metadata(EventStorePayloadProtectionMetadata? metadata)
        => string.Equals(metadata?.Scheme, "hexalith-pdenc-v2", StringComparison.Ordinal);

    private static void ValidateCompletionContext(PayloadProtectionCompletionContext completionContext) {
        ArgumentNullException.ThrowIfNull(completionContext);
        ArgumentNullException.ThrowIfNull(completionContext.Identity);
        ArgumentNullException.ThrowIfNull(completionContext.Occurrence);
        ValidateOccurrenceContext(completionContext.Occurrence, completionContext.Occurrence.PayloadKind);

        if (!IsCanonicalUlid(completionContext.OperationId)) {
            throw new ArgumentException("The operation identifier must be a canonical uppercase ULID.", nameof(completionContext));
        }

        if (!IsCanonicalUlid(completionContext.KeyReference)) {
            throw new ArgumentException("The key reference must be a canonical uppercase ULID.", nameof(completionContext));
        }

        if (completionContext.DekVersion == 0) {
            throw new ArgumentOutOfRangeException(nameof(completionContext));
        }
    }

    private static void ValidateOccurrenceContext(
        PayloadProtectionOccurrenceContext occurrenceContext,
        PayloadProtectionPayloadKind expectedKind,
        string? expectedTypeId = null) {
        ArgumentNullException.ThrowIfNull(occurrenceContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceContext.PayloadTypeId);

        if (expectedKind is not PayloadProtectionPayloadKind.Event and not PayloadProtectionPayloadKind.Snapshot) {
            throw new ArgumentOutOfRangeException(nameof(expectedKind));
        }

        if (occurrenceContext.PayloadKind != expectedKind) {
            throw new ArgumentException("The payload-protection occurrence kind does not match the operation.", nameof(occurrenceContext));
        }

        if (expectedTypeId is not null
            && !string.Equals(occurrenceContext.PayloadTypeId, expectedTypeId, StringComparison.Ordinal)) {
            throw new ArgumentException("The payload-protection occurrence type does not match the persisted type.", nameof(occurrenceContext));
        }
    }
}
