using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Preflights the approved outer proof framing before private buffer/entry allocation.</summary>
/// <remarks>
/// Claims are opaque exact bytes. This is not a semantic claim codec, signature verifier,
/// authenticated reader or checkpoint admission. A caller must separately verify every
/// purpose/key/claim, contiguous sequence, associated page count and complete prefix.
/// Dapr logical evidence does not grant historical/provider-attestation authority.
/// </remarks>
internal static class EventEvolutionProofFraming
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const int MaximumPageProofBytes = 64 * 1024 * 1024;
    private const int MaximumCommandProofBytes = 2 * 1024 * 1024;
    private const int MaximumRouteClaimBytes = 1024 * 1024;

    internal static UnverifiedEventEvolutionProofFrame Capture(ReadOnlySpan<byte> source, EventBufferBudget budget,
        bool commandPage, bool allowCheckpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);
        cancellationToken.ThrowIfCancellationRequested();
        int maximum = commandPage ? MaximumCommandProofBytes : MaximumPageProofBytes;
        allowCheckpoint &= !commandPage;
        int routeCount = Preflight(source, maximum, allowCheckpoint, cancellationToken);
        // One shared charge covers caller input, private exact bytes, slice-array capacity
        // and owner/container overhead. Claims and key strings are never copied separately.
        int charge = checked(source.Length * 2 + 512 + (routeCount + 2) * 64);
        EventBufferReservation reservation = budget.Reserve(charge);
        byte[]? bytes = null;
        try
        {
            bytes = source.ToArray();
            // Repeat the complete structural check on the private copy; caller mutation
            // during copying cannot substitute unadmitted lengths/counts/discriminators.
            int copiedCount = Preflight(bytes, maximum, allowCheckpoint, cancellationToken);
            if (copiedCount != routeCount) { throw new ArgumentException("Proof framing changed during private capture."); }
            var reader = new EventEvolutionBinaryReader(bytes);
            ReadHeader(ref reader);
            var routes = new EventEvolutionProofEntrySlice[routeCount];
            for (int index = 0; index < routes.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                routes[index] = ReadEntry(ref reader, MaximumRouteClaimBytes, maximum);
            }
            EventEvolutionProofEntrySlice prefix = ReadEntry(ref reader, maximum, maximum);
            EventEvolutionProofEntrySlice? checkpoint = reader.ReadByte() == 1 ? ReadEntry(ref reader, maximum, maximum) : null;
            reader.RequireEnd();
            cancellationToken.ThrowIfCancellationRequested();
            return new UnverifiedEventEvolutionProofFrame(bytes, reservation, routes, prefix, checkpoint);
        }
        catch
        {
            if (bytes is not null) { CryptographicOperations.ZeroMemory(bytes); }
            reservation.Dispose();
            throw;
        }
    }

    private static int Preflight(ReadOnlySpan<byte> bytes, int maximum, bool allowCheckpoint, CancellationToken cancellationToken)
    {
        if (bytes.Length > maximum) { throw new InvalidOperationException("ProofLimit: outer proof exceeds its page ceiling."); }
        var reader = new EventEvolutionBinaryReader(bytes);
        int count = ReadHeader(ref reader);
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = ReadEntry(ref reader, MaximumRouteClaimBytes, maximum);
        }
        _ = ReadEntry(ref reader, maximum, maximum); // Exactly one mandatory prefix.
        byte checkpoint = reader.ReadByte();
        if (checkpoint is not (0 or 1) || (checkpoint == 1 && !allowCheckpoint))
        {
            throw new ArgumentException("Invalid or disallowed proof checkpoint discriminator.");
        }
        if (checkpoint == 1) { _ = ReadEntry(ref reader, maximum, maximum); }
        reader.RequireEnd();
        cancellationToken.ThrowIfCancellationRequested();
        return count;
    }

    private static int ReadHeader(ref EventEvolutionBinaryReader reader)
    {
        ReadOnlySpan<byte> separator = "HX-EV-PROOF-1\0"u8;
        if (!reader.ReadRaw(separator.Length).SequenceEqual(separator) || reader.ReadByte() != 1)
        {
            throw new ArgumentException("Invalid outer proof separator or codec.");
        }
        uint count = reader.ReadUInt32();
        if (count > 256) { throw new InvalidOperationException("ProofLimit: a proof admits at most 256 route entries."); }
        return checked((int)count);
    }

    private static EventEvolutionProofEntrySlice ReadEntry(ref EventEvolutionBinaryReader reader, int maximumClaim, int maximumKey)
    {
        int claimOffset = checked(reader.Position + 4);
        int claimLength = reader.ReadBytes(maximumClaim).Length;
        int keyOffset = checked(reader.Position + 4);
        ReadOnlySpan<byte> key = reader.ReadBytes(maximumKey);
        if (claimLength == 0 || key.IsEmpty) { throw new ArgumentException("Proof entries require a nonempty claim and key ID."); }
        _ = StrictUtf8.GetCharCount(key);
        int signatureOffset = checked(reader.Position + 4);
        if (reader.ReadBytes(64).Length != 64) { throw new ArgumentException("A proof signature must be exactly 64 P1363 bytes."); }
        return new EventEvolutionProofEntrySlice(claimOffset, claimLength, keyOffset, key.Length, signatureOffset);
    }
}
