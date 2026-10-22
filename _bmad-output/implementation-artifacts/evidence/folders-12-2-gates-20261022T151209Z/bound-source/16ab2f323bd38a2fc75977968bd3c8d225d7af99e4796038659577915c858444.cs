namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Parsed DAPR resiliency retry policy.
/// </summary>
/// <param name="Name">The policy identifier from the YAML key (e.g., "defaultRetry").</param>
/// <param name="Strategy">The retry strategy: "constant" or "exponential".</param>
/// <param name="MaxRetries">The maximum number of retries.</param>
/// <param name="Duration">The duration between retries for constant strategy (e.g., "1s").</param>
/// <param name="MaxInterval">The maximum interval for exponential backoff (e.g., "15s").</param>
public record DaprRetryPolicy(
    string Name,
    string Strategy,
    int MaxRetries,
    string? Duration,
    string? MaxInterval) {
    /// <summary>Gets the policy identifier.</summary>
    public string Name { get; } = !string.IsNullOrWhiteSpace(Name)
        ? Name
        : throw new ArgumentException("Name cannot be null, empty, or whitespace.", nameof(Name));

    /// <summary>Gets the retry strategy.</summary>
    public string Strategy { get; } = !string.IsNullOrWhiteSpace(Strategy)
        ? Strategy
        : throw new ArgumentException("Strategy cannot be null, empty, or whitespace.", nameof(Strategy));
}
