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
    private readonly Dictionary<string, RouteCatalogHostOptions> _options = new(StringComparer.Ordinal);
    public Func<RouteCatalogHostObservation, RouteCatalogHostObservation>? Recapture { get; set; }
    public RouteCatalogTestStateStore Store { get; } = new();
    public RouteCatalogHostIdentity[] Hosts { get; }
    public RouteCatalogAuthority[] Authorities { get; }
    public RouteCatalogEnvelope Envelope { get; }
    public RouteCatalogHostObservation[] Observations { get; }

    public RouteCatalogTestFixture(RouteCatalogStateCoordinates? coordinates = null, IReadOnlyList<string>? additionalAppIds = null, bool compiledSchemaThree = false, string? replicaAppId = null)
    {
        if (coordinates is not null) Store.Coordinates = coordinates;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) directory = directory.Parent;
        Envelope = RouteCatalogCodec.Decode(File.ReadAllBytes(Path.Combine(directory!.FullName, "deploy/dapr/eventstore-routing-catalog.json")));
        string[] appIds = [.. new[] { "eventstore" }.Concat(Envelope.Routes.Select(r => r.AppId)).Concat(["sample-api", "tenants-api"]).Concat(additionalAppIds ?? []).Distinct(StringComparer.Ordinal)];
        var services = new ServiceCollection();
        RouteCatalogAdmissionFixture.Configure(services, compiledSchemaThree ? SampleCatalog.CreateAdapters() : [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()]);
        using var provider = services.BuildServiceProvider();
        var admission = RouteCatalogAdmissionObservation.CaptureAsync(provider, "eventstore").GetAwaiter().GetResult();
        if (compiledSchemaThree)
        {
            var declarations = CompiledRouteCatalogDeclarations.Read([typeof(SampleCatalog).Assembly]);
            var operations = CompiledRouteCatalogDeclarations.ReadOperations([typeof(SampleCatalog).Assembly]);
            Envelope = Hexalith.EventStore.Client.Routing.VersionedRouteCatalogBuilder.Build(new(1, "Development", declarations, operations,
                declarations.Select(declaration => new Hexalith.EventStore.Client.Routing.RouteCompositionBinding(declaration.RouteId,
                    declaration.Execution!.TargetKind == "projection-actor" ? "eventstore" : "sample", SampleCatalog.ReceiverMethods[declaration.RouteId],
                    declaration.ContractVersion, false, "*")).ToArray(), admission));
            appIds = [.. new[] { "eventstore" }.Concat(Envelope.Routes.Select(route => route.AppId)).Concat(["sample-api", "tenants-api"])
                .Concat(additionalAppIds ?? []).Distinct(StringComparer.Ordinal)];
        }
        Observations = [.. appIds.Select(app => new RouteCatalogHostObservation(app, app,
            Envelope.Declarations.Where(d => Envelope.Routes.Any(r => r.RouteId == d.RouteId && r.AppId == app)).ToArray(),
            Envelope.Routes.Where(r => r.AppId == app).ToArray(), appIds,
            [System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { kind = "Component", metadata = new { name = Store.Coordinates.StateStore }, spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = appIds }),
             System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } } })], Encoding.UTF8.GetBytes("observed-authentication-v1"), app == "eventstore" ? admission : [], [], [], Store.Coordinates, Operations: compiledSchemaThree ? Envelope.Operations!.Where(operation => Envelope.Routes.Any(route => route.RouteId == operation.RouteId && route.AppId == app)).ToArray() : null))];
        if (replicaAppId is not null)
        {
            string replicaHost = replicaAppId + "-ha";
            string[] required = [.. appIds, replicaHost];
            var executable = new Dictionary<string, string> { [typeof(SampleCatalog).Assembly.FullName!] =
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(SampleCatalog).Assembly.Location))) };
            var original = Observations.Single(observation => observation.AppId == replicaAppId);
            Observations = [.. Observations.Select(observation => observation with
            { RequiredHostIds = required, ExecutableOwners = observation.AppId == replicaAppId ? executable : null }),
                original with { HostId = replicaHost, RequiredHostIds = required, ExecutableOwners = executable,
                    TopologyDocuments = [.. original.TopologyDocuments, Encoding.UTF8.GetBytes("independent-replica-sidecar-coordinates")] }];
        }
        Hosts = [.. Observations.Select(ObservedRouteCatalogHost.Identity)];
        Authorities = [.. Hosts.Select(host => CreateAuthority(host))];
    }

    public RouteCatalogAuthority CreateAuthority(RouteCatalogHostIdentity host, string environment = "Development", IRouteCatalogQuiescenceVerifier? verifier = null)
    {
        var local = new RouteCatalogHostOptions { Identity = host, Profile = "Development", DevelopmentFixture = true };
        _options[host.HostId] = local;
        var options = Options.Create(local);
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var observer = new ObservedRouteCatalogHost(_ => Task.FromResult(Capture(host.HostId)));
        return new(Store, new DevelopmentRouteCatalogTrustValidator(options, env), options, observer, verifier);
    }

    private RouteCatalogHostObservation Capture(string hostId)
    {
        var actual = Observations.Single(observation => observation.HostId == hostId);
        return Recapture?.Invoke(actual) ?? actual;
    }

    public void UpdateObservation(int index, RouteCatalogHostObservation observation)
    {
        Observations[index] = observation;
        Hosts[index] = ObservedRouteCatalogHost.Identity(observation);
        _options[observation.HostId].Identity = Hosts[index];
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
        var technical = Observations.Where(observation => observation.ReceiverSurface?.FenceVersion == RouteCatalogTechnicalHttpPolicy.FenceVersion)
            .Select(observation => new RouteCatalogTechnicalHttpReceiver(observation.AppId, RouteCatalogTechnicalHttpPolicy.FenceVersion, observation.ReceiverSurface!.TechnicalHttp!)
                { Invocations = observation.DeclaredInvocations }).ToArray();
        candidate = candidate with { TechnicalHttpReceivers = technical.Length == 0 ? null : technical };
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
