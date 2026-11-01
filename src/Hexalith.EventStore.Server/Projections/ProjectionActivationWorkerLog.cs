using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Closed, payload-free activation failure and recovery signals.</summary>
internal static partial class ProjectionActivationWorkerLog
{
    /// <summary>Records a failed scan, delivery or conditional deferral.</summary>
    [LoggerMessage(EventId = 0, Level = LogLevel.Warning,
        Message = "Projection activation failure: Operation={Operation}, ReasonCode={ReasonCode}, Stage=ProjectionActivationFailed")]
    internal static partial void Failed(ILogger logger, string operation, string reasonCode);

    /// <summary>Preserves only closed source-guard refusal categories, never arbitrary provider or handler text.</summary>
    internal static void GuardRefused(ILogger logger, string? reasonCode)
        => Failed(logger, "delivery", reasonCode switch
        {
            "guard_activation_required" or "unsupported_capability" or "unsupported_dispatch_capability" or "source_proof_missing" or "source_conflict"
                or "checkpoint_drift" or "head_changed" or "empty_no_work" or "history_unavailable"
                or "delivery_reconciliation_required" or "delivery_in_progress" or "delivery_state_unavailable"
                or "delivery_rebuild_required" or "delivery_identity_conflict" or "delivery_gap"
                or "delivery_page_limit_exceeded" or "delivery_byte_limit_exceeded" or "delivery_owner_limit_exceeded"
                or "delivery_proof_limit_exceeded" or "handler_failure" or "malformed_projection_outcome" => reasonCode,
            _ => "activation_delivery_unproven",
        });

    /// <summary>Records demonstrated recovery of the indicated activation operation.</summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Projection activation recovered: Operation={Operation}, Stage=ProjectionActivationRecovered")]
    internal static partial void Recovered(ILogger logger, string operation);
}
