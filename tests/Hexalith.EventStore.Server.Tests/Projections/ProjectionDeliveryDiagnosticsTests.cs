using System.Diagnostics;
using System.Diagnostics.Metrics;

using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Telemetry;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionDeliveryDiagnosticsTests {
    [Fact]
    public void AdmissionTelemetry_ContainsOnlyBoundedRouteStatusAndReasonMetadata() {
        var measurements = new List<KeyValuePair<string, object?>[]>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) => {
            if (instrument.Name == ProjectionDeliveryDiagnostics.CounterName) {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Add(tags.ToArray()));
        meterListener.Start();
        Activity? stopped = null;
        using var activityListener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(activityListener);

        ProjectionDeliveryDiagnostics.RecordAdmission(
            "order-detail",
            new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.Retryable,
                "delivery_gap",
                null));

        KeyValuePair<string, object?>[] tags = measurements.ShouldHaveSingleItem();
        tags.Select(static tag => tag.Key).ShouldBe([
            ProjectionDeliveryDiagnostics.RouteTag,
            ProjectionDeliveryDiagnostics.StatusTag,
            ProjectionDeliveryDiagnostics.ReasonTag,
        ]);
        stopped.ShouldNotBeNull();
        stopped.OperationName.ShouldBe(ProjectionDeliveryDiagnostics.AdmissionActivityName);
        string[] allowedTags = [
            ProjectionDeliveryDiagnostics.RouteTag,
            ProjectionDeliveryDiagnostics.StatusTag,
            ProjectionDeliveryDiagnostics.ReasonTag,
        ];
        stopped.Tags.ShouldAllBe(tag => allowedTags.Contains(tag.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(0, null, "admitted", "delivery_admitted")]
    [InlineData(1, "delivery_already_completed", "already_completed", "delivery_already_completed")]
    [InlineData(2, "delivery_gap", "retryable", "delivery_gap")]
    [InlineData(3, "delivery_identity_conflict", "failed", "delivery_identity_conflict")]
    public void AdmissionOutcome_MapsToExactBoundedStatusAndReason(
        int disposition,
        string? reason,
        string expectedStatus,
        string expectedReason) {
        const string route = "diagnostic-admission-route";
        var measurements = new List<KeyValuePair<string, object?>[]>();
        using var meterListener = CreateMeterListener(measurements);
        Activity? stopped = null;
        using var activityListener = CreateActivityListener(
            ProjectionDeliveryDiagnostics.AdmissionActivityName,
            route,
            activity => stopped = activity);

        ProjectionDeliveryDiagnostics.RecordAdmission(
            route,
            new ProjectionDeliveryAdmissionResult((ProjectionDeliveryAdmissionDisposition)disposition, reason, null));

        KeyValuePair<string, object?>[] metricTags = measurements
            .Single(tags => tags.Any(tag => tag.Key == ProjectionDeliveryDiagnostics.RouteTag
                && string.Equals(tag.Value as string, route, StringComparison.Ordinal)));
        metricTags.Single(tag => tag.Key == ProjectionDeliveryDiagnostics.StatusTag).Value.ShouldBe(expectedStatus);
        metricTags.Single(tag => tag.Key == ProjectionDeliveryDiagnostics.ReasonTag).Value.ShouldBe(expectedReason);
        stopped.ShouldNotBeNull();
        stopped.GetTagItem(ProjectionDeliveryDiagnostics.StatusTag).ShouldBe(expectedStatus);
        stopped.GetTagItem(ProjectionDeliveryDiagnostics.ReasonTag).ShouldBe(expectedReason);
    }

    [Theory]
    [InlineData(0, "completed", "delivery_completed")]
    [InlineData(1, "already_completed", "delivery_already_completed")]
    [InlineData(2, "fenced", "delivery_fenced")]
    [InlineData(3, "state_unavailable", "delivery_state_unavailable")]
    public void CompletionOutcome_MapsToExactBoundedStatusAndReason(
        int completion,
        string expectedStatus,
        string expectedReason) {
        const string route = "diagnostic-completion-route";
        var measurements = new List<KeyValuePair<string, object?>[]>();
        using var meterListener = CreateMeterListener(measurements);
        Activity? stopped = null;
        using var activityListener = CreateActivityListener(
            ProjectionDeliveryDiagnostics.CompletionActivityName,
            route,
            activity => stopped = activity);

        ProjectionDeliveryDiagnostics.RecordCompletion(route, (ProjectionDeliveryCompletion)completion);

        KeyValuePair<string, object?>[] metricTags = measurements
            .Single(tags => tags.Any(tag => tag.Key == ProjectionDeliveryDiagnostics.RouteTag
                && string.Equals(tag.Value as string, route, StringComparison.Ordinal)));
        metricTags.Single(tag => tag.Key == ProjectionDeliveryDiagnostics.StatusTag).Value.ShouldBe(expectedStatus);
        metricTags.Single(tag => tag.Key == ProjectionDeliveryDiagnostics.ReasonTag).Value.ShouldBe(expectedReason);
        stopped.ShouldNotBeNull();
        stopped.GetTagItem(ProjectionDeliveryDiagnostics.StatusTag).ShouldBe(expectedStatus);
        stopped.GetTagItem(ProjectionDeliveryDiagnostics.ReasonTag).ShouldBe(expectedReason);
    }

    private static ActivityListener CreateActivityListener(
        string operationName,
        string route,
        Action<Activity> onStopped) {
        var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.OperationName == operationName
                    && string.Equals(activity.GetTagItem(ProjectionDeliveryDiagnostics.RouteTag) as string, route, StringComparison.Ordinal)) {
                    onStopped(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener CreateMeterListener(List<KeyValuePair<string, object?>[]> measurements) {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) => {
            if (instrument.Name == ProjectionDeliveryDiagnostics.CounterName) {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Add(tags.ToArray()));
        listener.Start();
        return listener;
    }
}
