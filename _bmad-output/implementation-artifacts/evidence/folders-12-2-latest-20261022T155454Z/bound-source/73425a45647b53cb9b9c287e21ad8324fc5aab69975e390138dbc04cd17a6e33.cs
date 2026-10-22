namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate original sealed ciphertext only; no plaintext/root/derived key in state.</summary>
/// <param name="KeyReference">Original uniquely reserved reference.</param>
/// <param name="PayloadBytes">Complete protected ciphertext JSON.</param>
/// <param name="SerializationFormat">Exact pdenc-v2 format.</param>
/// <param name="ProtectedPathCount">Positive complete selected field count.</param>
public sealed record InteractionOccurrenceSealedResult(string KeyReference, byte[] PayloadBytes, string SerializationFormat, int ProtectedPathCount)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceSealedResult);
}
