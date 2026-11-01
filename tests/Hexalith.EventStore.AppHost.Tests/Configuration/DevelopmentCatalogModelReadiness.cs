using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Runs only native model readiness monitoring and stops it before its service provider is disposed.</summary>
internal sealed class DevelopmentCatalogModelReadiness : IAsyncDisposable
{
    private readonly IHostedService _service;

    private DevelopmentCatalogModelReadiness(IHostedService service) => _service = service;

    /// <summary>Starts Aspire's readiness monitor without starting DCP or application resources.</summary>
    /// <param name="services">The model application's services.</param>
    /// <param name="token">The startup cancellation token.</param>
    /// <returns>The scope that awaits monitor shutdown.</returns>
    internal static async Task<DevelopmentCatalogModelReadiness> StartAsync(IServiceProvider services, CancellationToken token)
    {
        var service = services.GetServices<IHostedService>().Single(service => service.GetType().Name == "ResourceHealthCheckService");
        await service.StartAsync(token).ConfigureAwait(false);
        return new(service);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _service.StopAsync(CancellationToken.None).ConfigureAwait(false);
}
