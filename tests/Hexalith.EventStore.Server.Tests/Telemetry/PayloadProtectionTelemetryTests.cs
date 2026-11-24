using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.PayloadProtection.Tests;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>Exercises payload protection with the host's real telemetry providers.</summary>
[Collection("ReadDiagnosticsPrivacy")]
public sealed class PayloadProtectionTelemetryTests
{
    /// <summary>Verifies one protected write reaches both configured OpenTelemetry providers.</summary>
    [Fact]
    public async Task InteractionProtectionOptInCollectsCoreTracesAndMetricsAsync()
    {
        const string sourceName = "Hexalith.EventStore.PayloadProtection";
        var fixture = new InteractionPayloadProtectionHookFixture();
        var traces = new ReadPrivacyTraceExporter();
        var metrics = new PayloadProtectionMetricExporter(sourceName);
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
        builder.AddServiceDefaults();
        builder.Services.AddSingleton(fixture.Occurrences.Registry);
        builder.Services.AddSingleton<IInteractionOccurrenceKeyProvider>(fixture.Occurrences.Keys);
        builder.Services.AddSingleton(fixture.Bindings);
        builder.Services.AddInteractionPayloadProtection();
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddProcessor(new SimpleActivityExportProcessor(traces)));
        builder.Services.ConfigureOpenTelemetryMeterProvider(meter =>
            meter.AddReader(new PeriodicExportingMetricReader(metrics, int.MaxValue, 5000)));

        using IHost host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();
        MeterProvider meterProvider = host.Services.GetRequiredService<MeterProvider>();
        IEventPayloadProtectionService service = host.Services.GetRequiredService<IEventPayloadProtectionService>();
        PayloadProtectionWriteResult result = await service.ProtectEventPayloadAsync(
            fixture.Binding.Identity.Owner,
            Substitute.For<IEventPayload>(),
            fixture.Binding.Identity.PayloadTypeId,
            InteractionPayloadProtectionHookFixture.Payload(),
            "json",
            fixture.Occurrence,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.CompletionContext.ShouldNotBeNull();
        traces.Records.ShouldContain(record => IsProtectionSpan(record, sourceName));
        meterProvider.ForceFlush(5000).ShouldBeTrue();
        foreach (string instrument in new[] { "eventstore.payload_protection.operations", "eventstore.payload_protection.duration" })
        {
            metrics.Records.ShouldContain(record => record.Instrument == instrument
                && record.Tags.Count == 3
                && Equals(record.Tags.GetValueOrDefault("operation"), "protect")
                && Equals(record.Tags.GetValueOrDefault("result"), "success")
                && Equals(record.Tags.GetValueOrDefault("format_version"), "v2"));
        }
    }

    private static bool IsProtectionSpan(string record, string sourceName)
    {
        using JsonDocument span = JsonDocument.Parse(record);
        return span.RootElement.GetProperty("Source").GetString() == sourceName
            && span.RootElement.GetProperty("OperationName").GetString() == "EventStore.PayloadProtection.Protect";
    }
}
