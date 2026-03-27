namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Parsed DAPR resiliency circuit breaker policy.
/// </summary>
/// <param name="Name">The policy identifier from the YAML key (e.g., "defaultBreaker").</param>
/// <param name="MaxRequests">The half-open probe count.</param>
/// <param name="Interval">The sampling window duration (e.g., "60s").</param>
/// <param name="Timeout">The open state duration (e.g., "60s").</param>
/// <param name="Trip">The trip condition expression (e.g., "consecutiveFailures > 5").</param>
public record DaprCircuitBreakerPolicy(
    string Name,
    int MaxRequests,
    string Interval,
    string Timeout,
    string Trip)
{
    /// <summary>Gets the policy identifier.</summary>
    public string Name { get; } = !string.IsNullOrWhiteSpace(Name)
        ? Name
        : throw new ArgumentException("Name cannot be null, empty, or whitespace.", nameof(Name));

    /// <summary>Gets the sampling window duration.</summary>
    public string Interval { get; } = !string.IsNullOrWhiteSpace(Interval)
        ? Interval
        : throw new ArgumentException("Interval cannot be null, empty, or whitespace.", nameof(Interval));

    /// <summary>Gets the open state duration.</summary>
    public string Timeout { get; } = !string.IsNullOrWhiteSpace(Timeout)
        ? Timeout
        : throw new ArgumentException("Timeout cannot be null, empty, or whitespace.", nameof(Timeout));

    /// <summary>Gets the trip condition expression.</summary>
    public string Trip { get; } = !string.IsNullOrWhiteSpace(Trip)
        ? Trip
        : throw new ArgumentException("Trip cannot be null, empty, or whitespace.", nameof(Trip));
}
