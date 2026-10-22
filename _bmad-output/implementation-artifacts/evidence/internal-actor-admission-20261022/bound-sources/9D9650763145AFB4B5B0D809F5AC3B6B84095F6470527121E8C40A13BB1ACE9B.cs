using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Bounds exact guarded receipts against the completed checkpoint before proof graph use.</summary>
internal static class ProjectionDeliveryReceiptValidation
{
    /// <summary>Returns the only possible retained receipt count for a guarded row.</summary>
    internal static long GetExpectedCount(long completed, long retained, int receiptLimit)
    {
        if (completed < 0 || completed == 0 && retained != 0
            || completed > 0 && (retained <= 0 || retained > completed)) Reject();
        long count = completed == 0 ? 0 : completed - retained + 1;
        if (count > receiptLimit || count > ProjectionDeliveryIdempotencyOptions.MaximumCompletedReceiptLimit) Reject();
        return count;
    }

    /// <summary>Rejects malformed typed guarded receipts before sizing, serialization or classification.</summary>
    internal static void Validate(ProjectionDeliveryState state, int receiptLimit)
    {
        if (state.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion) return;
        long count = GetExpectedCount(state.LastDeliveredSequence, state.FirstRetainedSequence, receiptLimit);
        if (state.CompletedReceipts is not { } receipts || receipts.Count != count) Reject();
        long sequence = state.FirstRetainedSequence;
        foreach (ProjectionDeliveryReceipt receipt in ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts!))
        {
            if (receipt is null || receipt.SequenceNumber != sequence
                || string.IsNullOrWhiteSpace(receipt.MessageId) || string.IsNullOrWhiteSpace(receipt.EventFingerprint)
                || string.IsNullOrWhiteSpace(receipt.PrefixFingerprint)) Reject();
            sequence++;
        }
    }

    private static void Reject() => throw new ProjectionDeliveryHistoryValidationException("delivery_reconciliation_required");
}
