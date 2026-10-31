using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class DevelopmentBootstrapDrainingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BootstrapWaitsForActualDrainAndCancellationNeverDiscardsAFence(bool cancel)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var lease = await fixture.Authorities[0].AcquireAsync();
        string directory = Directory.CreateTempSubdirectory("catalog-bootstrap-draining-").FullName;
        var providers = new List<ServiceProvider>();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var second = fixture.Candidate(2, first.RootDigest);
            var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
            var envelope = RouteCatalogCodec.Decode(second.CanonicalCatalog, second.RootDigest);
            string[] hosts = fixture.Hosts.Select(host => host.HostId).ToArray();
            var tasks = fixture.Observations.Select((actual, index) =>
            {
                var services = new ServiceCollection();
                services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
                services.AddSingleton<IOptions<RouteCatalogHostOptions>>(Options.Create(new RouteCatalogHostOptions { Profile = "Development", DevelopmentFixture = true }));
                services.AddSingleton<IRouteCatalogAuthority>(provider => new RouteCatalogAuthority(fixture.Store,
                    new DevelopmentRouteCatalogTrustValidator(provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(), environment),
                    provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>(), new ObservedRouteCatalogHost(_ => Task.FromResult(actual))));
                var provider = services.BuildServiceProvider(); providers.Add(provider);
                return DevelopmentRouteCatalogBootstrap.ActivateAsync(provider, directory, actual.HostId, hosts[0], second.AttemptId, hosts,
                    (_, _) => Task.FromResult(actual), index == 0 ? _ => envelope : null, bound.Token, first.RootDigest);
            }).ToArray();
            while ((await fixture.Store.ReadAsync(bound.Token)).State.Pending is null) await Task.Delay(10, bound.Token);
            tasks.ShouldAllBe(task => !task.IsCompleted);
            (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
            if (cancel)
            {
                bound.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => Task.WhenAll(tasks));
                var state = (await fixture.Store.ReadAsync(default)).State;
                state.Leases.ShouldHaveSingleItem();
                state.Committed!.RootDigest.ShouldBe(first.RootDigest);
                state.Ready.ShouldBeEmpty();
            }
            else
            {
                await lease.DisposeAsync();
                await Task.WhenAll(tasks);
                (await fixture.Store.ReadAsync(default)).State.Committed!.RootDigest.ShouldBe(second.RootDigest);
                Directory.GetFiles(directory, "*.committed.json").Length.ShouldBe(hosts.Length);
            }
        }
        finally
        {
            await lease.DisposeAsync();
            foreach (var provider in providers) await provider.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
