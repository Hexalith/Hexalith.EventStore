using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Combines the real HTTP catalog state reader with a private test backend and current owner observations.</summary>
internal sealed class ReleaseBindingHttpFixture : IDisposable
{
    private readonly HttpClient _client;

    private ReleaseBindingHttpFixture(RouteCatalogTestFixture catalog, RouteCatalogActivationState initial)
    {
        Catalog = catalog;
        Backend = new(initial);
        _client = new(Backend) { Timeout = TimeSpan.FromSeconds(10) };
        var clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient("route-catalog-state").Returns(_client);
        Options = new() { Identity = catalog.Hosts[0], Profile = "Development", DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true, ScopedDecodeReuse = true, RetainValidatedCatalogBytes = true };
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DAPR_HTTP_ENDPOINT"] = catalog.Store.Coordinates.HttpEndpoint }).Build();
        Store = new(clients, configuration, options);
        Environment = Substitute.For<IHostEnvironment>();
        Environment.EnvironmentName.Returns(Environments.Development);
        Authority = new(Store, new DevelopmentRouteCatalogTrustValidator(options, Environment), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(catalog.Observations[0])), environment: Environment);
    }

    /// <summary>Gets the independent compiled observation fixture.</summary>
    internal RouteCatalogTestFixture Catalog { get; }
    /// <summary>Gets the test-owned HTTP backend.</summary>
    internal ReleaseBindingHttpHandler Backend { get; }
    /// <summary>Gets the actual Dapr HTTP store under test.</summary>
    internal DaprRouteCatalogStateStore Store { get; }
    /// <summary>Gets the actual issuing authority.</summary>
    internal RouteCatalogAuthority Authority { get; }
    /// <summary>Gets current selectors for fallback controls.</summary>
    internal RouteCatalogHostOptions Options { get; }
    /// <summary>Gets the current effective host environment.</summary>
    internal IHostEnvironment Environment { get; }

    /// <summary>Activates a test-owned generation before constructing the actual HTTP authority.</summary>
    internal static async Task<ReleaseBindingHttpFixture> CreateAsync()
    {
        var catalog = new RouteCatalogTestFixture(new("route-catalog", "route-catalog-authority-v1", "http://catalog-release.invalid/"));
        await catalog.ActivateAsync(catalog.Candidate());
        return new(catalog, (await catalog.Store.ReadAsync(default)).State);
    }

    /// <inheritdoc/>
    public void Dispose() { Authority.Dispose(); _client.Dispose(); }
}
