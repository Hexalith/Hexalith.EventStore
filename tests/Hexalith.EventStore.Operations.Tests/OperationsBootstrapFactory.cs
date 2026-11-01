using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>Supplies actual private-run inputs before the minimal Program creates its service registrations.</summary>
internal sealed class OperationsBootstrapFactory(IReadOnlyDictionary<string, string?> configuration) : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(configuration));
        return base.CreateHost(builder);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        OperationsTestCredentials.Configure(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
    }
}
