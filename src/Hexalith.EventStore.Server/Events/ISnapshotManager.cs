
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Manages aggregate state snapshots for optimizing state rehydration.
/// Snapshots are an advisory optimization -- failures never block command processing (rule #12).
/// </summary>
public interface ISnapshotManager {
    /// <summary>
    /// Determines whether a snapshot should be created based on the configured interval.
    /// Uses four-tier resolution: persisted policy > tenant-domain override > domain override > system default.
    /// </summary>
    /// <param name="tenantId">The tenant ID (used with domain for per-tenant-domain interval overrides).</param>
    /// <param name="domain">The domain name (used to resolve per-domain interval overrides).</param>
    /// <param name="aggregateType">The aggregate type used for exact persisted policy lookup.</param>
    /// <param name="currentSequence">The current event sequence number after persistence.</param>
    /// <param name="lastSnapshotSequence">The sequence number of the last snapshot (0 if none).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if a snapshot should be created; otherwise <c>false</c>.</returns>
    Task<bool> ShouldCreateSnapshotAsync(string tenantId, string domain, string aggregateType, long currentSequence, long lastSnapshotSequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a snapshot should be created based on static tenant/domain options.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="currentSequence">The current event sequence number after persistence.</param>
    /// <param name="lastSnapshotSequence">The sequence number of the last snapshot (0 if none).</param>
    /// <returns><c>true</c> if a snapshot should be created; otherwise <c>false</c>.</returns>
    Task<bool> ShouldCreateSnapshotAsync(string tenantId, string domain, long currentSequence, long lastSnapshotSequence);

    /// <summary>
    /// Creates a snapshot by staging it via IActorStateManager.SetStateAsync.
    /// Does NOT call SaveStateAsync -- the caller commits atomically (D1).
    /// On failure, logs a warning and returns without throwing (advisory per rule #12).
    /// </summary>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <param name="sequenceNumber">The event sequence number this snapshot represents.</param>
    /// <param name="state">The aggregate state to snapshot (domain-specific, opaque to EventStore).</param>
    /// <param name="stateManager">The actor state manager for staging the snapshot write.</param>
    /// <param name="correlationId">The correlation ID for structured logging (rule #9). Optional.</param>
    /// <param name="cancellationToken">The caller's cancellation token. Forwarded to the snapshot protection hook.</param>
    /// <param name="throwOnFailure">When true, snapshot failures are rethrown instead of handled as advisory warnings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateSnapshotAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false);

    /// <summary>
    /// Loads an existing snapshot for an aggregate.
    /// Returns null if no snapshot exists or if deserialization fails (graceful degradation).
    /// On deserialization failure, deletes the corrupt snapshot and logs a warning.
    /// </summary>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <param name="stateManager">The actor state manager for reading the snapshot.</param>
    /// <param name="correlationId">The correlation ID for structured logging (rule #9). Optional.</param>
    /// <param name="cancellationToken">The caller's cancellation token. Forwarded to the snapshot protection hook.</param>
    /// <returns>The snapshot record, or null if no valid snapshot exists.</returns>
    Task<SnapshotRecord?> LoadSnapshotAsync(AggregateIdentity identity, IActorStateManager stateManager, string? correlationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspects the snapshot key with a distinguishable load outcome suitable for fail-closed
    /// callers such as the DW16 manual snapshot overwrite path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="LoadSnapshotAsync"/>, this method does NOT delete corrupt snapshots and
    /// does NOT collapse unreadable protected/provider-opaque snapshots into <c>null</c>. The
    /// caller can decide to fail closed (and refuse to overwrite) on
    /// <see cref="SnapshotLoadOutcome.UnreadableProtected"/>, <see cref="SnapshotLoadOutcome.ProviderOpaque"/>,
    /// or <see cref="SnapshotLoadOutcome.Corrupt"/>.
    /// </para>
    /// </remarks>
    /// <param name="identity">The aggregate identity providing key derivation.</param>
    /// <param name="stateManager">The actor state manager.</param>
    /// <param name="correlationId">Optional correlation id for structured logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A typed load outcome with the snapshot (when Readable) or a safe reason code (otherwise).</returns>
    Task<SnapshotLoadResult> InspectSnapshotForManualOverwriteAsync(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
