using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Warns once at startup when the actor host has no folded-snapshot enrollment.</summary>
internal sealed partial class FoldedSnapshotQualificationHostedService(
    IServiceProviderIsService services,
    ILogger<FoldedSnapshotQualificationHostedService> logger) : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!services.IsService(typeof(FoldedSnapshotQualificationRegistry)))
        {
            QualificationAbsent(logger);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 7105,
        Level = LogLevel.Warning,
        Message = "Folded snapshot qualification is not registered on the actor host; automatic snapshots are skipped and manual snapshot creation is unsupported. Enroll reviewed profiles on both hosts. Legacy history-bearing snapshots require full event replay. FailureClass=SnapshotCaptureUnsupported, Stage=FoldedSnapshotQualificationAbsent")]
    private static partial void QualificationAbsent(ILogger logger);
}
