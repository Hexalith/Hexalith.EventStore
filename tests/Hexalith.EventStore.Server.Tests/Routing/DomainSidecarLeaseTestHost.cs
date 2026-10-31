using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Actual SDK subscription mapper, processor, channel authorization and durable catalog authority.</summary>
internal sealed class DomainSidecarLeaseTestHost : IAsyncDisposable, IEventStoreDomainEventHandler<CounterIncremented>
{
    private const string ChannelToken = "domain-sidecar-lease-channel";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "domain-sidecar-lease-" + Guid.NewGuid().ToString("N"));
    private readonly RouteCatalogTestFixture _fixture = new();
    private WebApplication _app = null!;
    private RouteCatalogAuxiliaryOperation _sidecar = null!;
    internal HttpClient Client { get; private set; } = null!;
    internal int Calls { get; private set; }
    internal Func<Task>? Work { get; set; }
    internal RouteCatalogAuthority Authority => _fixture.Authorities[0];
    internal RouteCatalogTestStateStore Store => _fixture.Store;

    internal static async Task<DomainSidecarLeaseTestHost> StartAsync()
    {
        var host = new DomainSidecarLeaseTestHost();
        Directory.CreateDirectory(host._directory);
        string aclPath = Path.Combine(host._directory, "acl.json");
        byte[] acl = JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new {
            defaultAction = "deny", trustDomain = "public", policies = new[] { new { appId = "eventstore", defaultAction = "deny",
                operations = new[] { new { name = "counter/events", httpVerb = new[] { "POST" }, action = "allow" } } } } } } });
        await File.WriteAllBytesAsync(aclPath, acl);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
            ["EventStore:DomainService:AppId"] = "eventstore",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = host._directory,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = aclPath,
            ["Dapr:DeclarativeSubscriptions"] = "true",
            ["Authentication:JwtBearer:Issuer"] = "sidecar-test", ["Authentication:JwtBearer:Audience"] = "sidecar-test",
            ["Authentication:JwtBearer:SigningKey"] = "__HEXALITH_TEST_SIGNING_KEY_PLACEHOLDER__",
        });
        builder.AddEventStoreDomainService(typeof(CounterIncremented).Assembly);
        builder.Services.AddEventStoreDomainEvents(typeof(CounterIncremented).Assembly, options => {
            options.SubscriptionRoute = "/counter/events"; options.PubSubName = "pubsub"; options.TopicName = "counter.events";
        });
        builder.Services.AddSingleton<IEventStoreDomainEventHandler<CounterIncremented>>(host);
        builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
        builder.Services.AddSingleton<IRouteCatalogAuthority>(_ => host.Authority);
        host._app = builder.Build();
        host._app.UseEventStoreDomainService();
        await host._app.StartAsync(TestContext.Current.CancellationToken);
        RouteCatalogReceiverSurface receiver = host._app.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
        var endpoint = receiver.Endpoints.Single(endpoint => endpoint.Pattern == "/counter/events");
        host._sidecar = new("eventstore", "counter/events", DomainRouteCatalogSidecarMetadata.Operation, "counter") { Sidecar = endpoint.Sidecar! };
        RouteCatalogEnvelope envelope = CatalogSchemaThreeFixture.Create(host._fixture.Envelope);
        for (int index = 0; index < host._fixture.Observations.Length; index++)
        {
            var original = host._fixture.Observations[index];
            var observed = original with { Operations = envelope.Operations!.Where(operation => original.Declarations.Any(declaration => declaration.RouteId == operation.RouteId)).ToArray() };
            if (index == 0) observed = observed with { TopologyDocuments = [original.TopologyDocuments[0], acl], ReceiverSurface = receiver, AuxiliaryOperations = [host._sidecar] };
            host._fixture.Observations[index] = observed;
            host._fixture.Hosts[index] = ObservedRouteCatalogHost.Identity(observed);
            host._fixture.Authorities[index] = host._fixture.CreateAuthority(host._fixture.Hosts[index]);
        }
        host.Client = host._app.GetTestClient();
        host.Client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        return host;
    }

    internal RouteCatalogActivation Candidate(long generation = 1, string? predecessor = null, string? mutation = null)
    {
        var candidate = _fixture.Candidate(generation, predecessor);
        var envelope = CatalogSchemaThreeFixture.Create(RouteCatalogCodec.Decode(candidate.CanonicalCatalog));
        var sidecar = mutation switch {
            "topic" => _sidecar with { Sidecar = _sidecar.Sidecar! with { TopicName = "foreign.events" } },
            "component" => _sidecar with { Sidecar = _sidecar.Sidecar! with { PubSubName = "foreign-pubsub" } },
            "code" => _sidecar with { Sidecar = _sidecar.Sidecar! with { CodeDigest = new string('a', 64) } },
            "method" => _sidecar with { Method = "foreign/events" },
            "operation" => _sidecar with { Operation = "foreign-operation" },
            _ => _sidecar,
        };
        candidate = candidate with { CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope),
            AuxiliaryOperations = [sidecar] };
        return candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
    }

    internal Task ActivateAsync(RouteCatalogActivation candidate) => _fixture.ActivateAsync(candidate);

    internal Task<HttpResponseMessage> DeliverAsync()
    {
        string id = Guid.NewGuid().ToString("N");
        var envelope = new EventStoreDomainEventEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAV", id, "tenant-a", typeof(CounterIncremented).FullName!,
            1, DateTimeOffset.UtcNow, "01ARZ3NDEKTSV4RRFFQ69G5FAW", "json", Encoding.UTF8.GetBytes("{}")) { Domain = "counter", AggregateType = "Counter" };
        return Client.PostAsJsonAsync("/counter/events", envelope, TestContext.Current.CancellationToken);
    }

    public async Task HandleAsync(CounterIncremented @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Work is not null) await Work();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }
}
