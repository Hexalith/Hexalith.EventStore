using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>A recomposed cohort succeeds through the real authority barrier, never through retained files alone.</summary>
public sealed class DevelopmentCatalogSuccessorTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("retained-lease")]
    [InlineData("wrong-predecessor")]
    [InlineData("new-cell")]
    [InlineData("missing-host")]
    public async Task ChangedCoordinatesRequireExactPredecessorAndEveryPhysicalReadyReceipt(string scenario)
    {
        var fixture = new RouteCatalogTestFixture(replicaAppId: "sample-api");
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        if (scenario == "retained-lease")
        {
            var lease = await fixture.Authorities[0].AcquireAsync(); lease.Retain(); await lease.DisposeAsync();
        }
        fixture.Store.Coordinates = fixture.Store.Coordinates with { HttpEndpoint = "http://127.0.0.1:3599/" };
        for (int i = 0; i < fixture.Observations.Length; i++) fixture.Observations[i] = fixture.Observations[i] with
            { AuthorityCoordinates = fixture.Store.Coordinates };
        var candidate = fixture.Candidate(2, first.RootDigest);
        candidate = candidate with { RequiredHosts = fixture.Observations.Select(ObservedRouteCatalogHost.Identity).ToArray() };
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        var envelope = RouteCatalogCodec.Decode(candidate.CanonicalCatalog, candidate.RootDigest);
        var store = scenario == "new-cell" ? new RouteCatalogTestStateStore { Coordinates = fixture.Store.Coordinates } : fixture.Store;
        string directory = Directory.CreateTempSubdirectory("catalog-successor-").FullName;
        List<ServiceProvider> providers = [];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            string[] hosts = candidate.RequiredHosts.Select(host => host.HostId).ToArray();
            var tasks = fixture.Observations.Where((_, index) => scenario != "missing-host" || index != fixture.Observations.Length - 1)
                .Select((actual, index) =>
                {
                    var options = Options.Create(new RouteCatalogHostOptions { Profile = "Development", DevelopmentFixture = true });
                    IHostEnvironment environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
                    var authority = new RouteCatalogAuthority(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
                        new ObservedRouteCatalogHost(_ => Task.FromResult(actual)));
                    var services = new ServiceCollection(); services.AddSingleton<IRouteCatalogStateStore>(store);
                    services.AddSingleton<IOptions<RouteCatalogHostOptions>>(options); services.AddSingleton<IRouteCatalogAuthority>(authority);
                    var provider = services.BuildServiceProvider(); providers.Add(provider);
                    return DevelopmentRouteCatalogBootstrap.ActivateAsync(provider, directory, actual.HostId, hosts[0], candidate.AttemptId,
                        hosts, (_, _) => Task.FromResult(actual), index == 0 ? _ => envelope : null, timeout.Token,
                        scenario == "wrong-predecessor" ? new string('f', 64) : first.RootDigest);
                }).ToArray();
            if (scenario == "valid")
            {
                await Task.WhenAll(tasks);
                var readback = await providers[0].GetRequiredService<IRouteCatalogAuthority>().ReadbackAsync();
                readback.Serving.ShouldBeTrue(); readback.Epoch.ShouldBe(2); readback.RootDigest.ShouldBe(candidate.RootDigest);
                readback.RequiredHosts.ShouldBe(candidate.RequiredHosts);
                readback.RequiredHosts.Count(host => host.AppId == "sample-api").ShouldBe(2);
                Directory.GetFiles(directory, "*.ready.json").Length.ShouldBe(hosts.Length);
                (await store.ReadAsync(default)).State.Previous!.RootDigest.ShouldBe(first.RootDigest);
            }
            else
            {
                await Should.ThrowAsync<Exception>(() => Task.WhenAll(tasks));
                var state = (await store.ReadAsync(default)).State;
                state.Committed?.RootDigest.ShouldBe(scenario == "new-cell" ? null : first.RootDigest);
                state.Epoch.ShouldBe(scenario == "new-cell" ? 0 : 1);
                state.Leases.Count.ShouldBe(scenario == "retained-lease" ? 1 : 0);
                Directory.GetFiles(directory, "*.committed.json").ShouldBeEmpty();
                if (scenario is "wrong-predecessor" or "new-cell")
                {
                    Directory.GetFiles(directory, "*.observation.json").Length.ShouldBe(hosts.Length);
                    File.Exists(Path.Combine(directory, "activation.json")).ShouldBeFalse();
                    Directory.GetFiles(directory, "*.ready.json").ShouldBeEmpty();
                }
            }
        }
        finally
        {
            foreach (var provider in providers) await provider.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }
}
