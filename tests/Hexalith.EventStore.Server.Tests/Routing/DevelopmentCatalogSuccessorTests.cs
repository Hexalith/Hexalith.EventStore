using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Recomposition checks the actual complete v2 predecessor through the real coordinator and persisted authority barrier.</summary>
public sealed class DevelopmentCatalogSuccessorTests
{
    [Theory(Timeout = 30000)]
    [InlineData("valid")]
    [InlineData("retained-lease")]
    [InlineData("wrong-binding")]
    [InlineData("wrong-attempt")]
    [InlineData("wrong-epoch")]
    [InlineData("wrong-host")]
    [InlineData("new-cell")]
    [InlineData("missing-host")]
    public async Task ActualCompiledV2SuccessorRequiresExactRetainedAuthority(string scenario)
    {
        await using var host = new TechnicalHttpCatalogFixture();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var fixture = host.Catalog;
        var first = (await fixture.Store.ReadAsync(default)).State.Committed!;
        var held = await fixture.Authorities[0].ReadbackAsync();
        held.ActivationBindingDigest.ShouldBe(RouteCatalogActivationBinding.ComputeDigest(first));
        if (scenario == "retained-lease")
        {
            var lease = await fixture.Authorities[0].AcquireAsync(); lease.Retain(); await lease.DisposeAsync();
        }
        held = scenario switch
        {
            "wrong-binding" => held with { ActivationBindingDigest = new string('f', 64) },
            "wrong-attempt" => held with { AttemptId = "another-attempt" },
            "wrong-epoch" => held with { Epoch = held.Epoch + 1 },
            "wrong-host" => held with { RequiredHosts = held.RequiredHosts.Select((peer, index) => index == 0 ? peer with { InventoryDigest = new string('f', 64) } : peer).ToArray() },
            _ => held,
        };
        fixture.Store.Coordinates = fixture.Store.Coordinates with { HttpEndpoint = "http://127.0.0.1:3599/" };
        for (int index = 0; index < fixture.Observations.Length; index++)
            fixture.UpdateObservation(index, fixture.Observations[index] with { AuthorityCoordinates = fixture.Store.Coordinates });
        var proposed = fixture.Candidate(2, first.RootDigest);
        var proposedCatalog = RouteCatalogCodec.Decode(proposed.CanonicalCatalog, proposed.RootDigest);
        var envelope = VersionedRouteCatalogBuilder.Build(new(2, "Development", proposedCatalog.Declarations, proposedCatalog.Operations!,
            proposedCatalog.Routes.Select(route => new RouteCompositionBinding(route.RouteId, route.AppId, route.Method, route.ContractVersion, false, route.ResolverTenantPattern)).ToArray(),
            proposedCatalog.Idempotency, first.CanonicalCatalog, first.RootDigest));
        var store = scenario == "new-cell" ? new RouteCatalogTestStateStore { Coordinates = fixture.Store.Coordinates } : fixture.Store;
        string directory = Directory.CreateTempSubdirectory("catalog-successor-").FullName;
        List<ServiceProvider> providers = [];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            string activationPath = Path.Combine(directory, "predecessor.json");
            string readbackPath = Path.Combine(directory, "predecessor-readback.json");
            await File.WriteAllBytesAsync(activationPath, JsonSerializer.SerializeToUtf8Bytes(first), deadline.Token);
            await File.WriteAllBytesAsync(readbackPath, JsonSerializer.SerializeToUtf8Bytes(held), deadline.Token);
            string[] hosts = fixture.Observations.Select(observation => observation.HostId).ToArray();
            Task[] tasks = fixture.Observations.Where((_, index) => scenario != "missing-host" || index != fixture.Observations.Length - 1)
                .Select((actual, index) =>
                {
                    var options = Options.Create(new RouteCatalogHostOptions { Profile = "Development", DevelopmentFixture = true });
                    IHostEnvironment environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
                    var authority = new RouteCatalogAuthority(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
                        new ObservedRouteCatalogHost(_ => Task.FromResult(actual)));
                    var services = new ServiceCollection(); services.AddSingleton<IRouteCatalogStateStore>(store);
                    services.AddSingleton<IOptions<RouteCatalogHostOptions>>(options); services.AddSingleton<IRouteCatalogAuthority>(authority);
                    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["EventStore:RouteCatalog:DevelopmentBootstrap:PredecessorActivationPath"] = activationPath,
                        ["EventStore:RouteCatalog:DevelopmentBootstrap:PredecessorReadbackPath"] = readbackPath,
                    }).Build());
                    var provider = services.BuildServiceProvider(); providers.Add(provider);
                    return DevelopmentRouteCatalogBootstrap.ActivateAsync(provider, directory, actual.HostId, hosts[0], "successor-attempt",
                        hosts, (_, _) => Task.FromResult(actual), index == 0 ? _ => envelope : null, deadline.Token);
                }).ToArray();
            if (scenario == "valid")
            {
                await Task.WhenAll(tasks);
                var readback = await providers[0].GetRequiredService<IRouteCatalogAuthority>().ReadbackAsync();
                var committed = (await store.ReadAsync(default)).State.Committed!;
                readback.Serving.ShouldBeTrue(); readback.Generation.ShouldBe(2); readback.Epoch.ShouldBe(2);
                readback.AttemptId.ShouldBe("successor-attempt"); readback.ActivationBindingDigest.ShouldBe(RouteCatalogActivationBinding.ComputeDigest(committed));
                readback.RequiredHosts.ShouldBe(committed.RequiredHosts);
                committed.PredecessorRootDigest.ShouldBe(first.RootDigest);
                RouteCatalogCodec.Decode(committed.CanonicalCatalog, committed.RootDigest).RetainedIdempotency.ShouldNotBeEmpty();
                Directory.GetFiles(directory, "*.committed.json").Length.ShouldBe(hosts.Length);
            }
            else
            {
                await Should.ThrowAsync<Exception>(() => Task.WhenAll(tasks));
                var state = (await store.ReadAsync(default)).State;
                state.Committed?.RootDigest.ShouldBe(scenario == "new-cell" ? null : first.RootDigest);
                state.Epoch.ShouldBe(scenario == "new-cell" ? 0 : 1);
                state.Leases.Count.ShouldBe(scenario == "retained-lease" ? 1 : 0);
                Directory.GetFiles(directory, "*.committed.json").ShouldBeEmpty();
                File.Exists(Path.Combine(directory, "activation.json")).ShouldBeFalse();
            }
        }
        finally
        {
            foreach (var provider in providers) await provider.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
