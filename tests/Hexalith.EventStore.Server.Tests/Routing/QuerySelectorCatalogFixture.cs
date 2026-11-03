using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.QuerySelectorFixture;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Normal schema-three composition from an isolated compiled owner and its independently installed endpoint/handler inventory.</summary>
internal sealed class QuerySelectorCatalogFixture
{
    public RouteCatalogTestStateStore Store { get; } = new();
    public IReadOnlyList<RouteContractDeclaration> Declarations { get; }
    public RouteCatalogEnvelope Catalog { get; }
    public RouteCatalogActivation Candidate { get; }
    public RouteCatalogAuthority[] Authorities { get; }

    public QuerySelectorCatalogFixture()
    {
        var owner = typeof(SelectorFixtureCatalog).Assembly;
        Declarations = CompiledRouteCatalogDeclarations.Read([owner]);
        var operations = CompiledRouteCatalogDeclarations.ReadOperations([owner]);
        var services = new ServiceCollection();
        services.AddSingleton(new DiscoveryResult([], []));
        services.AddSingleton<IDomainQueryHandler, SelectorFixtureQueryHandler>();
        services.AddSingleton(Options.Create(new DomainProjectionIdentityOptions { AppId = "selector-owner", ServiceVersion = "v1" }));
        using var provider = services.BuildServiceProvider();
        var endpoint = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/query"), 0);
        endpoint.Metadata.Add(new HttpMethodMetadata(["POST"]));
        endpoint.Metadata.Add(new AuthorizeAttribute { Policy = EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/query").Policy });
        var targets = DevelopmentDomainRouteCatalog.Targets(provider, [endpoint.Build()], Declarations);
        Catalog = VersionedRouteCatalogBuilder.Build(new(1, "Development", Declarations, operations,
            targets.Select(target => new RouteCompositionBinding(target.RouteId, target.AppId, target.Method, target.ContractVersion, false, target.ResolverTenantPattern)).ToArray(), []));
        string[] hosts = ["eventstore", "selector-owner"];
        byte[][] topology = [JsonSerializer.SerializeToUtf8Bytes(new { kind = "Component", metadata = new { name = Store.Coordinates.StateStore }, spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = hosts }),
            JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } } })];
        var observations = hosts.Select(host => new RouteCatalogHostObservation(host, host,
            host == "selector-owner" ? Declarations : [], host == "selector-owner" ? targets : [], hosts, topology,
            Encoding.UTF8.GetBytes("independent-query-selector-fixture-authentication"), [], [], [], Store.Coordinates,
            Operations: host == "selector-owner" ? operations : [])
            { ExecutableOwners = host == "selector-owner" ? new Dictionary<string, string> { [owner.FullName!] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(owner.Location))) } : null }).ToArray();
        var identities = observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
        Authorities = observations.Select((observation, index) =>
        {
            var options = Options.Create(new RouteCatalogHostOptions { Identity = identities[index], Profile = "Development", DevelopmentFixture = true });
            var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
            return new RouteCatalogAuthority(Store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
                new ObservedRouteCatalogHost(_ => Task.FromResult(observation)));
        }).ToArray();
        Candidate = new("selector-owner-activation", RouteCatalogCodec.Encode(Catalog), RouteCatalogCodec.ComputeRootDigest(Catalog), null, identities, "Development-fixture", "");
        Candidate = Candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(Candidate) };
    }

    public async Task ActivateAsync()
    {
        await Authorities[0].PrepareAsync(Candidate, TestContext.Current.CancellationToken);
        foreach (var authority in Authorities) await authority.ReadyAsync(Candidate.AttemptId, TestContext.Current.CancellationToken);
        await Authorities[0].CommitAsync(Candidate.AttemptId, TestContext.Current.CancellationToken);
    }
}
