using System.Text;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Enforces the approved serialized proof budget without pruning any correctness evidence.</summary>
internal static class ProjectionDeliveryStateSize
{
    internal const int AbsoluteLimit = 4_194_304;

    /// <summary>Returns S(H,R), including a frozen pending head when present.</summary>
    internal static long GetLimit(ProjectionDeliveryState state, int receiptLimit)
    {
        // S(H,R) qualifies the frozen fixture identifier lengths. Longer identifiers keep
        // their existing compatibility and are measured against the absolute cap.
        if (new[] { state.TenantId, state.Domain, state.AggregateId, state.ProjectionName }
                .Any(static value => value is not null && Encoding.UTF8.GetByteCount(value) > 64)
            || state.LastCompletedMessageId?.Length > 36
            || state.ActiveReservation?.HeadMessageId.Length > 36
            || state.ActiveReservation?.GuardBinding?.SelectedOwnerGeneration is { } generation
                && Encoding.UTF8.GetByteCount(generation) > 36
            || HasLongOwnerReferences(state.GuardEvidence?.OwnerProof)
            || HasLongOwnerReferences(state.ActiveReservation?.GuardBinding?.BaseOwnerProof)
            || HasLongOwnerReferences(state.IdleBindingPending?.OwnerProof)
            || ProjectionDeliveryCostDiagnostics.VisitProof(state.CompletedReceipts ?? []).Any(static receipt => receipt.MessageId.Length > 36))
            return AbsoluteLimit;
        long head = Math.Max(state.LastDeliveredSequence, state.ActiveReservation?.EndSequence ?? 0);
        if (head < 0 || head > (long.MaxValue - 16_384L) / 592)
            return AbsoluteLimit;
        return Math.Min(AbsoluteLimit, 16_384L + (80 * head) + (512 * Math.Min(head, receiptLimit)));
    }

    private static bool HasLongOwnerReferences(ProjectionOwnerProof? proof)
        => proof?.Selection is { } selection && (ExceedsUtf8(selection.OwnerGeneration, 36)
            || ExceedsUtf8(selection.OwnerIdentity, 64) || ExceedsUtf8(selection.FreshnessVersion, 64)
            || selection.MessageId?.Length > 36 || selection.BatchId?.Length > 36);

    private static bool ExceedsUtf8(string? value, int limit)
        => value is not null && Encoding.UTF8.GetByteCount(value) > limit;

    /// <summary>Rejects oversized guarded rows while leaving historical unguarded configurations compatible.</summary>
    internal static void Validate(ProjectionDeliveryState state, long bytes, int receiptLimit)
    {
        if (state.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion
            && (bytes > AbsoluteLimit || bytes > GetLimit(state, receiptLimit)))
            throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded");
    }
}
