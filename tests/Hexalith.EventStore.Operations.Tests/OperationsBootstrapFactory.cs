using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>Supplies actual private-run inputs before the minimal Program creates its service registrations.</summary>
internal sealed class OperationsBootstrapFactory(
    IReadOnlyDictionary<string, string?> configuration,
    bool disposeStoppedHostBeforeClientLookup = false) : WebApplicationFactory<Program>
{
    /// <summary>Gets the actual receiver captured before application startup can stop the host.</summary>
    internal FiniteRouteCatalogReceiver CapturedReceiver { get; private set; } = null!;

    /// <summary>Gets the actual host stopping token captured before startup.</summary>
    internal CancellationToken CapturedStoppingToken { get; private set; }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(configuration));
        IHost host = builder.Build();
        CapturedReceiver = host.Services.GetRequiredService<FiniteRouteCatalogReceiver>();
        CapturedStoppingToken = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = CapturedStoppingToken.Register(() => stopping.TrySetResult());
        host.Start();
        if (disposeStoppedHostBeforeClientLookup)
        {
            // Wait for the actual SDK refusal to request shutdown, then force the observed
            // factory lookup to occur after disposal. Never synthesize a stopping signal.
            stopping.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            host.Dispose();
        }
        return host;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        OperationsTestCredentials.Configure(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
    }
}
