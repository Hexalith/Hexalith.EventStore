using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Commands;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.Services;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Client.Tests.Projections;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Real Kestrel/JWT/MVC fence and real CAS catalog authority; no Dapr CLI or sidecar is started.</summary>
internal sealed class TechnicalHttpCatalogFixture : IAsyncDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sdk-technical-http-").FullName;
    private readonly string _key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private readonly string _channel = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public RecordingDaprClient State { get; } = new() { PersistTypedState = true, TrySaveResult = true };
    public RouteCatalogTestFixture Catalog { get; }
    public TechnicalHttpCatalogFixture(RouteCatalogStateCoordinates? coordinates = null, bool compiledSchemaThree = true)
        => Catalog = new(coordinates, ["eventstore-admin-server"], compiledSchemaThree);
    public ITenantCommandService TenantCommands { get; } = Substitute.For<ITenantCommandService>();

    public async Task StartAsync(CancellationToken token, Action<IServiceCollection>? services = null, Action<WebApplication>? endpoints = null, bool fullInventory = false, bool activate = true, string? inventoryAssembly = null, string? attempt = null, bool includeBusiness = false,
        IReadOnlyList<RouteCatalogDeclaredInvocation>? invocations = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        invocations ??= [new("technical-ui-proxy", "/api/v1/admin", ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"])];
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"] = System.Text.Json.JsonSerializer.Serialize(invocations);
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = _directory;
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:ReceiverProfile"] = RouteCatalogTechnicalHttpPolicy.FenceVersion;
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = _channel;
        builder.Configuration["Authentication:JwtBearer:Issuer"] = "technical-fixture";
        builder.Configuration["Authentication:JwtBearer:Audience"] = "technical-fixture";
        builder.Configuration["Authentication:JwtBearer:SigningKey"] = _key;
        builder.Configuration["Authentication:JwtBearer:RequireHttpsMetadata"] = "false";
        builder.Configuration["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true";
        builder.Services.AddAdminApi(builder.Configuration);
        builder.Services.AddSingleton<DaprClient>(State);
        builder.Services.AddSingleton(Substitute.For<IActorProxyFactory>());
        builder.Services.AddSingleton<DaprSnapshotPolicyRepository>();
        builder.Services.AddSingleton<Microsoft.Extensions.Options.IOptions<CommandStatusOptions>>(Microsoft.Extensions.Options.Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }));
        var tenants = Substitute.For<Hexalith.EventStore.Authorization.ITenantValidator>();
        tenants.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>()).Returns(TenantValidationResult.Allowed);
        builder.Services.AddSingleton(tenants);
        var rbac = Substitute.For<IRbacValidator>();
        rbac.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>()).Returns(RbacValidationResult.Allowed);
        builder.Services.AddSingleton(rbac);
        builder.Services.AddScoped<Hexalith.EventStore.Authorization.AdminTenantAuthorizationFilter>();
        builder.Services.AddSingleton(Substitute.For<ICommandActivityReader>());
        builder.Services.AddSingleton<ITenantCommandService>(TenantCommands);
        builder.Services.AddSingleton(Substitute.For<ITenantQueryService>());
        TenantCommands.AddUserToTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(true, "owned-admin-action", "accepted", null));
        builder.Services.AddEventStoreHttpHostSecurity();
        builder.Services.PostConfigure<AuthorizationOptions>(options =>
        {
            foreach (string name in new[] { AdminAuthorizationPolicies.ReadOnly, AdminAuthorizationPolicies.Operator, AdminAuthorizationPolicies.Admin })
                options.AddPolicy(name, new AuthorizationPolicyBuilder(options.GetPolicy(name)!).Combine(EventStoreHttpHostSecurityExtensions.CreateHumanPolicy()).Build());
        });
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new()
        {
            ValidateIssuer = true, ValidIssuer = "technical-fixture", ValidateAudience = true, ValidAudience = "technical-fixture",
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        });
        builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(RouteCatalogEndpointExtensions.WorkloadScheme, "Authentication:RouteCatalogWorkload",
            (options, _) => { options.Audience = "technical-fixture"; options.AllowedCallers = ["eventstore"]; });
        builder.Services.AddEventStoreWorkloadPolicies(RouteCatalogEndpointExtensions.WorkloadScheme, [EventStoreOperationIdentifiers.RouteCatalogRead, EventStoreOperationIdentifiers.RouteCatalogActivate]);
        if (includeBusiness || fullInventory)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(Hexalith.EventStore.Authentication.DaprInternalAuthenticationOptions.SchemeName,
                "Authentication:Workload", (options, _) => { options.Audience = "technical-fixture"; options.AllowedCallers = ["eventstore-admin-server"]; });
            builder.Services.AddEventStoreWorkloadPolicies(Hexalith.EventStore.Authentication.DaprInternalAuthenticationOptions.SchemeName, [EventStoreOperationIdentifiers.GatewayCommandSubmit, EventStoreOperationIdentifiers.GatewayCommandStatus, EventStoreOperationIdentifiers.GatewayStreamRead]);
            builder.Services.AddTransient<Hexalith.EventStore.Validation.ExtensionMetadataSanitizer>();
            builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.MapInboundClaims = false);
        }
        builder.Services.AddSingleton<IRouteCatalogAuthority>(Catalog.Authorities[0]);
        builder.Services.AddSingleton<TechnicalHttpRouteCatalogReceiver>();
        builder.Services.AddHealthChecks();
        builder.Services.AddScoped<GeneratedRouteCatalogFilter>();
        builder.Services.AddControllers(options => options.Filters.AddService<GeneratedRouteCatalogFilter>()).AddApplicationPart(typeof(AdminStorageCommandController).Assembly).AddApplicationPart(typeof(AdminTenantsController).Assembly)
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new TechnicalHttpControllerFeatureProvider(fullInventory, inventoryAssembly, includeBusiness)));
        services?.Invoke(builder.Services);
        App = builder.Build();
        if (includeBusiness) App.Use(async (context, next) =>
        {
            try { await next(context).ConfigureAwait(false); }
            catch (Hexalith.EventStore.ErrorHandling.CommandAuthorizationException) { context.Response.StatusCode = 403; }
            catch (RouteCatalogUnavailableException) { context.Response.StatusCode = 503; }
        });
        App.UseRouting(); App.UseAuthentication();
        TechnicalHttpRouteCatalogReceiver.UseDevelopmentTechnicalHttpCatalogReceiver(App, RunScopedRouteCatalogGateway.Operation);
        App.UseEventStoreHttpHostSecurity(); App.UseAuthorization();
        App.MapControllers(); App.MapDefaultEndpoints(); App.MapEventStoreRouteCatalog();
        App.MapGet("/dapr/subscribe", () => Results.Ok(Array.Empty<object>())).RequireEventStoreChannel();
        App.MapPost("/api/v1/admin/unmarked", () => Results.Ok()).RequireAuthorization(EventStoreHttpHostSecurityExtensions.HumanPolicy);
        endpoints?.Invoke(App);
        await App.StartAsync(token).ConfigureAwait(false);
        await App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>().WaitForCaptureAsync(token).ConfigureAwait(false);
        var fence = App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>();
        var observed = Catalog.Observations[0] with { ReceiverSurface = fence.Observe() };
        if (invocations is not null)
        {
            byte[] acl = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new
            {
                defaultAction = "deny", trustDomain = "public", policies = invocations.GroupBy(invocation => invocation.CallerAppId).Select(group => new
                { appId = group.Key, defaultAction = "deny", operations = group.Select(invocation => new { name = invocation.Method, action = "allow", httpVerb = invocation.HttpMethods }).ToArray() }).ToArray(),
            } } });
            observed = observed with { DeclaredInvocations = invocations, SelectedAppIds = Catalog.Hosts.Select(host => host.AppId).Concat(invocations.Select(invocation => invocation.CallerAppId)).Distinct(StringComparer.Ordinal).ToArray(),
                TopologyDocuments = [observed.TopologyDocuments[0], acl] };
        }
        Catalog.UpdateObservation(0, observed);
        Catalog.Recapture = actual => actual.HostId == Catalog.Hosts[0].HostId ? actual with { ReceiverSurface = fence.Observe() } : actual;
        if (activate) await Catalog.ActivateAsync(Catalog.Candidate(attempt: attempt)).ConfigureAwait(false);
        Client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        { BaseAddress = new Uri(App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        Human();
    }

    public void Human(string? surface = "ui", string role = "Admin", bool duplicate = false, bool channel = true)
    {
        Client.DefaultRequestHeaders.Clear();
        if (channel) Client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, _channel);
        List<Claim> claims = [new("sub", "technical-user"), new("eventstore:tenant", "tenant-a"), new("eventstore:admin-role", role)];
        if (surface is not null) claims.Add(new("eventstore:surface", surface));
        if (duplicate) claims.Add(new("eventstore:surface", surface!));
        var jwt = new JwtSecurityToken("technical-fixture", "technical-fixture", claims, expires: DateTime.UtcNow.AddMinutes(2),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256));
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
    }

    public void ConfigureWorkloadReceiver(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        foreach (string key in new[] { "Issuer", "Audience", "SigningKey", "RequireHttpsMetadata", "AllowInsecureSymmetricKey" })
            configuration["Authentication:JwtBearer:" + key] = App.Configuration["Authentication:JwtBearer:" + key];
        configuration[DaprAppChannelToken.ConfigurationKey] = _channel;
    }

    public void Workload(HttpClient client, string operation, string? surface = "service", bool duplicate = false,
        string caller = "eventstore", string? tenant = null, string? domain = null)
    {
        client.DefaultRequestHeaders.Clear(); client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, _channel);
        List<Claim> claims = [new("azp", caller), new("eventstore:operation", operation)];
        if (tenant is not null) claims.Add(new(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, tenant));
        if (domain is not null) claims.Add(new(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, domain));
        if (surface is not null) claims.Add(new("eventstore:surface", surface));
        if (duplicate) claims.Add(new("eventstore:surface", surface!));
        var jwt = new JwtSecurityToken("technical-fixture", "technical-fixture", claims, notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(2), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256));
        jwt.Payload["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, new JwtSecurityTokenHandler().WriteToken(jwt));
    }

    public void Coordinator(string operation)
    {
        Client.DefaultRequestHeaders.Clear(); Client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, _channel);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "technical-fixture", Audience = "technical-fixture", IssuedAt = DateTime.UtcNow, Expires = DateTime.UtcNow.AddMinutes(2),
            Claims = new Dictionary<string, object> { ["azp"] = "eventstore", ["eventstore:operation"] = operation, ["eventstore:surface"] = "service" },
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256),
        };
        Client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
            new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor));
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (App is not null) { await App.StopAsync().ConfigureAwait(false); await App.DisposeAsync().ConfigureAwait(false); }
        Directory.Delete(_directory, recursive: true);
    }
}
