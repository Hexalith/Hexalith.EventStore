namespace Hexalith.EventStore.Contracts.Events;

/// <summary>
/// Framework-level rejection event persisted when a command targets a tombstoned aggregate (FR66).
/// Implements <see cref="IRejectionEvent"/> so the existing rejection path handles it automatically.
/// </summary>
/// <param name="AggregateType">The aggregate class name (diagnostic context, not used for routing).</param>
/// <param name="AggregateId">The aggregate identifier that was terminated.</param>
public sealed record AggregateTerminated(string AggregateType, string AggregateId) : IRejectionEvent;
