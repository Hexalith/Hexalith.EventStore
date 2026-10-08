using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Bounds and encodes logical ledger participants without generic JSON materialization.</summary>
internal static class DaprReplayLedgerCodec
{
    /// <summary>Produces the fixed scalar preimage used solely for exact current logical participant comparison.</summary>
    internal static byte[] Encode(DaprReplayPageLedger ledger)
    {
        if (ledger.PageOrdinal is < 1 or > 65536 || ledger.Generation < 1 || ledger.Count is < 0 or > 256
            || ledger.StartSequence < 1 || ledger.EndSequence < 0
            || ledger.RequestHash is not
            { Length: 32 }
        || ledger.PreviousAccumulator is not
        { Length: 32 }

            || ledger.Accumulator is not
            { Length: 32 }
        || ledger.ResponseHash is not
        { Length: 32 }

            || (ledger.PriorStateHash is not null && ledger.PriorStateHash.Length != 32)
            || (ledger.CanonicalStateHash is not null && ledger.CanonicalStateHash.Length != 32))
        {
            throw new InvalidOperationException("ReplayRestartRequired: logical ledger fields exceed their bounded schema.");
        }

        using var writer = new EventEvolutionBinaryWriter(512);
        writer.WriteRaw("HX-EV-DAPR-REPLAY-LEDGER-1\0"u8);
        writer.WriteByte(1);
        writer.WriteInt64(ledger.PageOrdinal);
        writer.WriteInt64(ledger.Generation);
        writer.WriteHash(ledger.RequestHash);
        writer.WriteHash(ledger.PreviousAccumulator);
        writer.WriteHash(ledger.Accumulator);
        writer.WriteInt64(ledger.StartSequence);
        writer.WriteInt64(ledger.EndSequence);
        writer.WriteInt32(ledger.Count);
        writer.WriteHash(ledger.ResponseHash);
        writer.WriteByte(ledger.IsFinal ? (byte)1 : (byte)0);
        writer.WriteByte(ledger.PriorStateHash is null ? (byte)0 : (byte)1);
        if (ledger.PriorStateHash is not null)
        {
            writer.WriteHash(ledger.PriorStateHash);
        }

        writer.WriteByte(ledger.CanonicalStateHash is null ? (byte)0 : (byte)1);
        if (ledger.CanonicalStateHash is not null)
        {
            writer.WriteHash(ledger.CanonicalStateHash);
        }

        return writer.CopyEncodedBytes();
    }
}
