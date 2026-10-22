namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Private candidate exact durable occurrence owner transport; missing current credentials, installation or antirollback proof denies every operation.</summary>
public interface IInteractionOccurrenceRegistry
{
    /// <summary>Durably reserves a unique reference before encryption, or returns the original sealed result.</summary>
    Task<InteractionOccurrenceReservationResult> ReserveAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken = default);
    /// <summary>Retains the original exact ciphertext under the current completion lease.</summary>
    Task<InteractionOccurrenceReservationResult> RetainSealedAsync(InteractionOccurrenceIdentity identity, InteractionOccurrenceSealedResult result, CancellationToken cancellationToken = default);
    /// <summary>Confirms exact committed or never-written source evidence independently of ciphertext retention.</summary>
    Task<InteractionOccurrenceReservationResult> CompleteWriterAsync(InteractionOccurrenceIdentity identity, string keyReference, string proofId, bool persisted, CancellationToken cancellationToken = default);
    /// <summary>Returns only current-authenticated exact original occurrence evidence.</summary>
    Task<InteractionOccurrenceReservationResult> LookupAsync(InteractionOccurrenceIdentity identity, CancellationToken cancellationToken = default);
}
