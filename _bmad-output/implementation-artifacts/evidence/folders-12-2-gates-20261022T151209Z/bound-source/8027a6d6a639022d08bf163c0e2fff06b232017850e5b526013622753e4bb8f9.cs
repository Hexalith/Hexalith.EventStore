namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Provides the EventStore-owned fail-safe policy for projection lifecycle evidence and compatibility views.
/// </summary>
public static class ProjectionLifecyclePolicy
{
    /// <summary>
    /// The canonical HTTP response header carrying authoritative projection lifecycle evidence.
    /// </summary>
    public const string HeaderName = "X-Hexalith-Projection-Lifecycle";

    /// <summary>
    /// Normalizes lifecycle evidence against its authoritative query-route provenance.
    /// </summary>
    /// <param name="lifecycle">The producer-supplied lifecycle value.</param>
    /// <param name="provenance">The route provenance established by EventStore.</param>
    /// <returns>
    /// The supplied known lifecycle only for projection-backed provenance; otherwise
    /// <see cref="ProjectionLifecycleState.Unknown"/>.
    /// </returns>
    public static ProjectionLifecycleState Normalize(
        ProjectionLifecycleState lifecycle,
        QueryResponseProvenance provenance)
        => provenance == QueryResponseProvenance.ProjectionBacked && IsKnown(lifecycle)
            ? lifecycle
            : ProjectionLifecycleState.Unknown;

    /// <summary>
    /// Projects lifecycle evidence into the legacy stale compatibility field.
    /// </summary>
    /// <param name="lifecycle">The already normalized lifecycle value.</param>
    /// <param name="unknownFallback">The legacy value retained only when lifecycle is unknown.</param>
    /// <returns>
    /// <see langword="false"/> for current, <see langword="true"/> for stale, the supplied fallback for
    /// unknown, and <see langword="null"/> for lifecycle states that cannot be represented safely as stale/current.
    /// </returns>
    public static bool? ProjectIsStale(ProjectionLifecycleState lifecycle, bool? unknownFallback = null)
        => lifecycle switch
        {
            ProjectionLifecycleState.Current => false,
            ProjectionLifecycleState.Stale => true,
            ProjectionLifecycleState.Unknown => unknownFallback,
            _ => null,
        };

    /// <summary>
    /// Projects lifecycle evidence into the legacy degraded compatibility field.
    /// </summary>
    /// <param name="lifecycle">The already normalized lifecycle value.</param>
    /// <param name="unknownFallback">The existing additive degraded value retained unless lifecycle is degraded.</param>
    /// <returns>
    /// <see langword="true"/> for degraded and the supplied additive fallback for every other lifecycle.
    /// </returns>
    public static bool? ProjectIsDegraded(ProjectionLifecycleState lifecycle, bool? unknownFallback = null)
        => lifecycle switch
        {
            ProjectionLifecycleState.Degraded => true,
            _ => unknownFallback,
        };

    /// <summary>
    /// Determines whether the metadata constitutes projection-confirmed current evidence.
    /// </summary>
    /// <param name="provenance">The authoritative route provenance.</param>
    /// <param name="lifecycle">The lifecycle evidence.</param>
    /// <returns><see langword="true"/> only for projection-backed current evidence.</returns>
    public static bool IsProjectionConfirmed(
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle)
        => provenance == QueryResponseProvenance.ProjectionBacked
            && lifecycle == ProjectionLifecycleState.Current;

    /// <summary>
    /// Applies the default consumer mutation policy.
    /// </summary>
    /// <param name="isAuthorized">Whether the caller is otherwise authorized to mutate.</param>
    /// <param name="provenance">The authoritative route provenance.</param>
    /// <param name="lifecycle">The lifecycle evidence.</param>
    /// <returns>
    /// <see langword="true"/> only when authorization succeeds and the evidence is projection-backed current.
    /// </returns>
    public static bool CanMutate(
        bool isAuthorized,
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle)
        => isAuthorized && IsProjectionConfirmed(provenance, lifecycle);

    private static bool IsKnown(ProjectionLifecycleState lifecycle)
        => lifecycle is >= ProjectionLifecycleState.Unknown and <= ProjectionLifecycleState.LocalOnly;
}
