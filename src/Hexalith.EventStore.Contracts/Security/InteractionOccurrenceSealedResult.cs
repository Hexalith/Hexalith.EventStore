namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate original sealed ciphertext only; no plaintext/root/derived key in state.</summary>
/// <param name="KeyReference">Original uniquely reserved reference.</param>
/// <param name="PayloadBytes">Complete protected ciphertext JSON.</param>
/// <param name="SerializationFormat">Exact pdenc-v2 format.</param>
/// <param name="ProtectedPathCount">Positive complete selected field count.</param>
public sealed record InteractionOccurrenceSealedResult(string KeyReference, byte[] PayloadBytes, string SerializationFormat, int ProtectedPathCount)
{
    /// <summary>Authenticated content-free replay skeleton, verified against decrypted original bytes before retention. Legacy V2 ciphertext bytes are unchanged.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? SnapshotReplaySkeleton { get; init; }
    /// <inheritdoc/>
    public override string ToString() => nameof(InteractionOccurrenceSealedResult);
}
