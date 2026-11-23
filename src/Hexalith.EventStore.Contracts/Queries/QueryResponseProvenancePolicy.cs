namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>Defines the canonical query provenance names used on the wire.</summary>
public static class QueryResponseProvenancePolicy
{
    /// <summary>The response header that carries query provenance.</summary>
    public const string HeaderName = "X-Hexalith-Query-Provenance";

    /// <summary>Formats a provenance value, using <c>Unknown</c> for undefined values.</summary>
    /// <param name="provenance">The provenance to format.</param>
    /// <returns>The canonical wire name.</returns>
    public static string Format(QueryResponseProvenance provenance)
        => Normalize(provenance).ToString();

    /// <summary>Restricts a provenance value to the defined platform states.</summary>
    /// <param name="provenance">The provenance to validate.</param>
    /// <returns>The supported provenance or the safe default.</returns>
    public static QueryResponseProvenance Normalize(QueryResponseProvenance provenance)
        => provenance is QueryResponseProvenance.ProjectionBacked or QueryResponseProvenance.HandlerComputed
            ? provenance
            : QueryResponseProvenance.Unknown;

    /// <summary>Applies resolved query provenance and lifecycle to response metadata.</summary>
    /// <param name="metadata">The original response metadata, if present.</param>
    /// <param name="provenance">The resolved response provenance.</param>
    /// <param name="lifecycle">The resolved projection lifecycle.</param>
    /// <returns>Metadata with projection evidence restricted to projection-backed responses.</returns>
    public static QueryResponseMetadata NormalizeMetadata(
        QueryResponseMetadata? metadata,
        QueryResponseProvenance provenance,
        ProjectionLifecycleState lifecycle)
    {
        provenance = Normalize(provenance);
        lifecycle = ProjectionLifecyclePolicy.Normalize(lifecycle, provenance);
        bool projectionBacked = provenance == QueryResponseProvenance.ProjectionBacked;
        var source = metadata ?? new QueryResponseMetadata();
        return source with
        {
            Provenance = provenance,
            Lifecycle = lifecycle,
            ETag = projectionBacked ? source.ETag : null,
            IsNotModified = projectionBacked ? source.IsNotModified : null,
            IsStale = projectionBacked ? ProjectionLifecyclePolicy.ProjectIsStale(lifecycle, source.IsStale) : null,
            IsDegraded = ProjectionLifecyclePolicy.ProjectIsDegraded(lifecycle, source.IsDegraded),
            ProjectionVersion = projectionBacked ? source.ProjectionVersion : null,
        };
    }

    /// <summary>Reads an exact wire name and maps any unrecognized value to <c>Unknown</c>.</summary>
    /// <param name="value">The wire value to parse.</param>
    /// <returns>The recognized provenance or the safe default.</returns>
    public static QueryResponseProvenance Parse(string? value)
        => value switch
        {
            nameof(QueryResponseProvenance.ProjectionBacked) => QueryResponseProvenance.ProjectionBacked,
            nameof(QueryResponseProvenance.HandlerComputed) => QueryResponseProvenance.HandlerComputed,
            _ => QueryResponseProvenance.Unknown,
        };
}
