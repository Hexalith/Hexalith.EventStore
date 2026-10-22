namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate exact original occurrence/reference/sealed result and separately verified writer completion.</summary>
/// <param name="Request">Original immutable reservation/fingerprint.</param>
/// <param name="KeyReference">Original durable unique reference.</param>
/// <param name="RegistryRevision">Last original transition revision.</param>
/// <param name="WriterState">Exact verified writer lifecycle.</param>
/// <param name="Sealed">Original ciphertext only.</param>
/// <param name="WriterProofId">Exact authoritative committed-or-never-written proof.</param>
public sealed record InteractionOccurrenceRecord(InteractionOccurrenceRequest Request, string KeyReference, long RegistryRevision, InteractionOccurrenceWriterState WriterState, InteractionOccurrenceSealedResult? Sealed, string? WriterProofId)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceRecord);
}
