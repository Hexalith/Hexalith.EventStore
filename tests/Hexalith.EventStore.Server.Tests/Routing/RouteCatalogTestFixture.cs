using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.Tenants.Server.Routing;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Routing;

internal sealed class RouteCatalogTestFixture
{
    public RouteCatalogTestStateStore Store { get; } = new();
    public RouteCatalogHostIdentity[] Hosts { get; }
    public RouteCatalogAuthority[] Authorities { get; }
    public RouteCatalogEnvelope Envelope { get; }
    public RouteCatalogHostObservation[] Observations { get; }

    public RouteCatalogTestFixture(RouteCatalogStateCoordinates? coordinates = null)
    {
        if (coordinates is not null) Store.Coordinates = coordinates;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) directory = directory.Parent;
        Envelope = RouteCatalogCodec.Decode(File.ReadAllBytes(Path.Combine(directory!.FullName, "deploy/dapr/eventstore-routing-catalog.json")));
        string[] appIds = [.. new[] { "eventstore" }.Concat(Envelope.Routes.Select(r => r.AppId)).Concat(["sample-api", "tenants-api"]).Distinct(StringComparer.Ordinal)];
        var services = new ServiceCollection();
        RouteCatalogAdmissionFixture.Configure(services, [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()]);
        using var provider = services.BuildServiceProvider();
        var admission = RouteCatalogAdmissionObservation.CaptureAsync(provider, "eventstore").GetAwaiter().GetResult();
        Observations = [.. appIds.Select(app => new RouteCatalogHostObservation(app, app,
            Envelope.Declarations.Where(d => Envelope.Routes.Any(r => r.RouteId == d.RouteId && r.AppId == app)).ToArray(),
            Envelope.Routes.Where(r => r.AppId == app).ToArray(), appIds,
            [System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { kind = "Component", metadata = new { name = Store.Coordinates.StateStore }, spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = appIds }),
             System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } } })], Encoding.UTF8.GetBytes("observed-authentication-v1"), app == "eventstore" ? admission : [], [], [], Store.Coordinates))];
        Hosts = [.. Observations.Select(ObservedRouteCatalogHost.Identity)];
        Authorities = [.. Hosts.Select(host => CreateAuthority(host))];
    }

    public RouteCatalogAuthority CreateAuthority(RouteCatalogHostIdentity host, string environment = "Development", IRouteCatalogQuiescenceVerifier? verifier = null)
    {
        var options = Options.Create(new RouteCatalogHostOptions { Identity = host, Profile = "Development", DevelopmentFixture = true });
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var observer = new ObservedRouteCatalogHost(_ => Task.FromResult(Observations.Single(o => o.HostId == host.HostId)));
        return new(Store, new DevelopmentRouteCatalogTrustValidator(options, env), options, observer, verifier);
    }

    public RouteCatalogActivation Candidate(long generation = 1, string? predecessor = null, string? attempt = null)
    {
        RouteCatalogEnvelope changed = Envelope with
        {
            Generation = generation,
            Routes = [.. Envelope.Routes.Select(r => r with { Generation = generation })],
            Idempotency = [.. Envelope.Idempotency.Select(i =>
            {
                var item = i with { Generation = generation };
                return item with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(item) };
            })],
        };
        changed = RouteCatalogCodec.Seal(changed);
        var candidate = new RouteCatalogActivation(attempt ?? "attempt-" + generation, RouteCatalogCodec.Encode(changed),
            RouteCatalogCodec.ComputeRootDigest(changed), predecessor, Hosts, "Development-fixture", "");
        return candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
    }

    public async Task ActivateAsync(RouteCatalogActivation candidate)
    {
        await Authorities[0].PrepareAsync(candidate).ConfigureAwait(false);
        foreach (RouteCatalogAuthority authority in Authorities) await authority.ReadyAsync(candidate.AttemptId).ConfigureAwait(false);
        await Authorities[0].CommitAsync(candidate.AttemptId).ConfigureAwait(false);
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
