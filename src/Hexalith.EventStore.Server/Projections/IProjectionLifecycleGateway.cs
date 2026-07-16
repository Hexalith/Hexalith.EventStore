using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Mockable seam over the <see cref="ProjectionLifecycleActor"/> that composes the per
/// (tenant, domain, aggregate, projection) actor id and invokes the actor via the weak/JSON
/// DAPR proxy path. Kept internal so the actor + its contracts remain the only public surface.
/// </summary>
internal interface IProjectionLifecycleGateway {
    /// <summary>Begins or resumes a persisted rebuild lifecycle operation.</summary>
    Task<bool> BeginRebuildAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>Completes the matching persisted rebuild lifecycle operation.</summary>
    Task<bool> CompleteRebuildAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the persisted lifecycle phase for projection-backed query evidence.</summary>
    Task<ProjectionLifecyclePhase> ReadPhaseAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default);

    /// <summary>Reads phase and revision for a version-coherent projection query.</summary>
    Task<ProjectionLifecycleSnapshot> ReadSnapshotAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default);

    /// <summary>Acquires a persisted delivery lease held through the complete projection write.</summary>
    Task<bool> BeginDeliveryWriteAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>Releases the matching persisted delivery lease.</summary>
    Task<bool> CompleteDeliveryWriteAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports whether a projection delivery write is admitted for the given projection scope.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (fourth key segment).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True when the delivery write may proceed; false while a rebuild or erase is in progress.</returns>
    Task<bool> TryAdmitDeliveryWriteAsync(AggregateIdentity identity, string projectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins (or resumes) an erase operation for the given projection scope.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (fourth key segment).</param>
    /// <param name="operationId">The erase operation identifier.</param>
    /// <param name="manifestDigest">The erase target manifest digest.</param>
    /// <param name="allowBegin">
    /// Whether an idle lifecycle scope may begin erasing. A matching in-flight operation may still resume.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The admission decision.</returns>
    Task<ProjectionEraseAdmission> BeginEraseAsync(
        AggregateIdentity identity,
        string projectionName,
        string operationId,
        string manifestDigest,
        bool allowBegin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the erase outcome for a single target within the in-flight operation.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (fourth key segment).</param>
    /// <param name="operationId">The erase operation identifier.</param>
    /// <param name="targetKey">The target being erased.</param>
    /// <param name="outcome">The outcome recorded for the target.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True when the outcome was recorded; false when no matching operation is in progress.</returns>
    Task<bool> RecordTargetOutcomeAsync(AggregateIdentity identity, string projectionName, string operationId, string targetKey, string outcome, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the in-flight erase operation for the given projection scope.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (fourth key segment).</param>
    /// <param name="operationId">The erase operation identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True when the operation was completed; false when no matching operation is in progress.</returns>
    Task<bool> CompleteEraseAsync(AggregateIdentity identity, string projectionName, string operationId, CancellationToken cancellationToken = default);
}
