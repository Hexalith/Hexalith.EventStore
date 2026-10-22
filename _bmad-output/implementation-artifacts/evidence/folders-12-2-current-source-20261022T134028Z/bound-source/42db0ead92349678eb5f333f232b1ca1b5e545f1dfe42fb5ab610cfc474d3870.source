namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Typed result returned by metadata-aware snapshot unprotect entry points. Mirrors
/// <see cref="PayloadUnprotectionOutcome"/> so callers handle event and snapshot unreadable cases
/// uniformly.
/// </summary>
/// <param name="State">The unprotected snapshot state when <see cref="IsReadable"/>; otherwise <see langword="null"/>.</param>
/// <param name="Metadata">Provider-neutral protection metadata associated with the outcome.</param>
/// <param name="UnreadableReason">When the outcome is unreadable, the safe reason category; otherwise <see langword="null"/>.</param>
public sealed record SnapshotUnprotectionOutcome(
    object? State,
    EventStorePayloadProtectionMetadata Metadata,
    UnreadableProtectedDataReason? UnreadableReason) {
    /// <summary>Gets a value indicating whether the outcome carries readable state.</summary>
    public bool IsReadable => UnreadableReason is null;

    /// <summary>Gets a value indicating whether the outcome is unreadable.</summary>
    public bool IsUnreadable => UnreadableReason is not null;

    /// <summary>
    /// Creates a readable snapshot outcome.
    /// </summary>
    /// <param name="state">The unprotected snapshot state.</param>
    /// <param name="metadata">Provider-neutral protection metadata.</param>
    /// <returns>A readable outcome record.</returns>
    public static SnapshotUnprotectionOutcome Readable(
        object state,
        EventStorePayloadProtectionMetadata metadata) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(metadata);
        return new SnapshotUnprotectionOutcome(state, metadata, UnreadableReason: null);
    }

    /// <summary>
    /// Creates an unreadable snapshot outcome. When <paramref name="metadata"/> is not supplied a
    /// <see cref="PayloadProtectionState.ProviderOpaque"/> record with the reason code is used.
    /// </summary>
    /// <param name="reason">The unreadable reason category.</param>
    /// <param name="metadata">Optional metadata recorded alongside the unreadable outcome.</param>
    /// <returns>An unreadable outcome record.</returns>
    public static SnapshotUnprotectionOutcome Unreadable(
        UnreadableProtectedDataReason reason,
        EventStorePayloadProtectionMetadata? metadata = null)
        => new(
            State: null,
            Metadata: metadata ?? EventStorePayloadProtectionMetadata.ProviderOpaque(UnreadableProtectedDataReasonCodes.From(reason)),
            UnreadableReason: reason);
}
