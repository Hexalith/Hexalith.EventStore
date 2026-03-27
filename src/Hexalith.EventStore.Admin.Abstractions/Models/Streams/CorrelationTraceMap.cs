namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Captures the full trace for a correlation ID — from command submission through event production
/// to projection consumption. Used by the trace map UI to visualize a command's complete lifecycle.
/// </summary>
/// <param name="CorrelationId">The correlation ID being traced.</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain of the aggregate that processed the command; empty if command status not found.</param>
/// <param name="AggregateId">The aggregate that processed the command; empty if command status not found.</param>
/// <param name="CommandType">The fully qualified command type name.</param>
/// <param name="CommandStatus">Terminal status: "Completed", "Rejected", "PublishFailed", "TimedOut", "Processing", or "Unknown".</param>
/// <param name="UserId">Who submitted the command.</param>
/// <param name="CommandReceivedAt">When the command entered the pipeline.</param>
/// <param name="CommandCompletedAt">When the command reached terminal status.</param>
/// <param name="DurationMs">Elapsed time from received to completed; null if either timestamp is missing.</param>
/// <param name="ProducedEvents">Events produced by this command in the aggregate stream.</param>
/// <param name="AffectedProjections">Projections that consume events from this domain with their processing status.</param>
/// <param name="RejectionEventType">Non-null when CommandStatus is "Rejected".</param>
/// <param name="ErrorMessage">Non-null when CommandStatus is "PublishFailed", "TimedOut", or computation failed.</param>
/// <param name="ExternalTraceUrl">Deep link to external observability tool, null if not configured.</param>
/// <param name="TotalStreamEvents">Total events in the aggregate stream for context.</param>
/// <param name="ScanCapped">True if the event scan hit the 10,000-event cap before finding all expected events.</param>
/// <param name="ScanCapMessage">Non-null when ScanCapped is true, describes the cap condition.</param>
public record CorrelationTraceMap(
    string CorrelationId,
    string TenantId,
    string Domain,
    string AggregateId,
    string CommandType,
    string CommandStatus,
    string? UserId,
    DateTimeOffset? CommandReceivedAt,
    DateTimeOffset? CommandCompletedAt,
    long? DurationMs,
    IReadOnlyList<TraceMapEvent> ProducedEvents,
    IReadOnlyList<TraceMapProjection> AffectedProjections,
    string? RejectionEventType,
    string? ErrorMessage,
    string? ExternalTraceUrl,
    long TotalStreamEvents,
    bool ScanCapped,
    string? ScanCapMessage)
{
    /// <summary>Gets the correlation ID being traced.</summary>
    public string CorrelationId { get; } = CorrelationId ?? string.Empty;

    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = TenantId ?? string.Empty;

    /// <summary>Gets the domain of the aggregate that processed the command.</summary>
    public string Domain { get; } = Domain ?? string.Empty;

    /// <summary>Gets the aggregate that processed the command.</summary>
    public string AggregateId { get; } = AggregateId ?? string.Empty;

    /// <summary>Gets the fully qualified command type name.</summary>
    public string CommandType { get; } = CommandType ?? string.Empty;

    /// <summary>Gets the terminal status of the command.</summary>
    public string CommandStatus { get; } = CommandStatus ?? string.Empty;

    /// <summary>Gets the events produced by this command.</summary>
    public IReadOnlyList<TraceMapEvent> ProducedEvents { get; } = ProducedEvents ?? [];

    /// <summary>Gets the projections affected by this command's events.</summary>
    public IReadOnlyList<TraceMapProjection> AffectedProjections { get; } = AffectedProjections ?? [];

    /// <summary>
    /// Returns a string representation with event payloads omitted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"CorrelationTraceMap {{ CorrelationId = {CorrelationId}, TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, CommandType = {CommandType}, CommandStatus = {CommandStatus}, UserId = {UserId ?? "(none)"}, DurationMs = {DurationMs?.ToString() ?? "(none)"}, ProducedEvents = [{ProducedEvents.Count} events], AffectedProjections = [{AffectedProjections.Count} projections], ScanCapped = {ScanCapped}, TotalStreamEvents = {TotalStreamEvents} }}";
}
