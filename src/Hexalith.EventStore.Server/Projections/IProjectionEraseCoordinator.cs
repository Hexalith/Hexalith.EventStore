namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// The classified outcome of a coordinated projection erase operation.
/// </summary>
public enum ProjectionEraseOutcomeKind {
    /// <summary>Every target was erased (or was already absent) and verified absent; the operation completed.</summary>
    Success,

    /// <summary>The request was denied before any mutation (e.g. authorization/policy).</summary>
    Denied,

    /// <summary>A required erase capability has not opted in, or the target set is not erasable.</summary>
    Unsupported,

    /// <summary>An operator rebuild is (or may be) active for the domain; erasure is refused.</summary>
    ActiveRebuild,

    /// <summary>A different active erase operation, or a newer concurrent value, blocked the erase.</summary>
    Conflict,

    /// <summary>Some target could not be confirmed erased; the same operationId can resume.</summary>
    Incomplete,

    /// <summary>The operation was canceled.</summary>
    Canceled,

    /// <summary>The outcome could not be classified after a read-back failure; the same operationId can resume.</summary>
    Unknown,
}

/// <summary>The recorded outcome for a single erase target.</summary>
/// <param name="TargetKey">The canonical, value-free key of the target.</param>
/// <param name="Outcome">One of <c>Complete</c>, <c>Conflict</c>, <c>Incomplete</c>, or <c>Unknown</c>.</param>
public sealed record ProjectionEraseTargetOutcome(string TargetKey, string Outcome);

/// <summary>The structured result of a coordinated projection erase operation.</summary>
/// <param name="Kind">The classified outcome kind.</param>
/// <param name="ReasonCode">An optional, support-safe reason code; never discloses target values.</param>
/// <param name="TargetOutcomes">The per-target outcomes recorded during the operation.</param>
public sealed record ProjectionEraseResult(
    ProjectionEraseOutcomeKind Kind,
    string? ReasonCode,
    IReadOnlyList<ProjectionEraseTargetOutcome> TargetOutcomes) {
    /// <summary>Creates a result with the supplied kind, optional reason code, and optional target outcomes.</summary>
    /// <param name="kind">The classified outcome kind.</param>
    /// <param name="reasonCode">An optional support-safe reason code.</param>
    /// <param name="outcomes">The per-target outcomes, or an empty list when omitted.</param>
    /// <returns>The structured result.</returns>
    public static ProjectionEraseResult Of(
        ProjectionEraseOutcomeKind kind,
        string? reasonCode = null,
        IReadOnlyList<ProjectionEraseTargetOutcome>? outcomes = null) => new(kind, reasonCode, outcomes ?? []);
}

/// <summary>
/// A coordinated projection erase request. Callers supply only the projection scope, the logical slot
/// identifiers, and a stable operation identifier — never store names, keys, or ETags, which are resolved
/// canonically by the platform.
/// </summary>
/// <param name="TenantId">The owning tenant identifier.</param>
/// <param name="Domain">The owning domain.</param>
/// <param name="AggregateId">The owning aggregate identifier.</param>
/// <param name="ProjectionName">The projection name.</param>
/// <param name="Slots">The logical, aggregate-owned read-model slot identifiers to erase.</param>
/// <param name="OperationId">The stable erase operation identifier (idempotency/resume key).</param>
public sealed record ProjectionEraseRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    string ProjectionName,
    IReadOnlyList<string> Slots,
    string OperationId);

/// <summary>
/// Coordinates a resumable, structured-outcome projection erasure across the canonical read-model targets,
/// the aggregate-specific rebuild checkpoint row, and the projection-scoped delivery checkpoint.
/// </summary>
public interface IProjectionEraseCoordinator {
    /// <summary>
    /// Erases the projection targets described by <paramref name="request"/>, validating everything before
    /// any mutation, running through the persisted lifecycle actor, and classifying each target via an
    /// internal ETag read-back. The operation never uses transactions and is safe to resume with the same
    /// operation identifier.
    /// </summary>
    /// <param name="request">The coordinated erase request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The structured erase result.</returns>
    Task<ProjectionEraseResult> EraseAsync(ProjectionEraseRequest request, CancellationToken cancellationToken = default);
}
