using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.DomainService;

/// <summary>Reports missing handler telemetry metadata when the host starts.</summary>
/// <param name="registrations">Omissions recorded by domain-service discovery.</param>
/// <param name="logger">The host logger.</param>
internal sealed partial class DomainHandlerTelemetryDiagnostics(
    IEnumerable<MissingHandlerTelemetryAttributes> registrations,
    ILogger<DomainHandlerTelemetryDiagnostics> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (Type handler in registrations.SelectMany(static registration => registration.HandlerTypes).Distinct())
        {
            LogMissingDomainAttribute(logger, handler.FullName ?? handler.Name);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Domain handler {HandlerType} has no EventStoreDomain attribute. Add the attribute to discover its domain for telemetry.")]
    private static partial void LogMissingDomainAttribute(ILogger logger, string handlerType);
}
