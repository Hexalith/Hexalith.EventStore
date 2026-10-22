using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Telemetry;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        var tagSets = new ConcurrentQueue<string[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (string.Equals(instrument.Meter.Name, EventStoreOperationsTelemetry.MeterName, StringComparison.Ordinal))
            {
                currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            tagSets.Enqueue([.. tags.ToArray().Select(static tag => tag.Key).Order(StringComparer.Ordinal)]));
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            tagSets.Enqueue([.. tags.ToArray().Select(static tag => tag.Key).Order(StringComparer.Ordinal)]));
        listener.Start();

        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var telemetry = new EventStoreOperationsTelemetry(services.GetRequiredService<IMeterFactory>(), TimeProvider.System, Options.Create(new EventStoreOperationsOptions()));
        telemetry.Capture("deadletter.work.events", "captured");
        telemetry.Action("deadletter.work.events", "retryable", "timeout");
        telemetry.SetBacklog("deadletter.work.events", 2, DateTimeOffset.UtcNow.AddSeconds(-60));
        listener.RecordObservableInstruments();

        // MeterListener callbacks can arrive concurrently with the test thread under the MTP Tier-1 lane.
        // Assert over one immutable snapshot while the concurrent queue remains safe for any in-flight callback.
        string[][] capturedTagSets = tagSets.ToArray();
        capturedTagSets.Length.ShouldBeGreaterThanOrEqualTo(4);
        foreach (string[] keys in capturedTagSets)
        {
            keys.All(static key => key is "topic" or "status" or "reason").ShouldBeTrue();
        }
        capturedTagSets.SelectMany(static keys => keys).ShouldNotContain("messageId");
        capturedTagSets.SelectMany(static keys => keys).ShouldNotContain("tenantId");
        capturedTagSets.SelectMany(static keys => keys).ShouldNotContain("aggregateId");
    }

    /// <summary>Verifies backlog observations expose the latest global values rather than accumulating samples.</summary>
    [Fact]
    public void BacklogGaugesReportCurrentValues()
    {
        var counts = new ConcurrentQueue<long>();
        var ages = new ConcurrentQueue<double>();
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
                counts.Enqueue(value);
            }
        });
        listener.SetMeasurementEventCallback<double>((_, value, _, state) =>
        {
            if (string.Equals(state as string, "age", StringComparison.Ordinal))
            {
                ages.Enqueue(value);
            }
        });
        listener.Start();

        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero));
        var telemetry = new EventStoreOperationsTelemetry(services.GetRequiredService<IMeterFactory>(), timeProvider, Options.Create(new EventStoreOperationsOptions()));
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

    /// <summary>
    /// Verifies the backlog gauges carry the configured topic before the drain actor has ever been observed.
    /// </summary>
    /// <remarks>
    /// The gauges are published from construction. A host that restarts holding a captured-but-never-retried
    /// backlog does not activate the actor until something touches it, so a placeholder topic here would publish
    /// a series no alert rule matches for exactly the backlog nobody is watching.
    /// </remarks>
    [Fact]
    public void BacklogGaugesCarryTheConfiguredTopicBeforeAnyObservation()
    {
        var topics = new ConcurrentQueue<string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, active) => {
            if (instrument.Meter.Name == EventStoreOperationsTelemetry.MeterName
                && instrument.Name == "eventstore.operations.deadletter.backlog.count") {
                active.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            topics.Enqueue(tags.ToArray().Single(static tag => tag.Key == "topic").Value?.ToString()));
        listener.Start();

        using ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _ = new EventStoreOperationsTelemetry(
            services.GetRequiredService<IMeterFactory>(),
            TimeProvider.System,
            Options.Create(new EventStoreOperationsOptions { TopicName = "deadletter.telemetry-seed.test" }));

        listener.RecordObservableInstruments();

        // A distinct topic keeps the assertion independent of any other recorder publishing on the same meter
        // name while the suite runs in parallel. Before the topic was seeded from configuration, this gauge
        // published under a placeholder until the actor was first observed.
        topics.ShouldContain("deadletter.telemetry-seed.test");
    }
}
