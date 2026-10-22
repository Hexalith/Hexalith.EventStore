using Dapr.Actors;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Private candidate actual durable occurrence/reference writer owner; no production registration/profile acceptance.</summary>
public interface IInteractionOccurrenceRegistryActor : IActor
{
    /// <summary>Reserves exact original occurrence and globally unique tenant reference before any encryption.</summary>
    Task<InteractionOccurrenceReservationResult> ReserveAsync(InteractionOccurrenceRequest request);
    /// <summary>Retains one original sealed result under authenticated current completion lease before returning it to a writer.</summary>
    Task<InteractionOccurrenceReservationResult> RetainSealedAsync(InteractionOccurrenceIdentity identity, InteractionOccurrenceSealedResult sealedResult);
    /// <summary>Activates or permanently aborts only with exact independent current writer proof.</summary>
    Task<InteractionOccurrenceReservationResult> CompleteWriterAsync(InteractionOccurrenceIdentity identity, string keyReference, string proofId, bool persisted);
    /// <summary>Authenticated exact original record; stale restore/unavailable authority denies release.</summary>
    Task<InteractionOccurrenceReservationResult> LookupAsync(InteractionOccurrenceIdentity identity);
}
