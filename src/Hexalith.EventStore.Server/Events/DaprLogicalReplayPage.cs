using System.Security.Cryptography;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Owns an addressed effective logical page and its distinct exact signed logical claims.</summary>
internal sealed class DaprLogicalReplayPage : IDisposable
{
    private readonly DaprLogicalEventPage _page;
    private readonly DaprLogicalSignedClaim[] _routes;

    /// <summary>Takes ownership only after complete source, catalog, current trust and prefix verification.</summary>
    internal DaprLogicalReplayPage(DaprLogicalEventPage page, DaprLogicalSignedClaim[] routes,
        DaprLogicalSignedClaim prefix, DaprLogicalPrefixClaim prefixFields)
    { _page = page; _routes = routes; Prefix = prefix; PrefixFields = prefixFields; }

    /// <summary>Gets the complete admitted source range fields.</summary>
    internal DaprLogicalPrefixClaim PrefixFields { get; }

    /// <summary>Gets effective events owned by this private logical page.</summary>
    internal IReadOnlyList<DaprLogicalEventView> Events => _page.Events;

    /// <summary>Gets the new logical prefix carrier, without historical StoredDigest semantics.</summary>
    internal DaprLogicalSignedClaim Prefix { get; }

    /// <summary>Gets the exact route signatures in contiguous source sequence order.</summary>
    internal IReadOnlyList<DaprLogicalSignedClaim> Routes => Array.AsReadOnly(_routes);

    /// <summary>Produces the exact additive private response image, with existing bounded outer proof framing.</summary>
    internal DaprLogicalResponseOwner EncodeResponse(EventBufferBudget budget, CancellationToken cancellationToken)
    {
        int proofSize = 20 + EntryLength(Prefix);
        foreach (DaprLogicalSignedClaim route in _routes) { proofSize = checked(proofSize + EntryLength(route)); }
        if (proofSize > 2 * 1024 * 1024) { throw new InvalidOperationException("ProofLimit: command replay proof exceeds 2 MiB."); }
        int responseSize = checked(64 + proofSize);
        foreach (DaprLogicalEventView view in Events)
        {
            responseSize = checked(responseSize + 8 + 16 + System.Text.Encoding.UTF8.GetByteCount(view.Resolved.CanonicalType)
                + System.Text.Encoding.UTF8.GetByteCount(view.Resolved.SerializationFormat) + view.Resolved.Payload.Length);
        }
        if (responseSize > 64 * 1024 * 1024) { throw new InvalidOperationException("ReadableLimit: logical response exceeds its page ceiling."); }
        using EventBufferReservation charge = budget.Reserve(checked(responseSize * 2 + proofSize));
        using var proof = new EventEvolutionBinaryWriter(proofSize);
        proof.WriteRaw("HX-EV-PROOF-1\0"u8); proof.WriteByte(1); proof.WriteUInt32(checked((uint)_routes.Length));
        foreach (DaprLogicalSignedClaim route in _routes) { WriteEntry(proof, route); }
        WriteEntry(proof, Prefix); proof.WriteByte(0);
        byte[] proofBytes = proof.CopyEncodedBytes();
        try
        {
            using var writer = new EventEvolutionBinaryWriter(responseSize);
            writer.WriteRaw(PrefixFields.LogicalEvidenceModelId == DaprLogicalReplayAnchorCodec.ModelId
                ? "HX-EV-DAPR-ANCHORED-PAGE-1\0"u8 : "HX-EV-DAPR-REPLAY-PAGE-1\0"u8);
            writer.WriteByte(1); writer.WriteBytes(proofBytes);
            writer.WriteUInt32(checked((uint)Events.Count));
            foreach (DaprLogicalEventView view in Events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteInt64(view.SequenceNumber); writer.WriteString(view.Resolved.CanonicalType);
                writer.WriteInt32(view.Resolved.CurrentVersion); writer.WriteString(view.Resolved.SerializationFormat);
                int length = view.Resolved.Payload.Length;
                using EventBufferReservation payloadCharge = budget.Reserve(length);
                byte[] payload = new byte[length];
                try { view.Resolved.Payload.CopyTo(0, payload); writer.WriteBytes(payload); }
                finally { CryptographicOperations.ZeroMemory(payload); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            EventBufferReservation retained = budget.Reserve(checked(writer.Length + 256));
            try { return new DaprLogicalResponseOwner(writer.CopyEncodedBytes(), retained); }
            catch { retained.Dispose(); throw; }
        }
        finally { CryptographicOperations.ZeroMemory(proofBytes); }
    }

    private static int EntryLength(DaprLogicalSignedClaim entry)
        => checked(12 + entry.Claim.Length + System.Text.Encoding.UTF8.GetByteCount(entry.KeyId) + 64);
    private static void WriteEntry(EventEvolutionBinaryWriter writer, DaprLogicalSignedClaim entry) { writer.WriteBytes(entry.Claim.Span); writer.WriteString(entry.KeyId); writer.WriteBytes(entry.Signature.Span); }

    /// <inheritdoc/>
    public void Dispose() { foreach (DaprLogicalSignedClaim route in _routes) { route.Dispose(); } Prefix.Dispose(); _page.Dispose(); }
}
