namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate source-only outcome; a caller-authored record is never qualified authority.</summary>
/// <param name="Status">Authoritative owner verdict.</param>
/// <param name="Record">Original exact durable record, only when authenticated.</param>
public sealed record InteractionOccurrenceReservationResult(InteractionOccurrenceReservationStatus Status, InteractionOccurrenceRecord? Record)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceReservationResult);
}
