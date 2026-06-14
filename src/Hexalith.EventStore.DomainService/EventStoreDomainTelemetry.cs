using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Convention that derives a domain module's OpenTelemetry instrument names from its (kebab-case) domain name,
/// so a domain no longer hand-declares an <c>ActivitySource</c>/<c>Meter</c> and a per-domain telemetry class
/// (Epic A5). The platform owns the naming so every domain's traces and metrics are discoverable under a
/// predictable prefix.
/// </summary>
public static class EventStoreDomainTelemetry {
    /// <summary>The common prefix for every domain module's telemetry instrument names.</summary>
    public const string Prefix = "Hexalith.EventStore.Domain";

    /// <summary>Gets the conventional <see cref="ActivitySource"/> name for a domain (e.g. <c>Hexalith.EventStore.Domain.counter</c>).</summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <returns>The conventional activity-source name.</returns>
    public static string ActivitySourceName(string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return $"{Prefix}.{domain.Trim()}";
    }

    /// <summary>Gets the conventional <see cref="Meter"/> name for a domain. Matches <see cref="ActivitySourceName"/>.</summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <returns>The conventional meter name.</returns>
    public static string MeterName(string domain) => ActivitySourceName(domain);

    /// <summary>Gets the conventional health-check registration name for a domain's DAPR state store.</summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <returns>The conventional health-check name (e.g. <c>dapr-statestore-counter</c>).</returns>
    public static string StateStoreHealthCheckName(string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return $"dapr-statestore-{domain.Trim()}";
    }
}

/// <summary>
/// A domain module's OpenTelemetry instruments, named by the <see cref="EventStoreDomainTelemetry"/> convention.
/// Registered as a singleton by <see cref="EventStoreDomainTelemetryExtensions.AddEventStoreDomainTelemetry"/> and
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

/// <summary>
/// Registers convention-named OpenTelemetry instruments for a domain module (Epic A5).
/// </summary>
public static class EventStoreDomainTelemetryExtensions {
    /// <summary>
    /// Registers a singleton <see cref="EventStoreDomainDiagnostics"/> for the domain and wires its convention-named
    /// activity source and meter into the OpenTelemetry tracer/meter providers configured by
    /// <c>AddServiceDefaults</c>. Domain code injects <see cref="EventStoreDomainDiagnostics"/> to emit spans/metrics.
    /// </summary>
    /// <param name="builder">The web application builder (after <c>AddEventStoreDomainService</c>).</param>
    /// <param name="domain">The kebab-case domain name (e.g. <c>"tenants"</c>).</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="domain"/> is <c>null</c> or whitespace.</exception>
    public static WebApplicationBuilder AddEventStoreDomainTelemetry(this WebApplicationBuilder builder, string domain) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        _ = builder.Services.AddSingleton(new EventStoreDomainDiagnostics(domain));

        string sourceName = EventStoreDomainTelemetry.ActivitySourceName(domain);
        string meterName = EventStoreDomainTelemetry.MeterName(domain);

        _ = builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(sourceName));
        _ = builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(meterName));

        return builder;
    }
}
