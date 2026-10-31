using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;

/// <summary>Real HTTP, JWT and SDK generated-filter proofs; distributed activation has its independent authority suite.</summary>
public sealed class GeneratedRestRouteCatalogReceiverTests
{
    /// <summary>Admits the lawful parameterized human read and refuses credential, policy and neighboring-handler alternatives before effects.</summary>
    [Theory]
    [InlineData("valid", 200)]
    [InlineData("anonymous", 401)]
    [InlineData("agent", 403)]
    [InlineData("service", 403)]
    [InlineData("missing-surface", 403)]
    [InlineData("duplicate-surface", 403)]
    [InlineData("unknown-surface", 403)]
    [InlineData("mixed-workload", 403)]
    [InlineData("wrong-channel", 401)]
    [InlineData("untyped", 403)]
    [InlineData("typed-command", 403)]
    [InlineData("metadata-change", 403)]
    [InlineData("filter-change", 403)]
    [InlineData("policy-replacement", 403)]
    [InlineData("health", 200)]
    [InlineData("ready", 200)]
    [InlineData("alive", 200)]
    [InlineData("probe-wrong-verb", 403)]
    [InlineData("probe-descendant", 403)]
    [InlineData("catalog-valid", 200)]
    [InlineData("catalog-anonymous", 401)]
    [InlineData("catalog-human", 401)]
    public async Task ActualHumanDaprGetRetainsTypedPolicyAndEffects(string scenario, int expected)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string directory = Path.Combine(Path.GetTempPath(), "generated-rest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        string channel = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = channel;
            builder.Configuration["Authentication:JwtBearer:Issuer"] = "generated-rest-fixture";
            builder.Configuration["Authentication:JwtBearer:Audience"] = "generated-rest-fixture";
            builder.Configuration["Authentication:JwtBearer:SigningKey"] = key;
            builder.Configuration["Authentication:JwtBearer:RequireHttpsMetadata"] = "false";
            builder.Configuration["Authentication:JwtBearer:AllowInsecureSymmetricKey"] = "true";
            builder.Services.AddEventStoreHttpHostSecurity();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = "generated-rest-fixture", ValidateAudience = true, ValidAudience = "generated-rest-fixture",
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
            });
            builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(RouteCatalogEndpointExtensions.WorkloadScheme, "Authentication:RouteCatalogWorkload",
                (options, _) => { options.Audience = "generated-rest-fixture"; options.AllowedCallers = ["eventstore"]; });
            builder.Services.AddEventStoreWorkloadPolicies(RouteCatalogEndpointExtensions.WorkloadScheme,
                [EventStoreOperationIdentifiers.RouteCatalogRead, EventStoreOperationIdentifiers.RouteCatalogActivate]);
            builder.Services.AddSingleton<GeneratedRestRouteCatalogReceiver>();
            builder.Services.AddSingleton<GeneratedRestProbeState>();
            builder.Services.AddHealthChecks();
            builder.Services.AddScoped<GeneratedRouteCatalogFilter>();
            builder.Services.AddControllers(options => options.Filters.AddService<GeneratedRouteCatalogFilter>()).AddApplicationPart(typeof(GeneratedRestProbeController).Assembly);
            RouteContractDeclaration declaration = GeneratedRestProbeDeclarations.Routes.Single();
            RouteCatalogEnvelope catalog = GeneratedRestProbeDeclarations.Catalog();
            var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), 1, localAppId: "generated-api");
            var authority = Substitute.For<IRouteCatalogAuthority>();
            authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
            authority.ReadbackAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RouteCatalogGenerationReadback(1, 1, snapshot.RootDigest,
                catalog.RouteFacetDigest, catalog.IdempotencyFacetDigest, "Development", [], true)));
            builder.Services.AddSingleton(authority);
            await using WebApplication app = builder.Build();
            app.UseRouting(); app.UseAuthentication();
            app.UseEventStoreDevelopmentGeneratedRestCatalogReceiver();
            app.UseEventStoreHttpHostSecurity(); app.UseAuthorization(); app.MapControllers();
            app.MapDefaultEndpoints();
            app.MapEventStoreRouteCatalog();
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            var receiver = app.Services.GetRequiredService<GeneratedRestRouteCatalogReceiver>();
            receiver.Observe().GeneratedRest.ShouldHaveSingleItem().DeclarationDigest.ShouldBe(RouteCatalogCodec.ComputeDeclarationDigest(declaration));
            if (scenario is "metadata-change" or "filter-change" or "policy-replacement")
            {
                RouteEndpoint selected = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/') == "api/probe/{tenantId}");
                if (scenario == "metadata-change") selected.Metadata.GetOrderedMetadata<IAuthorizeData>().First().Policy = "changed-policy";
                else if (scenario == "filter-change") selected.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()!.FilterDescriptors.Clear();
                else app.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.AddPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy,
                    new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAssertion(_ => true).Build());
            }
            using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, scenario == "wrong-channel" ? Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) : channel);
            if (scenario is not ("anonymous" or "catalog-anonymous" or "catalog-valid"))
            {
                List<Claim> claims = [new("sub", "generated-user"), new("eventstore:tenant", "tenant-one")];
                if (scenario != "missing-surface") claims.Add(new("eventstore:surface", scenario is "agent" or "service" ? scenario : scenario == "unknown-surface" ? "other" : "ui"));
                if (scenario == "duplicate-surface") claims.Add(new("eventstore:surface", "ui"));
                var jwt = new JwtSecurityToken("generated-rest-fixture", "generated-rest-fixture", claims, expires: DateTime.UtcNow.AddMinutes(2), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
            }
            if (scenario == "mixed-workload") client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
            if (scenario == "catalog-valid")
            {
                var descriptor = new SecurityTokenDescriptor
                {
                    Issuer = "generated-rest-fixture", Audience = "generated-rest-fixture", IssuedAt = DateTime.UtcNow,
                    Expires = DateTime.UtcNow.AddMinutes(2), Claims = new Dictionary<string, object>
                    {
                        [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore",
                        [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreOperationIdentifiers.RouteCatalogRead,
                        ["eventstore:surface"] = "service",
                    }, SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
                };
                client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor));
            }
            using HttpResponseMessage response = scenario.StartsWith("catalog-", StringComparison.Ordinal)
                ? await client.GetAsync("/" + RouteCatalogEndpointExtensions.ReadbackMethod, TestContext.Current.CancellationToken).ConfigureAwait(true)
                : scenario is "health" or "ready" or "alive"
                ? await client.GetAsync("/" + scenario, TestContext.Current.CancellationToken).ConfigureAwait(true)
                : scenario == "probe-wrong-verb"
                ? await client.PostAsync("/ready", null, TestContext.Current.CancellationToken).ConfigureAwait(true)
                : scenario == "probe-descendant"
                ? await client.GetAsync("/ready/child", TestContext.Current.CancellationToken).ConfigureAwait(true)
                : scenario == "typed-command"
                ? await client.PostAsync("/api/command/tenant-one", null, TestContext.Current.CancellationToken).ConfigureAwait(true)
                : await client.GetAsync(scenario == "untyped" ? "/api/untyped/tenant-one" : "/api/probe/tenant-one", TestContext.Current.CancellationToken).ConfigureAwait(true);
            response.StatusCode.ShouldBe((HttpStatusCode)expected);
            if (scenario == "catalog-valid")
            {
                using System.Text.Json.JsonDocument readback = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
                readback.RootElement.GetProperty("rootDigest").GetString().ShouldBe(snapshot.RootDigest);
                readback.RootElement.GetProperty("serving").GetBoolean().ShouldBeTrue();
            }
            app.Services.GetRequiredService<GeneratedRestProbeState>().Tenants.ShouldBe(scenario == "valid" ? ["tenant-one"] : Array.Empty<string>());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
