using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Runs one actual host's private Development activation after its application starts.</summary>
internal sealed class DevelopmentRouteCatalogBootstrapWorker(IServiceProvider provider, string directory, string host, string coordinator, string attempt,
    string[] hosts, Func<IServiceProvider, CancellationToken, Task<RouteCatalogHostObservation>> capture,
    Func<IReadOnlyList<RouteCatalogHostObservation>, RouteCatalogEnvelope>? construct, string? predecessorRootDigest) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!provider.GetRequiredService<IHostEnvironment>().IsDevelopment())
            throw new InvalidOperationException("Private Development catalog bootstrap cannot run in another environment.");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = provider.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
        await DevelopmentRouteCatalogBootstrap.ActivateAsync(provider, directory, host, coordinator, attempt, hosts, capture, construct, stoppingToken, predecessorRootDigest).ConfigureAwait(false);
        provider.GetRequiredService<ILogger<DevelopmentRouteCatalogBootstrapWorker>>().LogInformation("Development catalog host {HostId} committed its independently observed generation.", host);
    }
}
