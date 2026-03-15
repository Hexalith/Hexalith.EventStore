namespace Hexalith.EventStore.Sample.Counter.Commands;

/// <summary>
/// Command to permanently close the counter. Once closed, the aggregate
/// is tombstoned and rejects all further commands (FR66).
/// </summary>
public sealed record CloseCounter;
