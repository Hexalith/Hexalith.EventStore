using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService;

/// <summary>A counted observation awaiting its source marker and fresh-coverage acknowledgement.</summary>
public sealed record ProcessPendingObservation(ProcessSource Source, long Cycle, bool Unresolved);
