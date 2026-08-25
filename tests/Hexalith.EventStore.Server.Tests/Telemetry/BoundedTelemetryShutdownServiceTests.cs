using System.Diagnostics;

using Hexalith.EventStore.ServiceDefaults;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using OpenTelemetry;
using OpenTelemetry.Trace;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;

/// <summary>
/// Regression coverage for the shutdown hang that made this service impossible to stop from the orchestration
/// layer. Provider disposal calls <c>Shutdown()</c> with <c>Timeout.Infinite</c>, which joins the batch exporter
/// thread with no timeout; when the OTLP collector is unreachable that thread is parked in the exporter's retry
/// sleep, so <c>IHost.DisposeAsync</c> never returns. Aspire/DCP then reports "timed out waiting for process to
/// stop" and the resource cannot be stopped at all — which every continuity and recovery drill depends on.
/// </summary>
public sealed class BoundedTelemetryShutdownServiceTests {
    private static readonly TimeSpan ExporterBlock = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task StopAsync_BoundsShutdownWhenTheExporterCannotDrain() {
        using BlockingExporter exporter = new(ExporterBlock);
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(nameof(BoundedTelemetryShutdownServiceTests))
            .AddProcessor(new BatchActivityExportProcessor(exporter))
            .Build()!;
        EmitActivity(provider);
        exporter.WaitUntilBlocked();

        BoundedTelemetryShutdownService service = new(NullLogger<BoundedTelemetryShutdownService>.Instance, provider);
        Stopwatch timer = Stopwatch.StartNew();
        await service.StopAsync(TestContext.Current.CancellationToken);
        timer.Stop();

        // The bounded shutdown must give up on the parked exporter thread instead of joining it forever. The
        // bound is the budget, not the exporter's block; generous slack keeps the assertion CI-stable.
        timer.Elapsed.ShouldBeLessThan(BoundedTelemetryShutdownService.ShutdownBudget * 3);
    }

    [Fact]
    public async Task StopAsync_MakesTheSubsequentDisposeNonBlocking() {
        using BlockingExporter exporter = new(ExporterBlock);
        TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(nameof(BoundedTelemetryShutdownServiceTests))
            .AddProcessor(new BatchActivityExportProcessor(exporter))
            .Build()!;
        EmitActivity(provider);
        exporter.WaitUntilBlocked();

        BoundedTelemetryShutdownService service = new(NullLogger<BoundedTelemetryShutdownService>.Instance, provider);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // This is the actual hang: without the bounded shutdown above, Dispose joins the parked exporter thread
        // with no timeout inside IHost.DisposeAsync. Shutdown is one-shot, so the bounded call disarms it.
        Stopwatch timer = Stopwatch.StartNew();
        provider.Dispose();
        timer.Stop();

        // Shutdown is one-shot, so after the bounded call this must be effectively instant. Asserting merely
        // "faster than the exporter block" would still pass on the unbounded join this test exists to catch.
        timer.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StopAsync_ReturnsPromptlyWhenTheHostCancelsTheStop() {
        using BlockingExporter exporter = new(ExporterBlock);
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(nameof(BoundedTelemetryShutdownServiceTests))
            .AddProcessor(new BatchActivityExportProcessor(exporter))
            .Build()!;
        EmitActivity(provider);
        exporter.WaitUntilBlocked();

        BoundedTelemetryShutdownService service = new(NullLogger<BoundedTelemetryShutdownService>.Instance, provider);
        using CancellationTokenSource hostStop = new();
        await hostStop.CancelAsync();

        // A host with a shorter shutdown timeout must be able to stop; the blocking provider calls previously ran
        // inline and ignored this token entirely.
        Stopwatch timer = Stopwatch.StartNew();
        await service.StopAsync(hostStop.Token);
        timer.Stop();

        // Deliberately tighter than the budget: an inline shutdown would join the parked exporter for the whole
        // 2s tracer budget before returning, so "under the budget" would not be falsifiable.
        timer.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StopAsync_IsANoOpWhenNoProviderIsRegistered() {
        BoundedTelemetryShutdownService service = new(NullLogger<BoundedTelemetryShutdownService>.Instance);

        await Should.NotThrowAsync(() => service.StopAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ConfigureOpenTelemetry_RegistersTheBoundedShutdownOnlyWithTheOtlpExporter() {
        // The hosted service exists for the exporter, and only the exporter can block disposal.
        HostApplicationBuilder configured = Host.CreateApplicationBuilder();
        configured.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:4317";
        _ = configured.ConfigureOpenTelemetry();
        HostedServiceCount(configured.Services).ShouldBe(1);

        HostApplicationBuilder unconfigured = Host.CreateApplicationBuilder();
        _ = unconfigured.ConfigureOpenTelemetry();
        HostedServiceCount(unconfigured.Services).ShouldBe(0);
    }

    private static int HostedServiceCount(IServiceCollection services)
        => services.Count(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(BoundedTelemetryShutdownService));

    private static void EmitActivity(TracerProvider provider) {
        using ActivitySource source = new(nameof(BoundedTelemetryShutdownServiceTests));
        using Activity? activity = source.StartActivity("blocked-export");
        activity?.Stop();
        _ = provider.ForceFlushAsyncSafe();
    }

    /// <summary>Stands in for the OTLP exporter parked in its retry sleep against an unreachable collector.</summary>
    private sealed class BlockingExporter(TimeSpan block) : BaseExporter<Activity> {
        private readonly ManualResetEventSlim _entered = new(false);

        /// <summary>Blocks until the exporter has actually parked, so the timing assertions are not vacuous.</summary>
        public void WaitUntilBlocked()
            => _entered
                .Wait(TimeSpan.FromSeconds(10))
                .ShouldBeTrue("the exporter never parked, so the timing assertions would pass trivially.");

        public override ExportResult Export(in Batch<Activity> batch) {
            _entered.Set();
            Thread.Sleep(block);
            return ExportResult.Success;
        }

        protected override void Dispose(bool disposing) {
            // Deliberately does NOT dispose the entered-signal: the exporter thread is still parked in its sleep
            // and will Set() it afterwards, which would throw ObjectDisposedException on a pool thread.
            base.Dispose(disposing);
        }
    }
}

/// <summary>Fires a flush without blocking the caller on the deliberately parked exporter.</summary>
internal static class BoundedTelemetryShutdownTestExtensions {
    public static Task ForceFlushAsyncSafe(this TracerProvider provider)
        => Task.Run(() => provider.ForceFlush(0));
}
