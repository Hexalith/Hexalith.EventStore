using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Contracts.Routing;

using MediatR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises production MVC validation, signed bearer authentication and catalog ingress before a counted component terminal.</summary>
public sealed class PlatformCommandModelValidationTests
{
    [Theory]
    [InlineData("bootstrap", 403)]
    [InlineData("public-create", 202)]
    [InlineData("missing-permission", 403)]
    [InlineData("wrong-scope", 403)]
    [InlineData("unknown-scope", 403)]
    [InlineData("mixed-scopes", 400)]
    [InlineData("malformed-scope", 400)]
    [InlineData("reserved-storage", 403)]
    [InlineData("wrong-kind", 403)]
    [InlineData("missing-credential", 401)]
    public async Task StructuralValidationReachesExistingScopeAndIngressGuardsWithoutGrantingAuthority(string scenario, int expectedStatus)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var original = new RouteCatalogTestFixture().Envelope;
        RouteOperationContract[] operations = original.Declarations.Select(declaration =>
            TenantCatalogDeclarations.Operations.SingleOrDefault(operation => operation.RouteId == declaration.RouteId)
            ?? new RouteOperationContract(declaration.RouteId, declaration.ContractVersion,
                declaration.Kind == "projection" || declaration.Execution!.RequiresDelegatedContext ? "internal" : "public", "agent-eligible",
                JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }), string.Empty,
                declaration.Kind, declaration.Domain, declaration.MessageType ?? declaration.ProjectionType))
            .Select(operation => operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) }).ToArray();
        var envelope = VersionedRouteCatalogBuilder.Build(new(1, "Development", original.Declarations, operations,
            original.Routes.Select(route => new RouteCompositionBinding(route.RouteId, route.AppId, route.Method,
                route.ContractVersion, false, route.ResolverTenantPattern)).ToArray(), original.Idempotency));
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        int leases = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            leases++;
            return new RouteCatalogDispatchLease(snapshot, () => { leases--; return Task.CompletedTask; });
        });
        int effects = 0;
        var terminal = Substitute.For<IRequestHandler<SubmitCommand, SubmitCommandResult>>();
        SubmitCommand? captured = null;
        terminal.Handle(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            leases.ShouldBeGreaterThan(0);
            captured = call.Arg<SubmitCommand>();
            effects++;
            return new SubmitCommandResult(captured.CorrelationId);
        });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        const string signingText = "canonical-platform-model-validation-0123456789abcdef";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = "platform-validation-test",
            ["Authentication:JwtBearer:Audience"] = "platform-validation-test",
            ["Authentication:JwtBearer:SigningKey"] = signingText,
        });
        builder.Services.AddEventStore();
        // Only unrelated background infrastructure and the mutation terminal are replaced in this
        // HTTP component host. Real MVC validation, bearer, human-kind, scope and ingress guards run.
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<IRequestHandler<SubmitCommand, SubmitCommandResult>>();
        builder.Services.AddSingleton(terminal);
        builder.Services.AddSingleton(authority);
        builder.Services.AddSingleton(Substitute.For<ITenantValidator>());
        builder.Services.AddSingleton(Substitute.For<IRbacValidator>());
        builder.Services.AddControllers().AddApplicationPart(typeof(Hexalith.EventStore.Controllers.CommandsController).Assembly)
            .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new CatalogV3MvcFeature()));
        byte[] signing = Encoding.UTF8.GetBytes(signingText);
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = "platform-validation-test", ValidateAudience = true,
                ValidAudience = "platform-validation-test", ValidateLifetime = true, IssuerSigningKey = new SymmetricSecurityKey(signing),
            };
        });
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (CommandAuthorizationException) { context.Response.StatusCode = 403; }
        });
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient();
        bool bootstrap = scenario == "bootstrap";
        string scope = bootstrap ? "platform:global-administrators" : "platform:tenants";
        if (scenario == "wrong-scope") scope = "platform:global-administrators";
        if (scenario == "unknown-scope") scope = "platform:unknown-owner";
        if (scenario == "malformed-scope") scope = "platform:tenants--bad";
        if (scenario == "reserved-storage") scope = "system";
        var claims = new List<Claim>
        {
            new("sub", "administrator"), new("eventstore:tenant", "tenant-a"), new("global_admin", "true"),
            new(RouteCatalogSurfacePolicy.ClaimType, "ui"),
        };
        if (scenario != "missing-permission") claims.Add(new("eventstore:permission", bootstrap ? "global-administrators:bootstrap" : "tenants:create"));
        if (scenario == "wrong-kind") claims.Add(new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "workload-test"));
        string bearer = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "platform-validation-test", Audience = "platform-validation-test", Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new(new SymmetricSecurityKey(signing), SecurityAlgorithms.HmacSha256), Subject = new ClaimsIdentity(claims),
        });
        if (scenario != "missing-credential") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commands")
        {
            Content = JsonContent.Create(new SubmitCommandRequest("01JAAAAAAAAAAAAAAAAAAAAAAA", scope,
                bootstrap ? "global-administrators" : "tenants", bootstrap ? "global-administrators" : "managed-tenant",
                bootstrap ? "BootstrapGlobalAdmin" : "CreateTenant", bootstrap
                    ? JsonSerializer.SerializeToElement(new { userId = "administrator" })
                    : JsonSerializer.SerializeToElement(new { tenantId = "managed-tenant", name = "component" }))),
        };
        if (scope != "system") request.Headers.Add("X-Hexalith-Scope", scope);
        if (scenario == "mixed-scopes") request.Headers.Add("X-Hexalith-Tenant", "tenant-a");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(expectedStatus);
        effects.ShouldBe(scenario == "public-create" ? 1 : 0);
        leases.ShouldBe(0);
        if (captured is not null)
        {
            captured.Tenant.ShouldBe("system");
            captured.UserId.ShouldBe("administrator");
            captured.AuthenticatedSubject.ShouldBe("administrator");
        }
        if (response.StatusCode == HttpStatusCode.BadRequest && scenario == "malformed-scope")
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("tenant");
    }
}
