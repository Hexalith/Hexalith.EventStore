namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for the event drain recovery mechanism (Story 4.4).
/// Bound to configuration section "EventStore:Drain".
/// </summary>
public record EventDrainOptions {
    /// <summary>Gets the initial delay before the first drain attempt after publication failure.</summary>
    public TimeSpan InitialDrainDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the recurring period between drain retry attempts.</summary>
    public TimeSpan DrainPeriod { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets the maximum drain period (upper bound for retry intervals).</summary>
    public TimeSpan MaxDrainPeriod { get; init; } = TimeSpan.FromMinutes(30);
}
