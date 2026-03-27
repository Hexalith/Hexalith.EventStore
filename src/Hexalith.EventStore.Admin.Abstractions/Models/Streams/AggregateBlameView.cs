namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Wraps the blame result showing per-field provenance for an aggregate's state.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AtSequence">The sequence position blame was computed at.</param>
/// <param name="Timestamp">Timestamp of the event at AtSequence.</param>
/// <param name="Fields">One entry per leaf field in the state JSON, sorted by FieldPath.</param>
/// <param name="IsTruncated">True when the event stream exceeded MaxBlameEvents and blame was computed from a partial window.</param>
/// <param name="IsFieldsTruncated">True when the state had more leaf fields than MaxBlameFields.</param>
public record AggregateBlameView(
    string TenantId,
    string Domain,
    string AggregateId,
    long AtSequence,
    DateTimeOffset Timestamp,
    IReadOnlyList<FieldProvenance> Fields,
    bool IsTruncated,
    bool IsFieldsTruncated)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = TenantId ?? string.Empty;

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = Domain ?? string.Empty;

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = AggregateId ?? string.Empty;

    /// <summary>Gets the per-field provenance entries, sorted by FieldPath.</summary>
    public IReadOnlyList<FieldProvenance> Fields { get; } = Fields ?? [];

    /// <summary>
    /// Returns a string representation with field values redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"AggregateBlameView {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, AtSequence = {AtSequence}, Fields = [{Fields.Count} fields], IsTruncated = {IsTruncated}, IsFieldsTruncated = {IsFieldsTruncated} }}";
}
