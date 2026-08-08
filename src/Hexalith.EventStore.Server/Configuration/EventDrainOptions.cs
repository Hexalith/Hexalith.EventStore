namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for the event drain recovery mechanism (Story 4.2, Story 4.4).
/// Bound to configuration section "EventStore:Drain".
/// <para>
/// Every field in this record is normalized at the point of use rather than validated at startup.
/// The timing fields deliberately tolerate zero/negative/over-max values (see
/// <c>docs/guides/dapr-component-reference.md</c>), and adding a second, startup-time policy over
/// the same inputs would make those normalization branches unreachable. The two Story 4.4 bounds
/// follow the same single-authority rule through
/// <see cref="NormalizeMaxDrainAttempts(int)"/> and
/// <see cref="NormalizeMaxOutstandingPublicationEntries(int, int)"/>.
/// </para>
/// </summary>
public record EventDrainOptions {
    /// <summary>
    /// The default maximum number of drain attempts before the range is dead-lettered.
    /// Mirrors <c>ProjectionDispatchOptions.DefaultMaxRetryAttempts</c>, the house bounded-attempt idiom.
    /// </summary>
    public const int DefaultMaxDrainAttempts = 8;

    /// <summary>
    /// Sentinel meaning "derive the index bound from
    /// <c>BackpressureOptions.MaxPendingCommandsPerAggregate</c>". Kept as the default so the bound
    /// tracks the backpressure ceiling instead of sitting above it, where the fail-closed branch
    /// could never fire.
    /// </summary>
    public const int DeriveMaxOutstandingPublicationEntries = 0;

    /// <summary>Gets the initial delay before the first drain attempt after publication failure.</summary>
    public TimeSpan InitialDrainDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the recurring period between drain retry attempts.</summary>
    public TimeSpan DrainPeriod { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets the maximum drain period (upper bound for retry intervals).</summary>
    public TimeSpan MaxDrainPeriod { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets the maximum number of drain attempts for one committed range. When the persisted
    /// retry count reaches this bound the range is dead-lettered instead of retried forever.
    /// </summary>
    public int MaxDrainAttempts { get; init; } = DefaultMaxDrainAttempts;

    /// <summary>
    /// Gets the maximum number of outstanding entries in the publication recovery index.
    /// <para>
    /// Left at <see cref="DeriveMaxOutstandingPublicationEntries"/> it tracks
    /// <c>BackpressureOptions.MaxPendingCommandsPerAggregate</c>. That matters: an entry and a
    /// pending slot are created and released together, so a bound ABOVE the backpressure ceiling
    /// could never be reached and the fail-closed branch would be dead code. At parity the branch
    /// is the backstop for a pending counter that has drifted BELOW the true outstanding count --
    /// which the backpressure read does deliberately, since it fails open to zero when the state
    /// read throws (<c>AggregateActor</c> "Backpressure check state read failed (fail-open)").
    /// </para>
    /// </summary>
    public int MaxOutstandingPublicationEntries { get; init; } = DeriveMaxOutstandingPublicationEntries;

    /// <summary>Normalizes a configured drain-attempt cap, falling back to the default when it is not positive.</summary>
    /// <param name="value">The configured value.</param>
    /// <returns>A strictly positive attempt cap.</returns>
    public static int NormalizeMaxDrainAttempts(int value)
        => value > 0 ? value : DefaultMaxDrainAttempts;

    /// <summary>
    /// Normalizes the index bound, deriving it from the backpressure ceiling when unset.
    /// </summary>
    /// <param name="value">The configured value, or a non-positive sentinel meaning "derive".</param>
    /// <param name="maxPendingCommandsPerAggregate">The configured backpressure ceiling.</param>
    /// <returns>A strictly positive index bound.</returns>
    public static int NormalizeMaxOutstandingPublicationEntries(
        int value,
        int maxPendingCommandsPerAggregate)
        => value > 0
            ? value
            : Math.Max(1, maxPendingCommandsPerAggregate);
}
