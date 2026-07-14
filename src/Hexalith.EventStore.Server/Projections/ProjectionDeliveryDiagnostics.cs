using System.Diagnostics;
using System.Diagnostics.Metrics;

using Hexalith.EventStore.Server.Telemetry;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Emits bounded, scope-free projection delivery outcome telemetry.</summary>
internal static class ProjectionDeliveryDiagnostics {
    internal const string AdmissionActivityName = "EventStore.Projection.DeliveryAdmission";
    internal const string CompletionActivityName = "EventStore.Projection.DeliveryCompletion";
    internal const string CounterName = "eventstore.projection.delivery.transitions";
    internal const string RouteTag = "projection.route";
    internal const string StatusTag = "projection.status";
    internal const string ReasonTag = "projection.reason";

    private static readonly Meter _meter = new(EventStoreActivitySource.SourceName);
    private static readonly Counter<long> _transitions = _meter.CreateCounter<long>(CounterName);

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
        var tags = new TagList {
            { RouteTag, projectionType },
            { StatusTag, status },
            { ReasonTag, reason },
        };
        _transitions.Add(1, tags);
        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(activityName, ActivityKind.Internal);
        _ = activity?.SetTag(RouteTag, projectionType);
        _ = activity?.SetTag(StatusTag, status);
        _ = activity?.SetTag(ReasonTag, reason);
    }
}
