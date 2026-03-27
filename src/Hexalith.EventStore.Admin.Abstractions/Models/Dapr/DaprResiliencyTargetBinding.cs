namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Flattened target-to-policy assignment for grid display.
/// </summary>
/// <param name="TargetName">The target name (e.g., "commandapi", "pubsub", "statestore").</param>
/// <param name="TargetType">The target type: "App" or "Component".</param>
/// <param name="Direction">The direction: "Inbound", "Outbound", or null for apps and non-directional components.</param>
/// <param name="RetryPolicy">The name of the assigned retry policy, or null.</param>
/// <param name="TimeoutPolicy">The name of the assigned timeout policy, or null.</param>
/// <param name="CircuitBreakerPolicy">The name of the assigned circuit breaker policy, or null.</param>
public record DaprResiliencyTargetBinding(
    string TargetName,
    string TargetType,
    string? Direction,
    string? RetryPolicy,
    string? TimeoutPolicy,
    string? CircuitBreakerPolicy)
{
    /// <summary>Gets the target name.</summary>
    public string TargetName { get; } = !string.IsNullOrWhiteSpace(TargetName)
        ? TargetName
        : throw new ArgumentException("TargetName cannot be null, empty, or whitespace.", nameof(TargetName));

    /// <summary>Gets the target type.</summary>
    public string TargetType { get; } = !string.IsNullOrWhiteSpace(TargetType)
        ? TargetType
        : throw new ArgumentException("TargetType cannot be null, empty, or whitespace.", nameof(TargetType));
}
