using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.Server.Tests.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Separate Development qualification of shared Dapr authority, including actual empty-cell CAS and persisted failure/restart readback.</summary>
[Trait("Category", "LiveSidecar")]
public sealed class RouteCatalogDevelopmentLiveTests
{
    [Fact]
    public async Task ThreeSidecarsSerializeBootstrapAndLifecycleWithoutMixedServing()
    {
        string id = UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
        await using var redis = new RouteCatalogDevelopmentRedis();
        await redis.StartAsync();
        string[] apps = ["catalog-gateway-" + id, "catalog-domain-" + id, "catalog-api-" + id];
        string scratch = Path.Combine(Path.GetTempPath(), "eventstore-route-catalog-" + id);
        string resources = Path.Combine(scratch, "components");
        Directory.CreateDirectory(resources);
        string component = Path.Combine(resources, "route-catalog.yaml");
        string config = Path.Combine(scratch, "configuration.yaml");
        await File.WriteAllBytesAsync(component, JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "dapr.io/v1alpha1", kind = "Component", metadata = new { name = "route-catalog" },
            spec = new { type = "state.redis", version = "v1", metadata = new[]
            { new { name = "redisHost", value = redis.Endpoint }, new { name = "redisPassword", value = "" }, new { name = "keyPrefix", value = "none" } } },
            scopes = apps,
        }));
        await File.WriteAllBytesAsync(config, JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = "route-catalog-development-fixture" },
            spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } },
        }));
        RouteCatalogDevelopmentSidecar[] sidecars = [.. apps.Select(a => new RouteCatalogDevelopmentSidecar(a, resources, config))];
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(http);
        try
        {
            foreach (var sidecar in sidecars) await sidecar.StartAsync();
            DaprRouteCatalogStateStore[] Stores(string key) => [.. sidecars.Select(sidecar => new DaprRouteCatalogStateStore(clients,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = sidecar.HttpEndpoint }).Build(),
                Options.Create(new RouteCatalogHostOptions { StateKey = key })))];

            // This is a real empty-key race through distinct sidecars, not a CAS test double.
            for (int race = 0; race < 8; race++)
            {
                DaprRouteCatalogStateStore[] stores = Stores("catalog-bootstrap-" + id + "-" + race);
                bool[] won = await Task.WhenAll(stores.Select((store, index) => store.CompareExchangeAsync(
                    RouteCatalogActivationState.Empty with { Epoch = index + 1 }, string.Empty, default)));
                won.Count(value => value).ShouldBe(1);
                var readbacks = await Task.WhenAll(stores.Select(store => store.ReadAsync(default)));
                readbacks.Select(r => r.State.Epoch).Distinct().Count().ShouldBe(1);
                readbacks.Select(r => r.ETag).Distinct().Count().ShouldBe(1);
                readbacks[0].ETag.ShouldNotBeNullOrWhiteSpace();
                (await stores[0].CompareExchangeAsync(RouteCatalogActivationState.Empty with { Epoch = 99 }, string.Empty, default)).ShouldBeFalse();
                (await stores[1].ReadAsync(default)).State.Epoch.ShouldBe(readbacks[0].State.Epoch);
            }

            RouteCatalogEnvelope Catalog(long generation) => DevelopmentRouteCatalogBuilder.Build(new(generation, false, [.. SampleCatalog.Routes, .. CatalogLiveDeclarations.Routes],
                SampleCatalog.Routes.Concat(CatalogLiveDeclarations.Routes).Select(d => new RouteCompositionBinding(d.RouteId, d.Execution!.TargetKind == "projection-actor" ? apps[0] : apps[1],
                    d.Execution.TargetKind == "projection-actor" ? "QueryAsync" : d.Kind == "command" ? "process" : d.Kind == "query" ? "query" : "project", "v1", false, "*")).ToArray(),
                SampleCatalog.Commands.Select(c =>
                {
                    var entry = DevelopmentIdempotencyBindings.Bind(c, generation) with { ConsumerAppId = apps[0] };
                    return entry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry) };
                }).ToArray()));
            RouteCatalogEnvelope baseline = Catalog(1);
            var admissionServices = new ServiceCollection();
            RouteCatalogAdmissionFixture.Configure(admissionServices, SampleCatalog.CreateAdapters());
            using var admissionProvider = admissionServices.BuildServiceProvider();
            DaprRouteCatalogStateStore[] authorityStores = Stores("catalog-authority-" + id);
            async Task<RouteCatalogHostObservation> Observe(int index)
            {
                sidecars[index].RequireRunning();
                using var metadata = await http.GetAsync(sidecars[index].HttpEndpoint + "/v1.0/metadata");
                metadata.EnsureSuccessStatusCode();
                using JsonDocument actual = JsonDocument.Parse(await metadata.Content.ReadAsStringAsync());
                actual.RootElement.GetProperty("components").EnumerateArray().ShouldContain(c => c.GetProperty("name").GetString() == "route-catalog"
                    && c.GetProperty("type").GetString() == "state.redis");
                var compiled = CompiledRouteCatalogDeclarations.Read(index == 2 ? [typeof(CounterCatalogDeclarations).Assembly] : [typeof(SampleCatalog).Assembly, typeof(CatalogLiveDeclarations).Assembly]);
                return new(apps[index], apps[index], compiled, baseline.Routes.Where(r => r.AppId == apps[index]).ToArray(), apps,
                    [await File.ReadAllBytesAsync(component), await File.ReadAllBytesAsync(config), sidecars[index].Arguments, Encoding.UTF8.GetBytes(sidecars[index].RuntimeFingerprint)],
                    Encoding.UTF8.GetBytes("Development-only loopback Dapr state-API fixture; no production authentication authority"),
                    index == 0 ? await RouteCatalogAdmissionObservation.CaptureAsync(admissionProvider, apps[0]) : [], [], [], authorityStores[index].Coordinates);
            }
            RouteCatalogHostObservation[] observations = await Task.WhenAll(Enumerable.Range(0, 3).Select(Observe));
            RouteCatalogHostIdentity[] identities = [.. observations.Select(ObservedRouteCatalogHost.Identity)];
            RouteCatalogAuthority[] authorities = [.. Enumerable.Range(0, 3).Select(index =>
            {
                var settings = Options.Create(new RouteCatalogHostOptions { Identity = identities[index], Profile = "Development", DevelopmentFixture = true });
                IHostEnvironment environment = Substitute.For<IHostEnvironment>();
                environment.EnvironmentName.Returns("Development");
                return new RouteCatalogAuthority(authorityStores[index], new DevelopmentRouteCatalogTrustValidator(settings, environment), settings,
                    new ObservedRouteCatalogHost(_ => Observe(index)));
            })];
            RouteCatalogActivation Candidate(long generation, string? predecessor)
            {
                var envelope = Catalog(generation);
                var candidate = new RouteCatalogActivation("catalog-attempt-" + generation + "-" + id, RouteCatalogCodec.Encode(envelope),
                    RouteCatalogCodec.ComputeRootDigest(envelope), predecessor, identities, "Development-fixture", "");
                return candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
            }
            var first = Candidate(1, null);
            await authorities[0].PrepareAsync(first);
            foreach (var authority in authorities) await authority.ReadyAsync(first.AttemptId);
            await authorities[0].CommitAsync(first.AttemptId);
            foreach (var authority in authorities) (await authority.ReadbackAsync()).RootDigest.ShouldBe(first.RootDigest);
            using var metadataClient = new DaprClientBuilder().UseGrpcEndpoint(sidecars[0].GrpcEndpoint).UseHttpEndpoint(sidecars[0].HttpEndpoint).Build();
            var registry = new DaprDomainQueryHandlerRegistry(authorities[0], metadataClient, Options.Create(new CommandStatusOptions { StateStoreName = "route-catalog" }),
                NullLogger<DaprDomainQueryHandlerRegistry>.Instance);
            var unavailableRegistry = new DaprDomainQueryHandlerRegistry(authorities[0], metadataClient, Options.Create(new CommandStatusOptions { StateStoreName = "missing-metadata-component" }),
                NullLogger<DaprDomainQueryHandlerRegistry>.Instance);
            await metadataClient.SaveStateAsync("route-catalog", "admin:query-types:catalog-test", new List<string>());
            (await registry.SupportsQueryAsync("catalog-test", "get-catalog-value")).ShouldBeTrue(); // cached absence preserves the HTTP handler
            await metadataClient.SaveStateAsync("route-catalog", "admin:query-types:catalog-test", new List<string> { "undeclared-old-handler" });
            (await registry.SupportsQueryAsync("catalog-test", "get-catalog-value")).ShouldBeTrue();
            await metadataClient.SaveStateAsync("route-catalog", "admin:query-types:counter", new List<string> { "get-counter-status" });
            (await registry.SupportsQueryAsync("counter", "get-counter-status")).ShouldBeFalse(); // stale positive cannot replace the actor
            (await unavailableRegistry.SupportsQueryAsync("catalog-test", "get-catalog-value")).ShouldBeTrue();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => registry.SupportsQueryAsync("catalog-test", "undeclared-old-handler"));
            var requests = await Task.WhenAll(Enumerable.Range(0, 6).Select(async index =>
            {
                var lease = await authorities[index % 3].AcquireAsync();
                lease.Snapshot.Resolve("command", "tenant-a", "counter", "increment-counter").Target.AppId.ShouldBe(apps[1]);
                return lease;
            }));
            var second = Candidate(2, first.RootDigest);
            second = second with { AttemptId = second.AttemptId + "-retry" };
            second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
            await authorities[0].PrepareAsync(second);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => registry.SupportsQueryAsync("catalog-test", "get-catalog-value"));
            foreach (var authority in authorities) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync());
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authorities[0].ReadyAsync(second.AttemptId));
            foreach (var lease in requests) { lease.Snapshot.RootDigest.ShouldBe(first.RootDigest); await lease.DisposeAsync(); }
            await authorities[0].ReadyAsync(second.AttemptId);
            await sidecars[2].StopAsync();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authorities[0].CommitAsync(second.AttemptId));
            await authorities[0].RollbackAsync(new(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await authorities[0].ReadbackAsync()).Epoch, "pending"));
            (await authorityStores[1].ReadAsync(default)).State.Committed!.RootDigest.ShouldBe(first.RootDigest);
            await sidecars[2].StartAsync();
            (await authorities[2].ReadbackAsync()).RootDigest.ShouldBe(first.RootDigest);
            second = second with { AttemptId = second.AttemptId + "-retry" };
            second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
            await authorities[0].PrepareAsync(second);
            foreach (var authority in authorities) await authority.ReadyAsync(second.AttemptId);
            await authorities[0].CommitAsync(second.AttemptId);
            (await registry.SupportsQueryAsync("catalog-test", "get-catalog-value")).ShouldBeTrue();
            (await registry.SupportsQueryAsync("counter", "get-counter-status")).ShouldBeFalse();
            (await unavailableRegistry.SupportsQueryAsync("catalog-test", "get-catalog-value")).ShouldBeTrue();
            await sidecars[2].StopAsync();
            (await authorities[1].ReadbackAsync()).RootDigest.ShouldBe(second.RootDigest);
            await sidecars[2].StartAsync();
            (await authorities[2].ReadbackAsync()).RootDigest.ShouldBe(second.RootDigest);
            await authorities[0].RollbackAsync(new(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await authorities[0].ReadbackAsync()).Epoch, "committed"));
            (await registry.SupportsQueryAsync("catalog-test", "get-catalog-value")).ShouldBeTrue();
            (await registry.SupportsQueryAsync("counter", "get-counter-status")).ShouldBeFalse();
            (await metadataClient.GetStateAsync<List<string>>("route-catalog", "admin:query-types:catalog-test")).ShouldBe(["undeclared-old-handler"]);
            foreach (var store in authorityStores)
            {
                var persisted = (await store.ReadAsync(default)).State;
                persisted.Committed!.RootDigest.ShouldBe(first.RootDigest);
                persisted.Epoch.ShouldBe(3);
                persisted.Pending.ShouldBeNull();
                persisted.Leases.ShouldBeEmpty();
            }
        }
        finally
        {
            foreach (var sidecar in sidecars) await sidecar.DisposeAsync();
            Directory.Delete(scratch, recursive: true);
        }
    }
}
