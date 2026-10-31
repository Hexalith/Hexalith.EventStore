using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

internal sealed class TechnicalAdminHost : IAsyncDisposable
{
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private static readonly string Channel = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "technical-admin-" + Guid.NewGuid().ToString("N"));
    private WebApplication _app = null!;
    private WebApplication _domain = null!;
    private NativeTechnicalAdminFactory? _native;
    internal TechnicalCatalogMemoryStore Store { get; } = new();
    internal Dapr.Client.DaprClient Dapr { get; } = Substitute.For<Dapr.Client.DaprClient>();
    internal IDaprInfrastructureQueryService Backend { get; } = Substitute.For<IDaprInfrastructureQueryService>();
    internal HttpClient Client { get; private set; } = null!;
    internal string Root { get; private set; } = "";
    internal HttpClient CreateOwnerClient() => _app.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
    internal long Epoch { get; private set; } = 1;
    internal string DirectoryPath => _directory;
    internal IRouteCatalogAuthority Authority => (_native?.Services ?? _app.Services).GetRequiredService<IRouteCatalogAuthority>();
    internal int ForeignCalls { get; private set; }
    internal IReadOnlyList<RouteEndpoint> Endpoints => ((IEndpointRouteBuilder)_app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();

    internal async Task StartAsync(string? grant = null, string? ownerEndpoint = null, string? ownerToken = null, bool nativeBootstrap = false)
    {
        Directory.CreateDirectory(_directory);
        string[] grantVerbs = grant is null ? ["GET", "POST", "PUT"]
            : RouteCatalogEndpointExtensions.IsControlMethod(grant.Trim('/'), HttpMethod.Post) ? ["POST"] : ["GET"];
        string acl = Path.Combine(_directory, "acl.json");
        string component = Path.Combine(_directory, "component.json");
        await File.WriteAllTextAsync(acl, """{"spec":{"accessControl":{"defaultAction":"deny","trustDomain":"public","policies":[{"appId":"admin-test","defaultAction":"deny","operations":[{"name":"/**","httpVerb":["GET","POST","PUT"],"action":"allow"}]}]}}}""");
        if (grant is not null) await File.WriteAllTextAsync(acl, JsonSerializer.Serialize(new { spec = new { accessControl = new {
            defaultAction = "deny", trustDomain = "public", policies = new[] { new { appId = "admin-test", defaultAction = "deny", operations = new[] { new { name = grant, httpVerb = grantVerbs, action = "allow" } } } } } } }));
        await File.WriteAllTextAsync(component, """{"kind":"Component","metadata":{"name":"route-catalog"},"spec":{"type":"state.redis","metadata":[{"name":"keyPrefix","value":"none"}]},"scopes":["admin-test","domain-test"]}""");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APP_API_TOKEN"] = Channel,
            ["AdminServer:HealthHistoryEnabled"] = "false",
            ["AdminServer:EventStoreDaprHttpEndpoint"] = ownerEndpoint,
            ["AdminServer:EventStoreAppId"] = "domain-test",
            ["EventStore:RouteCatalog:DevelopmentActorStateOwners:0:AppId"] = ownerEndpoint is null ? null : "domain-test",
            ["EventStore:RouteCatalog:DevelopmentActorStateOwners:0:HttpEndpoint"] = ownerEndpoint,
            ["EventStore:RouteCatalog:DevelopmentActorStateOwners:0:ApiToken"] = ownerToken,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = _directory,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:HostId"] = "admin",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:ComponentPath"] = component,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:0"] = "admin",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:1"] = "domain",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:0"] = "admin-test",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:1"] = "domain-test",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"] = JsonSerializer.Serialize(new[] { new RouteCatalogDeclaredInvocation("admin-test", grant ?? "/**", grantVerbs) }),
        });
        if (nativeBootstrap)
        {
            var config = builder.Configuration.AsEnumerable().Where(pair => pair.Key.StartsWith("EventStore:", StringComparison.Ordinal) || pair.Key.StartsWith("AdminServer:", StringComparison.Ordinal) || pair.Key == "APP_API_TOKEN").ToDictionary(pair => pair.Key, pair => pair.Value);
            config["EventStore:RouteCatalog:DevelopmentBootstrap:Role"] = "admin-receiver";
            config["EventStore:RouteCatalog:DevelopmentBootstrap:CoordinatorHostId"] = "domain";
            config["EventStore:RouteCatalog:DevelopmentBootstrap:AttemptId"] = "technical-attempt";
            config["EventStore:DomainService:AppId"] = "admin-test";
            config["Authentication:JwtBearer:Issuer"] = "technical-admin";
            config["Authentication:JwtBearer:Audience"] = "admin-test";
            config["Authentication:JwtBearer:SigningKey"] = Key;
            config["Authentication:JwtBearer:RequireHttpsMetadata"] = "false";
            config["EventStore:Admin:OpenApi:Enabled"] = "false";
            await StartDomainAsync(component, bootstrap: true);
            _native = new NativeTechnicalAdminFactory(config, Store, Dapr, Backend);
            Client = _native.CreateClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            while (!File.Exists(Path.Combine(_directory, "admin.committed.json")) || !File.Exists(Path.Combine(_directory, "domain.committed.json")))
                await Task.Delay(25, timeout.Token);
            var committed = (await Store.ReadAsync(default)).State.Committed!;
            Root = committed.RootDigest;
            return;
        }
        builder.Services.AddSingleton(Dapr);
        builder.Services.AddAdminApi(builder.Configuration);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "technical-admin", ValidateAudience = true, ValidAudience = "admin-test",
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            });
        builder.Services.AddEventStoreHttpHostSecurity();
        builder.Services.PostConfigure<AuthorizationOptions>(options =>
        {
            foreach (string policy in new[] { AdminAuthorizationPolicies.ReadOnly, AdminAuthorizationPolicies.Operator, AdminAuthorizationPolicies.Admin })
                options.AddPolicy(policy, new AuthorizationPolicyBuilder(options.GetPolicy(policy)!).Combine(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy()).Build());
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminHealthController).Assembly);
        if (ownerEndpoint is null) builder.Services.AddSingleton(Backend);
        else builder.Services.AddScoped<IDaprInfrastructureQueryService, DaprInfrastructureQueryService>();
        builder.Services.AddSingleton(Substitute.For<IAdminAuthContext>());
        builder.Services.AddEventStoreRouteCatalog(builder.Configuration);
        builder.Services.AddSingleton<IRouteCatalogStateStore>(Store);
        builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
        builder.Services.Configure<RouteCatalogHostOptions>(options => { options.Profile = "Development"; options.DevelopmentFixture = true; });
        builder.Services.AddEventStoreRouteCatalogObservation(async (provider, token) =>
        {
            var surface = provider.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            return (await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, "admin-test", [], [], [], [],
                DevelopmentTechnicalHttpTransport.Auxiliary(surface, "admin-test", "admin"), token)) with { ReceiverSurface = surface };
        });
        _app = builder.Build();
        _app.UseRouting(); _app.UseEventStoreHttpHostSecurity(); _app.UseAuthentication(); _app.UseAuthorization();
        _app.MapControllers();
        _app.MapEventStoreRouteCatalog();
        _app.MapGet("/api/v1/admin/foreign/{id}", () => { ForeignCalls++; return "forbidden"; })
            .RequireAuthorization(AdminAuthorizationPolicies.ReadOnly);
        FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(_app, DevelopmentTechnicalHttpTransport.Operation, technical: true);
        await _app.StartAsync(TestContext.Current.CancellationToken);
        var surface = _app.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
        var observation = (await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, "admin-test", [], [], [], [],
            DevelopmentTechnicalHttpTransport.Auxiliary(surface, "admin-test", "admin"))) with { ReceiverSurface = surface, AuthorityCoordinates = Store.Coordinates };
        var identity = ObservedRouteCatalogHost.Identity(observation);
        _app.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = identity;
        RouteCatalogHostObservation domain = await StartDomainAsync(component);
        var domainIdentity = ObservedRouteCatalogHost.Identity(domain);
        _domain.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = domainIdentity;
        var envelope = RouteCatalogCodec.Seal(new RouteCatalogEnvelope(3, 1, "Development", false, domain.Declarations, domain.Targets, [], "", "", domain.Operations, []));
        Root = RouteCatalogCodec.ComputeRootDigest(envelope);
        var activation = new RouteCatalogActivation("technical-attempt", RouteCatalogCodec.Encode(envelope), Root, null,
            [identity, domainIdentity], "Development-fixture", "", observation.AuxiliaryOperations);
        activation = activation with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(activation) };
        var authority = (_native?.Services ?? _app.Services).GetRequiredService<IRouteCatalogAuthority>();
        var domainAuthority = _domain.Services.GetRequiredService<IRouteCatalogAuthority>();
        await authority.PrepareAsync(activation); await domainAuthority.PrepareAsync(activation);
        await authority.ReadyAsync(activation.AttemptId); await domainAuthority.ReadyAsync(activation.AttemptId);
        await authority.CommitAsync(activation.AttemptId);
        Client = new HttpClient { BaseAddress = new Uri(_app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    private async Task<RouteCatalogHostObservation> StartDomainAsync(string component, bool bootstrap = false)
    {
        string acl = Path.Combine(_directory, "domain-acl.json");
        await File.WriteAllTextAsync(acl, """{"spec":{"accessControl":{"defaultAction":"deny","trustDomain":"public","policies":[]}}}""");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:DomainService:AppId"] = "domain-test", ["EventStore:DomainService:ServiceVersion"] = "v1",
            ["EventStore:DataProtection:PersistToStateStore"] = "false", ["APP_API_TOKEN"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Authentication:JwtBearer:Issuer"] = "technical-admin", ["Authentication:JwtBearer:Audience"] = "domain-test",
            ["Authentication:JwtBearer:SigningKey"] = Key, ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:HostId"] = "domain",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:ComponentPath"] = component,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl,
            ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:0"] = "admin",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:RequiredHostIds:1"] = "domain",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:0"] = "admin-test",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:SelectedAppIds:1"] = "domain-test",
            ["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"] = "[]",
        });
        if (bootstrap)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = _directory,
                ["EventStore:RouteCatalog:DevelopmentBootstrap:CoordinatorHostId"] = "domain",
                ["EventStore:RouteCatalog:DevelopmentBootstrap:AttemptId"] = "technical-attempt",
            });
        }
        builder.AddEventStoreDomainService(typeof(TechnicalQueryHandler).Assembly);
        builder.Services.RemoveAll<IDomainQueryHandler>(); builder.Services.AddScoped<IDomainQueryHandler, TechnicalQueryHandler>();
        builder.Services.AddSingleton<IRouteCatalogStateStore>(Store);
        builder.Services.Configure<RouteCatalogHostOptions>(options => { options.Profile = "Development"; options.DevelopmentFixture = true; });
        async Task<RouteCatalogHostObservation> Capture(IServiceProvider provider, CancellationToken token)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IDomainQueryHandler>().ShouldBeOfType<TechnicalQueryHandler>();
            provider.GetRequiredService<EventStoreDomainServiceEndpointSource>().Endpoints!.DataSources.SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>().ShouldContain(endpoint => endpoint.RoutePattern.RawText == "/query");
            var declaration = TechnicalQueryHandler.Declaration;
            var operation = RouteOperationSchema.Create(declaration, typeof(QueryEnvelope), typeof(QueryResult), "internal", "UI-only", JsonSerializerOptions.Web);
            var target = new RouteCatalogEntry(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
                "domain-test", "query", "v1", declaration.CredentialKind, declaration.Operation, false, "tenant-a", operation.Digest);
            return (await DevelopmentRouteCatalogInputs.CaptureAsync(builder.Configuration, "domain-test", [declaration], [target], [], [], [], token))
                with { Operations = [operation], AuthorityCoordinates = Store.Coordinates };
        }
        if (bootstrap)
        {
            builder.Services.AddEventStoreDevelopmentRouteCatalogBootstrap(builder.Configuration, Capture, observations => RouteCatalogCodec.Seal(new RouteCatalogEnvelope(3, 1, "Development", false,
                observations.SelectMany(observation => observation.Declarations).ToArray(), observations.SelectMany(observation => observation.Targets).ToArray(), [], "", "",
                observations.SelectMany(observation => observation.Operations ?? []).ToArray(), [])));
        }
        else builder.Services.AddEventStoreRouteCatalogObservation(Capture);
        _domain = builder.Build(); _domain.UseEventStoreDomainService();
        await _domain.StartAsync(TestContext.Current.CancellationToken);
        return await Capture(_domain.Services, TestContext.Current.CancellationToken);
    }

    internal HttpRequestMessage Request(string path, string? role = "Admin", bool generation = true, string? channel = null)
    {
        var claims = new List<Claim> { new("sub", "technical-human"), new("eventstore:surface", "ui") };
        if (role is not null) claims.Add(new(AdminClaimTypes.AdminRole, role));
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "technical-admin", Audience = "admin-test", Expires = DateTime.UtcNow.AddMinutes(5), Subject = new ClaimsIdentity(claims),
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256),
        });
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(DaprAppChannelToken.HeaderName, channel ?? Channel);
        if (generation) { request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, Root); request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        return request;
    }

    internal async Task AdvanceAsync()
    {
        var before = (await Store.ReadAsync(default)).State.Committed!;
        var envelope = RouteCatalogCodec.Decode(before.CanonicalCatalog, before.RootDigest);
        envelope = RouteCatalogCodec.Seal(envelope with { Generation = 2, Routes = envelope.Routes.Select(route => route with { Generation = 2 }).ToArray() });
        var next = before with { AttemptId = "technical-next", CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope), PredecessorRootDigest = before.RootDigest };
        next = next with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(next) };
        var peer = _domain.Services.GetRequiredService<IRouteCatalogAuthority>();
        await Authority.PrepareAsync(next); await peer.PrepareAsync(next);
        await Authority.ReadyAsync(next.AttemptId); await peer.ReadyAsync(next.AttemptId); await Authority.CommitAsync(next.AttemptId);
        Root = next.RootDigest; Epoch = 2;
    }

    internal async Task<HttpResponseMessage> SendThroughGuardAsync(HttpRequestMessage request)
    {
        var services = new ServiceCollection();
        services.AddSingleton((_native?.Services ?? _app.Services).GetRequiredService<IRouteCatalogAuthority>());
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        services.AddHttpClient("technical-sender", client => client.BaseAddress = Client.BaseAddress)
            .AddEventStoreDaprServiceInvocation("admin-test", Channel);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("technical-sender");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_native is not null) await _native.DisposeAsync();
        if (_app is not null) await _app.DisposeAsync();
        if (_domain is not null) await _domain.DisposeAsync();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
