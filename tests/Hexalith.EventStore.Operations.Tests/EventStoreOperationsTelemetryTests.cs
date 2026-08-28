using System.Diagnostics.Metrics;

using Hexalith.EventStore.Operations.Telemetry;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies operations metrics cannot acquire identifier-cardinality dimensions.
/// </summary>
public sealed class EventStoreOperationsTelemetryTests
{
    /// <summary>Verifies every emitted dimension is bounded to topic, status, or reason.</summary>
    [Fact]
    public void MetricDimensionsAreBoundedAndContainNoIdentifiers()
    {
        var tagSets = new List<string[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (string.Equals(instrument.Meter.Name, EventStoreOperationsTelemetry.MeterName, StringComparison.Ordinal))
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            tagSets.Add([.. tags.ToArray().Select(static tag => tag.Key).Order(StringComparer.Ordinal)]));
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            tagSets.Add([.. tags.ToArray().Select(static tag => tag.Key).Order(StringComparer.Ordinal)]));
        listener.Start();

        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var telemetry = new EventStoreOperationsTelemetry(services.GetRequiredService<IMeterFactory>(), TimeProvider.System);
        telemetry.Capture("deadletter.work.events", "captured");
        telemetry.Action("deadletter.work.events", "retryable", "timeout");
        telemetry.SetBacklog("deadletter.work.events", 2, DateTimeOffset.UtcNow.AddSeconds(-60));
        listener.RecordObservableInstruments();

        tagSets.Count.ShouldBeGreaterThanOrEqualTo(4);
        foreach (string[] keys in tagSets)
        {
            keys.All(static key => key is "topic" or "status" or "reason").ShouldBeTrue();
        }
        tagSets.SelectMany(static keys => keys).ShouldNotContain("messageId");
        tagSets.SelectMany(static keys => keys).ShouldNotContain("tenantId");
        tagSets.SelectMany(static keys => keys).ShouldNotContain("aggregateId");
    }

    /// <summary>Verifies backlog observations expose the latest global values rather than accumulating samples.</summary>
    [Fact]
    public void BacklogGaugesReportCurrentValues()
    {
        var counts = new List<long>();
        var ages = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (!string.Equals(instrument.Meter.Name, EventStoreOperationsTelemetry.MeterName, StringComparison.Ordinal))
            {
                return;
            }

            if (instrument.Name.EndsWith("backlog.count", StringComparison.Ordinal))
            {
                currentListener.EnableMeasurementEvents(instrument, "count");
            }
            else if (instrument.Name.EndsWith("oldest_age_seconds", StringComparison.Ordinal))
            {
                currentListener.EnableMeasurementEvents(instrument, "age");
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, state) =>
        {
            if (string.Equals(state as string, "count", StringComparison.Ordinal))
            {
                counts.Add(value);
            }
        });
        listener.SetMeasurementEventCallback<double>((_, value, _, state) =>
        {
            if (string.Equals(state as string, "age", StringComparison.Ordinal))
            {
                ages.Add(value);
            }
        });
        listener.Start();

        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero));
        var telemetry = new EventStoreOperationsTelemetry(services.GetRequiredService<IMeterFactory>(), timeProvider);
        telemetry.SetBacklog("deadletter.work.events", 4, timeProvider.GetUtcNow().AddSeconds(-90));
        telemetry.SetBacklog("deadletter.work.events", 2, timeProvider.GetUtcNow().AddSeconds(-30));
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        listener.RecordObservableInstruments();

        counts.ShouldContain(2);
        ages.ShouldContain(60);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan value) => _now += value;
    }
}
