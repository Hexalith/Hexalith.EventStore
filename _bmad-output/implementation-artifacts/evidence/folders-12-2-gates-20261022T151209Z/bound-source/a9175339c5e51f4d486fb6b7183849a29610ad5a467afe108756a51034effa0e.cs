using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>
/// Shuts the OpenTelemetry providers down under a bounded budget while the host is still stopping, so an
/// unreachable OTLP collector cannot make the process unstoppable.
/// </summary>
/// <remarks>
/// <para>
/// Provider disposal calls <c>BaseProcessor.Shutdown()</c> with no timeout, which reaches
/// <c>BatchExportThreadWorker.Shutdown(-1)</c> and performs an unbounded <c>Thread.Join()</c> on the exporter
/// thread. When the OTLP endpoint is unreachable that thread is parked in the exporter's retry handler
/// (<c>Thread.Sleep</c>), so the join never completes: <c>IHost.DisposeAsync</c> blocks forever and the process
/// never exits. <c>ShutdownTimeout</c> does not help — it bounds <c>StopAsync</c>, not disposal.
/// </para>
/// <para>
/// Observed as an Aspire/DCP <c>stop</c> command failing with "timed out waiting for process to stop" while the
/// application had already logged "Application is shutting down..." and then gone silent. That makes the service
/// impossible to stop from the orchestration layer, which any continuity or recovery drill depends on.
/// </para>
/// <para>
/// <c>BaseProcessor.Shutdown</c> is guarded by an interlocked one-shot counter, so calling it here with a finite
/// budget makes the later disposal a no-op instead of an unbounded join. Telemetry still flushes normally when
/// the collector is reachable; only the pathological case is bounded.
/// </para>
/// </remarks>
internal sealed partial class BoundedTelemetryShutdownService(
    ILogger<BoundedTelemetryShutdownService> logger,
    TracerProvider? tracerProvider = null,
    MeterProvider? meterProvider = null,
    LoggerProvider? loggerProvider = null) : IHostedService {
    /// <summary>The budget allowed to each provider's shutdown before the process is allowed to exit anyway.</summary>
    internal static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>
    /// Each provider shutdown is a blocking call, so three of them ran inline for up to six seconds while ignoring
    /// the token the host supplies — a host configured with a shorter shutdown timeout could not honour it. They
    /// now run off the calling thread and the wait observes the token.
    /// <para>
    /// Abandoning the wait is safe and still achieves the point of this service: <c>BaseProcessor.Shutdown</c>
    /// takes its one-shot interlock on entry, so the shutdown is already claimed by the time the token fires and
    /// the later disposal returns immediately instead of joining the exporter thread without a timeout.
    /// </para>
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken) {
        int budgetMilliseconds = (int)ShutdownBudget.TotalMilliseconds;
        Task shutdown = Task.Run(
            () => {
                ShutdownProvider("tracer", () => tracerProvider?.Shutdown(budgetMilliseconds));
                ShutdownProvider("meter", () => meterProvider?.Shutdown(budgetMilliseconds));
                ShutdownProvider("logger", () => loggerProvider?.Shutdown(budgetMilliseconds));
            },
            CancellationToken.None);

        try {
            await shutdown.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            // The host wants to stop now. The interlock is already taken, so disposal stays bounded.
            Log.TelemetryShutdownAbandoned(logger, (int)ShutdownBudget.TotalMilliseconds);
        }
    }

    private void ShutdownProvider(string provider, Func<bool?> shutdown) {
        try {
            if (shutdown() == false) {
                Log.TelemetryShutdownIncomplete(logger, provider, (int)ShutdownBudget.TotalMilliseconds);
            }
        }
        catch (Exception ex) {
            // Telemetry shutdown must never be able to fail the host stop it is protecting.
            Log.TelemetryShutdownFaulted(logger, provider, ex.GetType().Name);
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 6200,
            Level = LogLevel.Warning,
            Message = "Telemetry provider did not drain inside its shutdown budget; the process exits anyway: Provider={Provider}, BudgetMs={BudgetMs}, Stage=BoundedTelemetryShutdownIncomplete")]
        public static partial void TelemetryShutdownIncomplete(ILogger logger, string provider, int budgetMs);

        [LoggerMessage(
            EventId = 6202,
            Level = LogLevel.Warning,
            Message = "Host stop was cancelled before telemetry providers finished draining; disposal stays bounded because the shutdown interlock is already taken: BudgetMs={BudgetMs}, Stage=BoundedTelemetryShutdownAbandoned")]
        public static partial void TelemetryShutdownAbandoned(ILogger logger, int budgetMs);

        [LoggerMessage(
            EventId = 6201,
            Level = LogLevel.Warning,
            Message = "Telemetry provider shutdown threw and was ignored so host stop can complete: Provider={Provider}, ExceptionType={ExceptionType}, Stage=BoundedTelemetryShutdownFaulted")]
        public static partial void TelemetryShutdownFaulted(ILogger logger, string provider, string exceptionType);
    }
}
