namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>One flushed observation of an actual actor execution, without deduplication.</summary>
/// <param name="OperationId">The submitted observation identity.</param>
/// <param name="Stage">Actor entry, save start, or save completion.</param>
/// <param name="ProcessId">The independent host process.</param>
/// <param name="AtUtc">The actual observation time.</param>
/// <param name="ActorId">The routed exact actor identity.</param>
/// <param name="Request">The observed operation.</param>
/// <param name="Detail">The returned code or observed save failure type.</param>
public sealed record AttemptObservation(string OperationId, string Stage, int ProcessId, DateTimeOffset AtUtc,
    string ActorId, ShardRequest Request, string? Detail = null);
