using System.Diagnostics;
using System.Diagnostics.Metrics;

using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Emits bounded, scope-free projection delivery outcome telemetry.</summary>
internal static class ProjectionDeliveryDiagnostics {
    internal const string AdmissionActivityName = "EventStore.Projection.DeliveryAdmission";
    internal const string CompletionActivityName = "EventStore.Projection.DeliveryCompletion";
    internal const string CounterName = "eventstore.projection.delivery.transitions";
    internal const string RouteTag = "projection.route";
    internal const string StatusTag = "projection.status";
    internal const string ReasonTag = "projection.reason";
    internal const string ModeTag = "projection.mode";

    private static readonly Meter _meter = new(EventStoreActivitySource.SourceName);
    private static readonly Counter<long> _transitions = _meter.CreateCounter<long>(CounterName);
    private static readonly Histogram<long> _head = _meter.CreateHistogram<long>("eventstore.projection.delivery.head");
    private static readonly Histogram<long> _checkpoint = _meter.CreateHistogram<long>("eventstore.projection.delivery.checkpoint");
    private static readonly Histogram<long> _lag = _meter.CreateHistogram<long>("eventstore.projection.delivery.lag");
    private static readonly HashSet<string> _reasons = new(typeof(ProjectionDispatchReasonCodes).GetFields()
        .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(static field => (string)field.GetRawConstantValue()!), StringComparer.Ordinal)
    {
        "delivery_admitted", "delivery_completed", "delivery_fenced", "delivery_state_unavailable", "head_changed",
        "source_conflict", "source_proof_missing", "source_proof_unsupported", "checkpoint_drift", "history_unavailable",
        "delivery_owner_limit_exceeded", "delivery_page_limit_exceeded", "delivery_byte_limit_exceeded",
        "delivery_proof_limit_exceeded", "fallback_full_replay", "guard_activation_required", "empty_no_work", "notification_unavailable",
    };

    /// <summary>Records point-in-time guarded decisions; only configured route and closed mode/status/reason codes are labels.</summary>
    internal static void RecordGuarded(string route, string mode, string status, string reason, long? head, long? checkpoint)
    {
        if (mode is not ("current" or "tail" or "full_replay" or "mapped_replay" or "incremental_rebuild" or "reconciliation")
            || status is not ("completed" or "already_completed" or "retryable" or "failed" or "indeterminate" or "fenced")) return;
        try
        {
            reason = _reasons.Contains(reason) ? reason : "delivery_state_unavailable";
            var tags = new TagList { { RouteTag, route }, { ModeTag, mode }, { StatusTag, status }, { ReasonTag, reason } };
            _transitions.Add(1, tags);
            if (head is >= 0) _head.Record(head.Value, tags);
            if (checkpoint is >= 0) _checkpoint.Record(checkpoint.Value, tags);
            if (head is >= 0 && checkpoint is >= 0) _lag.Record(Math.Max(0, head.Value - checkpoint.Value), tags);
        }
        catch { /* Exporter failure cannot interrupt durable projection work. */ }
    }

    public static void RecordAdmission(string projectionType, ProjectionDeliveryAdmissionResult result) {
        ArgumentNullException.ThrowIfNull(result);
        string status = result.Disposition switch {
            ProjectionDeliveryAdmissionDisposition.Dispatch => "admitted",
            ProjectionDeliveryAdmissionDisposition.AlreadyCompleted => "already_completed",
            ProjectionDeliveryAdmissionDisposition.Retryable => "retryable",
            _ => "failed",
        };
        Record(
            AdmissionActivityName,
            projectionType,
            status,
            result.ReasonCode ?? "delivery_admitted");
    }

    public static void RecordCompletion(string projectionType, ProjectionDeliveryCompletion completion) {
        string status = completion switch {
            ProjectionDeliveryCompletion.Completed => "completed",
            ProjectionDeliveryCompletion.AlreadyCompleted => "already_completed",
            ProjectionDeliveryCompletion.Fenced => "fenced",
            _ => "state_unavailable",
        };
        string reason = completion switch {
            ProjectionDeliveryCompletion.Completed => "delivery_completed",
            ProjectionDeliveryCompletion.AlreadyCompleted => "delivery_already_completed",
            ProjectionDeliveryCompletion.Fenced => "delivery_fenced",
            _ => "delivery_state_unavailable",
        };
        Record(CompletionActivityName, projectionType, status, reason);
    }

    private static void Record(string activityName, string projectionType, string status, string reason) {
        reason = _reasons.Contains(reason) ? reason : "delivery_state_unavailable";
        var tags = new TagList {
            { RouteTag, projectionType },
            { StatusTag, status },
            { ReasonTag, reason },
        };
        try {
        _transitions.Add(1, tags);
        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(activityName, ActivityKind.Internal);
        _ = activity?.SetTag(RouteTag, projectionType);
        _ = activity?.SetTag(StatusTag, status);
        _ = activity?.SetTag(ReasonTag, reason);
        }
        catch { /* Exporter failure cannot interrupt durable projection work. */ }
    }
}
