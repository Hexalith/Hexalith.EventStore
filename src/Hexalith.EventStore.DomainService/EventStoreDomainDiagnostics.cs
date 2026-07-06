using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// A domain module's OpenTelemetry instruments, named by the <see cref="EventStoreDomainTelemetry"/> convention.
/// Registered as a singleton by <see cref="EventStoreDomainTelemetryExtensions.AddEventStoreDomainTelemetry(Microsoft.AspNetCore.Builder.WebApplicationBuilder, string)"/> and
/// injected by domain code to create spans and metrics without declaring its own source/meter.
/// </summary>
public sealed class EventStoreDomainDiagnostics : IDisposable {
    private readonly Histogram<double> _admissionStageDurationMilliseconds;

    /// <summary>Initializes the diagnostics for the given domain.</summary>
    /// <param name="domain">The kebab-case domain name.</param>
    public EventStoreDomainDiagnostics(string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        Domain = domain.Trim();
        ActivitySource = new ActivitySource(EventStoreDomainTelemetry.ActivitySourceName(Domain));
        Meter = new Meter(EventStoreDomainTelemetry.MeterName(Domain));
        _admissionStageDurationMilliseconds = Meter.CreateHistogram<double>(
            "eventstore.domain.admission.stage.duration",
            unit: "ms",
            description: "Duration of one DomainService admission stage evaluation.");
    }

    /// <summary>Gets the domain these instruments belong to.</summary>
    public string Domain { get; }

    /// <summary>Gets the domain's distributed-tracing source.</summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>Gets the domain's metrics meter.</summary>
    public Meter Meter { get; }

    /// <summary>Records metadata-only admission-stage telemetry.</summary>
    /// <param name="commandType">The command type name.</param>
    /// <param name="stageName">The admission stage name.</param>
    /// <param name="accepted">A value indicating whether the stage accepted the command.</param>
    /// <param name="duration">The stage evaluation duration.</param>
    public void RecordAdmissionStage(string commandType, string stageName, bool accepted, TimeSpan duration) {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);

        _admissionStageDurationMilliseconds.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("eventstore.domain", Domain),
            new KeyValuePair<string, object?>("eventstore.command.type", commandType),
            new KeyValuePair<string, object?>("eventstore.admission.stage", stageName),
            new KeyValuePair<string, object?>("eventstore.admission.accepted", accepted));
    }

    /// <inheritdoc/>
    public void Dispose() {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
