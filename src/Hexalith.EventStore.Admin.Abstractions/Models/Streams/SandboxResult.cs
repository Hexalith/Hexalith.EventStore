namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Wraps the complete output of a sandbox (dry-run) command execution.
/// Contains the produced events, resulting state, state diff, and execution metadata.
/// No events are actually persisted — this represents a hypothetical execution result.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AtSequence">The state position the command was executed against; 0 if tested against empty initial state.</param>
/// <param name="CommandType">The command type that was tested.</param>
/// <param name="Outcome">The outcome: "accepted", "rejected", or "error".</param>
/// <param name="ProducedEvents">Events that would be produced; empty list on error or no-op.</param>
/// <param name="ResultingStateJson">The aggregate state JSON after applying produced events; empty on rejection or error.</param>
/// <param name="StateChanges">Diff between input state and resulting state; empty on rejection, error, or no-op.</param>
/// <param name="ErrorMessage">Non-null only when Outcome is "error".</param>
/// <param name="ExecutionTimeMs">Elapsed time for sandbox execution in milliseconds.</param>
public record SandboxResult(
    string TenantId,
    string Domain,
    string AggregateId,
    long AtSequence,
    string CommandType,
    string Outcome,
    IReadOnlyList<SandboxEvent> ProducedEvents,
    string ResultingStateJson,
    IReadOnlyList<FieldChange> StateChanges,
    string? ErrorMessage,
    long ExecutionTimeMs) {
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = TenantId ?? string.Empty;

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = Domain ?? string.Empty;

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = AggregateId ?? string.Empty;

    /// <summary>Gets the command type that was tested.</summary>
    public string CommandType { get; } = CommandType ?? string.Empty;

    /// <summary>Gets the outcome: "accepted", "rejected", or "error".</summary>
    public string Outcome { get; } = Outcome ?? string.Empty;

    /// <summary>Gets the events that would be produced.</summary>
    public IReadOnlyList<SandboxEvent> ProducedEvents { get; } = ProducedEvents ?? [];

    /// <summary>Gets the aggregate state JSON after applying produced events.</summary>
    public string ResultingStateJson { get; } = ResultingStateJson ?? string.Empty;

    /// <summary>Gets the diff between input state and resulting state.</summary>
    public IReadOnlyList<FieldChange> StateChanges { get; } = StateChanges ?? [];

    /// <summary>
    /// Returns a string representation with ResultingStateJson, event payloads, and field values redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"SandboxResult {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, AtSequence = {AtSequence}, CommandType = {CommandType}, Outcome = {Outcome}, ProducedEvents = [{ProducedEvents.Count} events], ResultingStateJson = [REDACTED], StateChanges = [{StateChanges.Count} changes], ErrorMessage = {ErrorMessage ?? "(none)"}, ExecutionTimeMs = {ExecutionTimeMs} }}";
}
