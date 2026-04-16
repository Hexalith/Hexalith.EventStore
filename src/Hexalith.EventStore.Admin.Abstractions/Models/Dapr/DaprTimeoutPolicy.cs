namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Parsed DAPR resiliency timeout policy.
/// </summary>
/// <param name="Name">The policy identifier from the YAML key (e.g., "daprSidecar", "pubsubTimeout").</param>
/// <param name="Value">The timeout duration string (e.g., "5s", "10s", "30s").</param>
public record DaprTimeoutPolicy(
    string Name,
    string Value) {
    /// <summary>Gets the policy identifier.</summary>
    public string Name { get; } = !string.IsNullOrWhiteSpace(Name)
        ? Name
        : throw new ArgumentException("Name cannot be null, empty, or whitespace.", nameof(Name));

    /// <summary>Gets the timeout duration.</summary>
    public string Value { get; } = !string.IsNullOrWhiteSpace(Value)
        ? Value
        : throw new ArgumentException("Value cannot be null, empty, or whitespace.", nameof(Value));
}
