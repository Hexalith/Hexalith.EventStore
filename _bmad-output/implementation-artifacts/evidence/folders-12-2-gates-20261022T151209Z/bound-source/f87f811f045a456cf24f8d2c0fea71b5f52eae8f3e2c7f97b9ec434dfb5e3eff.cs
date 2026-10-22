using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService;

/// <summary>Durable readiness handoff from source mutation to a later source observation.</summary>
public sealed record ProcessPendingTransition(ProcessSource Source, long Generation, string Token);
