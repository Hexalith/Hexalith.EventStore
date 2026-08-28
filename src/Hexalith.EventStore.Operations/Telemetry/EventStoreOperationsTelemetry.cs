using System.Diagnostics;
using System.Diagnostics.Metrics;

using Hexalith.EventStore.Operations.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Operations.Telemetry;

/// <summary>
/// Emits bounded-cardinality subscriber dead-letter metrics.
/// </summary>
public sealed class EventStoreOperationsTelemetry
{
    internal const string MeterName = "Hexalith.EventStore.Operations";

    private readonly Counter<long> _actions;
    private readonly Counter<long> _captures;
    private readonly object _observationLock = new();
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset? _oldestCapturedAtUtc;
    private long _openCount;
    private string _topic;

    internal (long Count, double OldestAgeSeconds) CurrentBacklog
    {
        get
        {
            lock (_observationLock)
            {
                return (_openCount, OldestAgeSeconds());
            }
        }
    }

    /// <summary>Initializes a telemetry recorder.</summary>
    /// <remarks>
    /// The backlog gauges are published from construction, before the drain actor has ever been activated, so the
    /// topic dimension is seeded from configuration rather than from the first observation. A restarted host that
    /// has not yet activated the actor therefore reports its backlog under the real topic instead of a placeholder
    /// series an alert rule cannot match.
    /// </remarks>
    public EventStoreOperationsTelemetry(
        IMeterFactory meterFactory,
        TimeProvider timeProvider,
        IOptions<EventStoreOperationsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentNullException.ThrowIfNull(options);
        _topic = options.Value.TopicName;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        Meter meter = meterFactory.Create(MeterName);
        _captures = meter.CreateCounter<long>("eventstore.operations.deadletter.capture");
        _actions = meter.CreateCounter<long>("eventstore.operations.deadletter.action");
        _ = meter.CreateObservableGauge(
            "eventstore.operations.deadletter.backlog.count",
            ObserveOpenCount);
        _ = meter.CreateObservableGauge(
            "eventstore.operations.deadletter.backlog.oldest_age_seconds",
            ObserveOldestAge);
    }

    /// <summary>Records a capture outcome without an item identifier.</summary>
    internal void Capture(string topic, string status)
        => _captures.Add(1, new KeyValuePair<string, object?>("topic", topic), new("status", status));

    /// <summary>Records an operator or replay action without an item identifier.</summary>
    internal void Action(string topic, string status, string reason)
        => _actions.Add(
            1,
            new KeyValuePair<string, object?>("topic", topic),
            new("status", status),
            new("reason", reason));

    /// <summary>Records a backlog observation using bounded topic and status dimensions.</summary>
    internal void SetBacklog(string topic, long count, DateTimeOffset? oldestCapturedAtUtc)
    {
        lock (_observationLock)
        {
            _topic = topic;
            _openCount = Math.Max(0, count);
            _oldestCapturedAtUtc = count > 0 ? oldestCapturedAtUtc : null;
        }
    }

    private Measurement<double> ObserveOldestAge()
    {
        lock (_observationLock)
        {
            return new Measurement<double>(OldestAgeSeconds(), Tags(_topic));
        }
    }

    private Measurement<long> ObserveOpenCount()
    {
        lock (_observationLock)
        {
            return new Measurement<long>(_openCount, Tags(_topic));
        }
    }

    private static TagList Tags(string topic) => new()
    {
        { "topic", topic },
        { "status", "open" },
    };

    private double OldestAgeSeconds()
        => _oldestCapturedAtUtc is null
            ? 0
            : Math.Max(0, (_timeProvider.GetUtcNow() - _oldestCapturedAtUtc.Value).TotalSeconds);
}
