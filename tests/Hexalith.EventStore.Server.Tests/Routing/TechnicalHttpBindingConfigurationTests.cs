using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises the real bootstrap's pinned v2 intent rather than allowing configuration drift to restore ordinary HTTP.</summary>
public sealed class TechnicalHttpBindingConfigurationTests
{
    [Theory]
    [InlineData("unknown")]
    [InlineData("cleared-config")]
    [InlineData("cleared-effective-option")]
    [InlineData("legacy-absent")]
    public async Task UnsupportedOrChangedBindingIntentRefusesBeforeObservation(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string directory = Directory.CreateTempSubdirectory("binding-mode-").FullName;
        try
        {
            var fixture = new RouteCatalogTestFixture();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var section = configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
            section["Directory"] = directory; section["HostId"] = fixture.Hosts[0].HostId;
            section["CoordinatorHostId"] = fixture.Hosts[1].HostId; section["AttemptId"] = "binding-mode";
            section["BindingVersion"] = scenario == "unknown" ? "3" : scenario == "legacy-absent" ? null : "2";
            for (int index = 0; index < fixture.Hosts.Length; index++) section["RequiredHostIds:" + index] = fixture.Hosts[index].HostId;
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
            bool captured = false;
            void Register() => services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration, (_, _) =>
            { captured = true; return Task.FromResult(fixture.Observations[0]); });
            if (scenario == "unknown")
            {
                Should.Throw<RouteCatalogUnavailableException>(Register).Category.ShouldBe(RouteCatalogRefusalReason.Configuration);
                return;
            }
            Register();
            using var provider = services.BuildServiceProvider();
            if (scenario == "cleared-config") section["BindingVersion"] = null;
            if (scenario == "cleared-effective-option") provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.RequireTechnicalHttpBinding = false;
            Task<Hexalith.EventStore.Contracts.Routing.RouteCatalogHostIdentity> Observe() => provider.GetRequiredService<IRouteCatalogHostObserver>()
                .ObserveAsync(fixture.Candidate(), fixture.Store.Coordinates, TestContext.Current.CancellationToken);
            if (scenario == "legacy-absent") (await Observe()).ShouldBe(fixture.Hosts[0]);
            else (await Should.ThrowAsync<RouteCatalogUnavailableException>(Observe)).Category.ShouldBe(RouteCatalogRefusalReason.Configuration);
            captured.ShouldBe(scenario == "legacy-absent");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
