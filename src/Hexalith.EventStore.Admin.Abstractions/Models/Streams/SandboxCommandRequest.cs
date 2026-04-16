namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Captures the command input for a sandbox (dry-run) execution against an aggregate's
/// historical state. No events are persisted — the sandbox invokes the domain service
/// Handle method and returns what would happen.
/// </summary>
/// <param name="CommandType">The fully qualified command type name (e.g., "Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter").</param>
/// <param name="PayloadJson">The command payload as a JSON string; if null or empty, defaults to "{}".</param>
/// <param name="AtSequence">The state position to execute against; null = latest state; 0 = empty initial state before any events.</param>
/// <param name="CorrelationId">Optional correlation ID for tracing; auto-generated if null.</param>
/// <param name="UserId">Optional user ID override; defaults to the authenticated user's ID from JWT.</param>
public record SandboxCommandRequest(
    string CommandType,
    string? PayloadJson,
    long? AtSequence,
    string? CorrelationId,
    string? UserId) {
    /// <summary>Gets the fully qualified command type name.</summary>
    public string CommandType { get; } = CommandType ?? string.Empty;

    /// <summary>Gets the command payload as a JSON string.</summary>
    public string? PayloadJson { get; } = PayloadJson;

    /// <summary>Gets the optional correlation ID for tracing.</summary>
    public string? CorrelationId { get; } = CorrelationId;

    /// <summary>Gets the optional user ID override.</summary>
    public string? UserId { get; } = UserId;

    /// <summary>
    /// Returns a string representation with PayloadJson redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"SandboxCommandRequest {{ CommandType = {CommandType}, PayloadJson = [REDACTED], AtSequence = {AtSequence?.ToString() ?? "latest"}, CorrelationId = {CorrelationId ?? "(auto)"}, UserId = {UserId ?? "(default)"} }}";
}
