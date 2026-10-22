namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Runs operator-triggered projection rebuild work and advances rebuild checkpoints only after accepted projection applies.
/// </summary>
public interface IProjectionRebuildOrchestrator {
    /// <summary>
    /// Rebuilds the projection scope by applying tracked aggregate streams through the domain projection path.
    /// </summary>
    /// <param name="scope">The rebuild checkpoint scope.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <b>Caller precondition (P-DEC4-7P):</b> the caller must persist the initial state row
    /// (typically via <c>AdminProjectionRebuildController</c> writing <see cref="ProjectionRebuildStatus.Running"/>
    /// through <see cref="IProjectionRebuildCheckpointStore.SaveAsync"/>) BEFORE invoking the orchestrator.
    /// The orchestrator's initial <see cref="IProjectionRebuildCheckpointStore.ReadAsync"/> assumes the row exists;
    /// a transient store failure on that read writes a <see cref="ProjectionRebuildStatus.Failed"/> audit row.
    /// Direct invokers without that precondition (future schedulers, integration tests) will see a Failed audit row
    /// for a rebuild that never started, plus a potential orphan in the active-rebuilds index.
    /// </para>
    /// <para>
    /// <b>Tenant validation precondition (P46-7P):</b> tenant/RBAC validation runs once at the controller entry
    /// (e.g., <c>AdminProjectionRebuildController</c>); the orchestrator does NOT re-validate tenant or RBAC per
    /// page or per aggregate. Acceptable under the current GlobalAdministrator-only operator-rebuild gate (D2b);
    /// any future change permitting tenant-scoped operator rebuild MUST add per-iteration tenant re-validation.
    /// </para>
    /// <para>
    /// <b>Operator-scope LastAppliedSequence semantics (P-DEC3-8P):</b> for domain-wide rebuilds
    /// (<c>scope.AggregateId is null</c>), the operator-scope checkpoint row's <c>LastAppliedSequence</c>
    /// is intentionally written as <c>0</c> after <see cref="ProjectionRebuildStatus.Succeeded"/>. Per-aggregate
    /// rows carry truthful per-aggregate progress because aggregate sequence spaces are heterogeneous and a
    /// cross-aggregate <c>Math.Max</c> would inflate the operator-scope value to an artifact of the largest
    /// aggregate's space (e.g., domain covering aggregates A {0..100} and B {0..1000} would otherwise report
    /// operator-scope LastAppliedSequence=1000 which is not meaningful domain-wide progress). Admin/CLI/MCP
    /// callers querying domain-wide rebuild status should treat operator-scope <c>LastAppliedSequence</c> as
    /// "rebuild completed" rather than a progress indicator; per-aggregate rows are the source of truth.
    /// Aggregate-scoped rebuilds (<c>scope.AggregateId is not null</c>) write the actual applied sequence.
    /// </para>
    /// </remarks>
    Task RebuildProjectionAsync(ProjectionRebuildCheckpointScope scope, CancellationToken cancellationToken = default);
}
