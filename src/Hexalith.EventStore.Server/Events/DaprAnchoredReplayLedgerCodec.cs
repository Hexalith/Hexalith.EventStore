using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Separately frames an anchored ledger's exact selection and complete bounded scalar participant image.</summary>
internal static class DaprAnchoredReplayLedgerCodec
{
    /// <summary>Encodes only anchored reconstruction ledgers, never a command-proof or ordinary predecessor.</summary>
    internal static byte[] Encode(DaprReplayPageLedger ledger, EventBufferBudget budget)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (ledger.AnchorSelectionHash is not { Length: 32 } || ledger.PriorStateHash is not { Length: 32 } || ledger.CanonicalStateHash is not { Length: 32 } || ledger.CommandProofHash is not null)
        {
            throw new InvalidOperationException("AnchorCapabilityHold: exact anchored reconstruction ledger fields are required.");
        }

        using EventBufferReservation working = budget.Reserve(4096);
        byte[] inner = DaprReplayLedgerCodec.Encode(ledger with { AnchorSelectionHash = null });
        try
        {
            using var w = new EventEvolutionBinaryWriter(checked("HX-EV-DAPR-ANCHORED-LEDGER-1\0"u8.Length + 1 + 32 + 4 + inner.Length));
            w.WriteRaw("HX-EV-DAPR-ANCHORED-LEDGER-1\0"u8);
            w.WriteByte(1);
            w.WriteHash(ledger.AnchorSelectionHash);
            w.WriteBytes(inner);
            return w.CopyEncodedBytes();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inner);
        }
    }
}
