using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Actual SDK endpoints with real channel/workload authorization and committed catalog authority.</summary>
internal sealed class TerminalRefusalHttpFixture(bool schemaThree = true, bool fullBinding = true, bool ordinaryReceiver = false) : IAsyncDisposable
{
    private string? _ordinaryDirectory;
    internal TechnicalHttpCatalogFixture Cohort { get; } = new();
    internal RouteCatalogTestFixture Catalog { get; private set; } = null!;
    internal RecordingDaprClient State { get; } = new();
    internal WebApplication App { get; private set; } = null!;
    internal HttpClient Client { get; } = new();
    internal int Callbacks { get; private set; }
    internal int Admissions { get; private set; }
    internal int Processors { get; private set; }
    internal int Delegates { get; private set; }
    internal int ProtectedReads { get; private set; }
    internal Func<DomainServiceTerminalRefusalContext, CancellationToken, Task<DomainServiceTerminalRefusal>> Rule { get; set; }
        = (_, _) => Task.FromResult(DomainServiceTerminalRefusal.Reject(new TerminalRefusalRejected("unsupported")));
    internal Func<TerminalRefusalRejected, Stream, CancellationToken, Task> Serializer { get; set; }
        = (value, stream, token) => JsonSerializer.SerializeAsync(stream, value, cancellationToken: token);
    internal RouteCatalogActivation Activation { get; private set; } = null!;

    internal async Task StartAsync(bool activate = true, bool namedOnly = false, bool legacyAuxiliary = true,
        string appId = "sample", Action<IServiceCollection>? configure = null, Action<WebApplication>? middleware = null,
        bool registerRule = true, bool serializer = true, bool wrongIdentity = false, bool knownRule = false, bool probeDelegate = false,
        bool rejectAdmission = false, bool knownV2 = false, bool stepUp = false, string? knownTarget = null,
        int? commandByteBudget = null, int rejectionPayloadByteBudget = 128)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await Cohort.StartAsync(token, activate: false);
        Catalog = schemaThree && fullBinding && !ordinaryReceiver ? Cohort.Catalog : new RouteCatalogTestFixture(compiledSchemaThree: schemaThree);
        var catalog = Catalog;
        const string selectedRoute = "counter-increment-counter";
        var declarations = catalog.Envelope.Declarations.Select(declaration => knownV2 && declaration.RouteId == selectedRoute
            ? declaration with { ContractVersion = "v2" } : declaration).ToArray();
        var operations = catalog.Envelope.Operations?.Select(operation =>
        {
            if (operation.RouteId != selectedRoute || !knownV2 && !stepUp) return operation;
            var changed = operation with { ContractVersion = knownV2 ? "v2" : operation.ContractVersion,
                Eligibility = stepUp ? "step-up" : operation.Eligibility };
            return changed with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(changed) };
        }).ToArray();
        RouteCatalogEntry Change(RouteCatalogEntry route) => route with
        {
            ResolverTenantPattern = "tenant-a",
            Method = namedOnly && route.RouteId == "counter-projection" ? "project/v2" : route.Method,
            AppId = route.RouteId == selectedRoute && knownTarget is not null ? knownTarget : route.AppId,
            ContractVersion = knownV2 && route.RouteId == selectedRoute ? "v2" : route.ContractVersion,
            DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(declarations.Single(declaration => declaration.RouteId == route.RouteId)),
            OperationContractDigest = operations?.Single(operation => operation.RouteId == route.RouteId).Digest,
        };
        var routes = catalog.Envelope.Routes.Select(Change).ToArray();
        RouteCatalogAuxiliaryOperation[] auxiliary = [.. catalog.Observations.SelectMany(observation => observation.AuxiliaryOperations ?? []),
            .. namedOnly && legacyAuxiliary ? new[] { new RouteCatalogAuxiliaryOperation("sample", "project", "domain-service:project", "counter") } : []];
        RouteCatalogProjectionCapability[] capabilities = namedOnly
            ? [new("sample", "counter", "v1", "terminal-named", ["counter"])] : [];
        for (int index = 0; index < catalog.Observations.Length; index++)
        {
            var observation = catalog.Observations[index];
            catalog.UpdateObservation(index, observation with { Targets = routes.Where(route => route.AppId == observation.AppId).ToArray(),
                Declarations = declarations.Where(declaration => routes.Any(route => route.RouteId == declaration.RouteId && route.AppId == observation.AppId)).ToArray(),
                Operations = operations?.Where(operation => routes.Any(route => route.RouteId == operation.RouteId && route.AppId == observation.AppId)).ToArray(),
                AuxiliaryOperations = auxiliary.Where(item => item.AppId == observation.AppId).ToArray(),
                ProjectionCapabilities = capabilities.Where(item => item.AppId == observation.AppId).ToArray() });
        }
        var envelope = RouteCatalogCodec.Seal(catalog.Envelope with { Routes = routes, Declarations = declarations, Operations = operations });
        Activation = catalog.Candidate() with { CanonicalCatalog = RouteCatalogCodec.Encode(envelope),
            RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope), AuxiliaryOperations = auxiliary, ProjectionCapabilities = capabilities };
        Activation = Activation with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(Activation) };
        if (activate && !ordinaryReceiver) await catalog.ActivateAsync(Activation);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        Cohort.ConfigureWorkloadReceiver(builder.Configuration);
        builder.Configuration["Authentication:Workload:Audience"] = "technical-fixture";
        byte[]? ordinaryAcl = null;
        if (ordinaryReceiver)
        {
            _ordinaryDirectory = Directory.CreateTempSubdirectory("terminal-ordinary-").FullName;
            string aclPath = Path.Combine(_ordinaryDirectory, "acl.json");
            ordinaryAcl = JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new
            {
                defaultAction = "deny", trustDomain = "public", policies = new[] { new
                {
                    appId = "eventstore", defaultAction = "deny", operations = routes.Where(route => route.AppId == appId).Select(route => route.Method)
                        .Concat(auxiliary.Where(operation => operation.AppId == appId).Select(operation => operation.Method)).Distinct(StringComparer.Ordinal)
                        .Select(method => new { name = method, action = "allow", httpVerb = new[] { "POST" } }).ToArray(),
                } },
            } } });
            await File.WriteAllBytesAsync(aclPath, ordinaryAcl, token);
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = _ordinaryDirectory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = aclPath;
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
        }
        if (commandByteBudget is int budget) builder.Configuration["EventStore:Processes:MaxCommandBytesPerEnvelope"]
            = budget.ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder.Services.AddEventStoreDomainServiceSecurity();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<Dapr.Client.DaprClient>(State);
        builder.Services.AddSingleton(new DiscoveryResult([], []));
        builder.Services.AddSingleton<DomainProjectionCatalogRegistry>();
        int hostIndex = Array.FindIndex(catalog.Hosts, host => host.AppId == (wrongIdentity ? "sample" : appId));
        builder.Services.AddSingleton<IDomainRouteCatalogGuard>(new DomainRouteCatalogGuard(catalog.Authorities[hostIndex],
            Options.Create(new RouteCatalogHostOptions { Identity = catalog.Hosts.Single(host => host.AppId == appId) })));
        builder.Services.AddScoped<DomainRouteCatalogFilter>();
        void ConfigureRejectionSerialization(BoundedV1DomainSerializerProfile profile)
        {
            if (serializer) profile.Add<TerminalRefusalRejected>("terminal-refusal-rejected", "json", rejectionPayloadByteBudget,
                (value, stream, cancellation) => Serializer(value, stream, cancellation));
        }
        if (registerRule)
        {
            foreach (string domain in new[] { "counter", "unowned" })
                builder.Services.AddEventStoreDomainServiceTerminalRefusal(domain, "unsupported", async (context, cancellation) =>
                { Callbacks++; return await Rule(context, cancellation); }, ConfigureRejectionSerialization);
            if (knownRule) builder.Services.AddEventStoreDomainServiceTerminalRefusal("counter", "increment-counter", async (context, cancellation) =>
                { Callbacks++; return await Rule(context, cancellation); }, ConfigureRejectionSerialization);
        }
        var stage = Substitute.For<IDomainServiceAdmissionStage>();
        var protectedState = new DaprReadModelBatchStateAccessor(State, "statestore");
        stage.Name.Returns("unchanged-admission");
        stage.EvaluateAsync(Arg.Any<DomainServiceAdmissionContext>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Admissions++;
            if (rejectAdmission) return DomainServiceAdmissionResult.Rejected([new TerminalRefusalOrdinaryRejected("ordinary-denial")]);
            await protectedState.ReadAsync("untouched", call.Arg<CancellationToken>());
            await protectedState.TryWriteAsync("reservation", "{\"reserved\":true}"u8.ToArray(), "", call.Arg<CancellationToken>());
            return DomainServiceAdmissionResult.Accepted();
        });
        builder.Services.AddSingleton(stage);
        var processor = Substitute.For<IDomainProcessor>();
        processor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(async _ =>
        {
            Processors++;
            await protectedState.TryWriteAsync("successful-write", "{\"value\":7}"u8.ToArray(), "", CancellationToken.None);
            return DomainResult.Success([new TerminalRefusalOrdinarySucceeded(7)]);
        });
        builder.Services.AddKeyedSingleton("counter", processor);
        configure?.Invoke(builder.Services);
        State.SeedByteStore("untouched", "{\"value\":3}"u8.ToArray());
        State.BeforeReadByteState = _ => ProtectedReads++;
        App = builder.Build();
        App.UseRouting(); App.UseAuthentication();
        if (ordinaryReceiver) FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(App, endpoint =>
            routes.Any(route => route.AppId == appId && "/" + route.Method == endpoint.RoutePattern.RawText)
                || auxiliary.Any(operation => operation.AppId == appId && "/" + operation.Method == endpoint.RoutePattern.RawText)
                ? EventStoreDomainServiceRoutes.Operational.SingleOrDefault(route => route.Route == endpoint.RoutePattern.RawText)?.Operation : null);
        App.UseEventStoreHttpHostSecurity(); App.UseAuthorization();
        middleware?.Invoke(App);
        if (probeDelegate)
        {
            App.Services.GetRequiredService<EventStoreDomainServiceEndpointSource>().Capture(App);
            App.UseEventStoreDomainServiceEndpointSecurity();
            App.MapPost("/process", (DomainServiceRequest body) =>
            {
                Delegates++;
                throw new InvalidOperationException("A terminal refusal invoked the endpoint delegate.");
            }).RequireEventStoreDomainServicePolicy("/process");
        }
        else App.MapEventStoreDomainService();
        await App.StartAsync(token);
        if (ordinaryReceiver)
        {
            var finite = App.Services.GetRequiredService<FiniteRouteCatalogReceiver>();
            await finite.WaitForCaptureAsync(token);
            System.Reflection.Assembly[] owners = [typeof(DomainRouteCatalogFilter).Assembly, typeof(RouteCatalogInvocationHandler).Assembly,
                typeof(Hexalith.EventStore.Sample.Counter.CounterAggregate).Assembly, typeof(TerminalRefusalHttpFixture).Assembly];
            var executable = owners.ToDictionary(assembly => assembly.FullName!, assembly =>
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly.Location))), StringComparer.Ordinal);
            for (int index = 0; index < catalog.Observations.Length; index++)
            {
                var observation = catalog.Observations[index];
                catalog.UpdateObservation(index, observation with { ExecutableOwners = executable,
                    ReceiverSurface = observation.AppId == appId ? finite.Observe() : observation.ReceiverSurface,
                    TopologyDocuments = observation.AppId == appId ? [observation.TopologyDocuments[0], ordinaryAcl!] : observation.TopologyDocuments });
            }
            catalog.Recapture = observation => observation.AppId == appId ? observation with { ReceiverSurface = finite.Observe() } : observation;
            Activation = Activation with { RequiredHosts = catalog.Hosts };
            Activation = Activation with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(Activation) };
            if (activate) await catalog.ActivateAsync(Activation);
        }
        Cohort.Workload(Client, "domain-service:process");
    }

    internal HttpRequestMessage Request(object body, string method = "process")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, App.Urls.Single() + "/" + method) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, Activation.RootDigest);
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "1");
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.BindingHeader, RouteCatalogActivationBinding.ComputeDigest(Activation));
        if (fullBinding) request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.ProcessBindingHeader, RouteCatalogActivationBinding.ComputeDigest(Activation));
        return request;
    }

    internal RouteCatalogActivation Successor()
    {
        var current = RouteCatalogCodec.Decode(Activation.CanonicalCatalog);
        var next = RouteCatalogCodec.Seal(current with { Generation = 2,
            Routes = current.Routes.Select(route => route with { Generation = 2 }).ToArray(),
            Idempotency = current.Idempotency.Select(item =>
            {
                var changed = item with { Generation = 2 };
                return changed with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(changed) };
            }).ToArray() });
        var candidate = Catalog.Candidate(2, Activation.RootDigest) with
        { CanonicalCatalog = RouteCatalogCodec.Encode(next), RootDigest = RouteCatalogCodec.ComputeRootDigest(next),
            AuxiliaryOperations = Activation.AuxiliaryOperations, ProjectionCapabilities = Activation.ProjectionCapabilities };
        return candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
    }

    internal static DomainServiceRequest Command(string command = "unsupported", string tenant = "tenant-a", string domain = "counter")
        => new(new("01ARZ3NDEKTSV4RRFFQ69G5FAV", tenant, domain, "aggregate", command, "{}"u8.ToArray(), "correlation", null, "user", null), null);

    internal async Task AssertNoEffectsAsync(int callbacks, bool retainedLease = false)
    {
        Callbacks.ShouldBe(callbacks); Admissions.ShouldBe(0); Processors.ShouldBe(0); Delegates.ShouldBe(0); ProtectedReads.ShouldBe(0);
        State.ByteStoreContains("reservation").ShouldBeFalse();
        State.ByteStoreContains("successful-write").ShouldBeFalse();
        State.TrySaveByteOperations.ShouldBeEmpty();
        System.Text.Encoding.UTF8.GetString(State.ByteStoreValue("untouched")).ShouldBe("{\"value\":3}");
        var leases = (await Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases;
        if (retainedLease) leases.ShouldHaveSingleItem();
        else leases.ShouldBeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose(); State.Dispose();
        if (App is not null) { await App.StopAsync(); await App.DisposeAsync(); }
        await Cohort.DisposeAsync();
        if (_ordinaryDirectory is not null) Directory.Delete(_ordinaryDirectory, recursive: true);
    }
}
