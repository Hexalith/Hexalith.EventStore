namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Wraps the output of a bisect binary search operation that identifies the exact event
/// where aggregate state diverged from expected field values.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="GoodSequence">The final narrowed known-good sequence.</param>
/// <param name="DivergentSequence">The exact event where divergence was first detected.</param>
/// <param name="DivergentTimestamp">Timestamp of the divergent event.</param>
/// <param name="DivergentEventType">Event type name of the divergent event.</param>
/// <param name="DivergentCorrelationId">Correlation ID of the divergent event.</param>
/// <param name="DivergentUserId">User who initiated the command that produced the divergent event.</param>
/// <param name="DivergentFieldChanges">The fields that changed at the divergent event AND were being watched.</param>
/// <param name="WatchedFieldPaths">The field paths that were compared during bisect; empty list means all fields were compared.</param>
/// <param name="Steps">Complete bisect history in order.</param>
/// <param name="TotalSteps">Count of bisect iterations performed.</param>
/// <param name="IsTruncated">True when the bisect was limited by MaxBisectSteps and could not converge to a single event.</param>
public record BisectResult(
    string TenantId,
    string Domain,
    string AggregateId,
    long GoodSequence,
    long DivergentSequence,
    DateTimeOffset DivergentTimestamp,
    string DivergentEventType,
    string DivergentCorrelationId,
    string DivergentUserId,
    IReadOnlyList<FieldChange> DivergentFieldChanges,
    IReadOnlyList<string> WatchedFieldPaths,
    IReadOnlyList<BisectStep> Steps,
    int TotalSteps,
    bool IsTruncated) {
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = TenantId ?? string.Empty;

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = Domain ?? string.Empty;

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = AggregateId ?? string.Empty;

    /// <summary>Gets the event type name of the divergent event.</summary>
    public string DivergentEventType { get; } = DivergentEventType ?? string.Empty;

    /// <summary>Gets the correlation ID of the divergent event.</summary>
    public string DivergentCorrelationId { get; } = DivergentCorrelationId ?? string.Empty;

    /// <summary>Gets the user who initiated the command that produced the divergent event.</summary>
    public string DivergentUserId { get; } = DivergentUserId ?? string.Empty;

    /// <summary>Gets the fields that changed at the divergent event and were being watched.</summary>
    public IReadOnlyList<FieldChange> DivergentFieldChanges { get; } = DivergentFieldChanges ?? [];

    /// <summary>Gets the field paths that were compared during bisect.</summary>
    public IReadOnlyList<string> WatchedFieldPaths { get; } = WatchedFieldPaths ?? [];

    /// <summary>Gets the complete bisect history in order.</summary>
    public IReadOnlyList<BisectStep> Steps { get; } = Steps ?? [];

    /// <summary>
    /// Returns a string representation with field values redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"BisectResult {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, GoodSequence = {GoodSequence}, DivergentSequence = {DivergentSequence}, DivergentEventType = {DivergentEventType}, Steps = [{Steps.Count} steps], IsTruncated = {IsTruncated} }}";
}
