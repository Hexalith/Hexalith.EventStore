using System.Diagnostics.Metrics;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionGuardAttemptDiagnosticsTests
{
    [Theory]
    [InlineData(false, "head_changed", 3, 1)]
    [InlineData(true, "delivery_state_unavailable", 2, 0)]
    public async Task PostCompletionSourceObservation_PreservesActualCompletedCheckpointAndLatestKnownHead(bool unavailable,
        string reason, long head, long lag)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        fixture.AfterCompletionSaveAsync = unavailable ? () =>
        {
            fixture.InspectionFailure = new IOException("sensitive-post-completion-provider");
            return Task.CompletedTask;
        } : fixture.AppendPersistedSourceEventAsync;
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var measured = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Name.StartsWith("eventstore.projection.delivery.", StringComparison.Ordinal)) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            Dictionary<string, object?> copied = tags.ToArray().ToDictionary(static pair => pair.Key, static pair => pair.Value);
            if (copied.GetValueOrDefault(ProjectionDeliveryDiagnostics.RouteTag) as string == route
                && copied.ContainsKey(ProjectionDeliveryDiagnostics.ModeTag)) measured.Add((instrument.Name, value, copied));
        });
        listener.Start();
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe(reason);
        measured.Single(item => item.Name == ProjectionDeliveryDiagnostics.CounterName).Tags[ProjectionDeliveryDiagnostics.ReasonTag].ShouldBe(reason);
        measured.Single(item => item.Name.EndsWith(".head", StringComparison.Ordinal)).Value.ShouldBe(head);
        measured.Single(item => item.Name.EndsWith(".checkpoint", StringComparison.Ordinal)).Value.ShouldBe(2);
        measured.Single(item => item.Name.EndsWith(".lag", StringComparison.Ordinal)).Value.ShouldBe(lag);
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(2);
        fixture.Rows[route].ActiveReservation.ShouldBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(1);
        JsonSerializer.Serialize(measured).ShouldNotContain("sensitive-");
    }

    [Theory]
    [InlineData("source", "delivery_state_unavailable", false, true, "reconciliation")]
    [InlineData("row", "guard_activation_required", true, false, "reconciliation")]
    [InlineData("admission", "source_conflict", true, true, "tail")]
    [InlineData("invalid-row", "delivery_reconciliation_required", true, false, "reconciliation")]
    [InlineData("completed", "delivery_completed", true, true, "tail")]
    public async Task GuardAttempt_ReportsEachRouteOnceUsingOnlyActuallyObservedHeadAndCheckpoint(string failure, string reason,
        bool knownHead, bool knownCheckpoint, string mode)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        if (failure == "source") fixture.InspectionFailure = new IOException("sensitive-source-provider-detail");
        if (failure == "row") fixture.StateReadFailure = new JsonException("sensitive-row-provider-detail");
        if (failure == "admission") fixture.Events[1] = fixture.Events[1] with { UserId = "changed-before-admission" };
        if (failure == "invalid-row") fixture.Rows[route] = fixture.Rows[route] with { CompletedPrefixFingerprint = "corrupt" };
        var measurements = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Name.StartsWith("eventstore.projection.delivery.", StringComparison.Ordinal))
                current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            Dictionary<string, object?> copied = tags.ToArray().ToDictionary(static pair => pair.Key, static pair => pair.Value);
            if (copied.GetValueOrDefault(ProjectionDeliveryDiagnostics.RouteTag) as string == route
                && copied.ContainsKey(ProjectionDeliveryDiagnostics.ModeTag)) measurements.Add((instrument.Name, value, copied));
        });
        listener.Start();
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256);
        result.AlreadyCurrent.ShouldBe(failure == "completed");
        var outcome = measurements.Single(item => item.Name == ProjectionDeliveryDiagnostics.CounterName);
        outcome.Value.ShouldBe(1);
        outcome.Tags[ProjectionDeliveryDiagnostics.ReasonTag].ShouldBe(reason);
        outcome.Tags[ProjectionDeliveryDiagnostics.ModeTag].ShouldBe(mode);
        outcome.Tags.Keys.Order().ShouldBe(new[] { ProjectionDeliveryDiagnostics.RouteTag, ProjectionDeliveryDiagnostics.StatusTag,
            ProjectionDeliveryDiagnostics.ReasonTag, ProjectionDeliveryDiagnostics.ModeTag }.Order());
        measurements.Count(item => item.Name.EndsWith(".head", StringComparison.Ordinal)).ShouldBe(knownHead ? 1 : 0);
        measurements.Count(item => item.Name.EndsWith(".checkpoint", StringComparison.Ordinal)).ShouldBe(knownCheckpoint ? 1 : 0);
        measurements.Count(item => item.Name.EndsWith(".lag", StringComparison.Ordinal)).ShouldBe(knownHead && knownCheckpoint ? 1 : 0);
        if (knownHead) measurements.Single(item => item.Name.EndsWith(".head", StringComparison.Ordinal)).Value.ShouldBe(2);
        if (knownCheckpoint) measurements.Single(item => item.Name.EndsWith(".checkpoint", StringComparison.Ordinal)).Value.ShouldBe(failure == "completed" ? 2 : 1);
        JsonSerializer.Serialize(measurements).ShouldNotContain("sensitive-");
        if (failure != "completed")
        {
            fixture.Handlers.Single().Calls.ShouldBe(0);
            fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        }
    }
}
