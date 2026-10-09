namespace Hexalith.EventStore.Client.Events;

/// <summary>Frames and verifies a distinct logical purpose-07 proof without accepting historical proof carriers.</summary>
internal static class DaprLogicalCommandStateProofCodec
{
    /// <summary>Gets the independently selected bounded completed-proof ceiling.</summary>
    internal const int MaximumProofBytes = 2 * 1024 * 1024;

    /// <summary>Encodes exactly one signed logical completed-state claim.</summary>
    internal static byte[] Encode(DaprLogicalSignedClaim signed)
    {
        using var writer = new EventEvolutionBinaryWriter(checked(signed.Claim.Length + signed.KeyId.Length * 4 + 256));
        writer.WriteRaw("HX-EV-DAPR-COMMAND-PROOF-1\0"u8);
        writer.WriteByte(1);
        writer.WriteBytes(signed.Claim.Span);
        writer.WriteString(signed.KeyId);
        writer.WriteBytes(signed.Signature.Span);
        if (writer.Length > MaximumProofBytes)
        {
            throw new InvalidOperationException("ProofLimit: completed command proof exceeds 2 MiB.");
        }
        return writer.CopyEncodedBytes();
    }

    /// <summary>Verifies exact purpose, key, model, current registry and expiry after bounded structural admission.</summary>
    internal static DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim> Verify(ReadOnlySpan<byte> bytes,
        DaprLogicalClaimTrust trust, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (bytes.Length > MaximumProofBytes)
        {
            throw new InvalidOperationException("ProofLimit: completed command proof exceeds 2 MiB.");
        }
        using EventBufferReservation keyWorkspace = budget.Reserve(512);
        var reader = new EventEvolutionBinaryReader(bytes);
        if (!reader.ReadRaw("HX-EV-DAPR-COMMAND-PROOF-1\0"u8.Length).SequenceEqual("HX-EV-DAPR-COMMAND-PROOF-1\0"u8)
            || reader.ReadByte() != 1)
        {
            throw new ArgumentException("Logical command-proof model or codec mismatch.");
        }
        ReadOnlySpan<byte> claim = reader.ReadBytes(DaprLogicalClaimCodec.MaximumClaimBytes);
        string key = reader.ReadString(64);
        ReadOnlySpan<byte> signature = reader.ReadBytes(64);
        reader.RequireEnd();
        return trust.VerifyCommandState(claim, key, signature, budget, token);
    }
}
