using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Validation;

using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public class RouteCatalogV3IngressTests
{
    [Fact]
    public void BootstrapRefusesAValidCatalogBoundToAnotherRunBeforeRegisteringAuthority()
    {
        var fixture = new RouteCatalogTestFixture();
        string directory = Path.Combine(Path.GetTempPath(), "catalog-cross-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "catalog.json"), RouteCatalogCodec.Encode(fixture.Envelope));
            string root = RouteCatalogCodec.ComputeRootDigest(fixture.Envelope);
            File.WriteAllBytes(Path.Combine(directory, "receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new { instanceId = "run-one-catalog", rootDigest = root }));
            var values = new Dictionary<string, string?>
            {
                ["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory,
                ["EventStore:RouteCatalog:DevelopmentBootstrap:HostId"] = "eventstore",
                ["EventStore:RouteCatalog:DevelopmentBootstrap:CoordinatorHostId"] = "eventstore",
                ["EventStore:RouteCatalog:DevelopmentBootstrap:AttemptId"] = "attempt-two",
                ["EventStore:RouteCatalog:DevelopmentBootstrap:InstanceId"] = "run-two-catalog",
                ["EventStore:RouteCatalog:DevelopmentBootstrap:CatalogPath"] = Path.Combine(directory, "catalog.json"),
                ["EventStore:RouteCatalog:DevelopmentBootstrap:RootDigest"] = root,
            };
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var services = new ServiceCollection();
            Should.Throw<InvalidOperationException>(() => services.AddEventStoreDevelopmentRouteCatalogBootstrap(configuration,
                (_, _) => throw new InvalidOperationException("No observer may run"), _ => fixture.Envelope));
            services.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task ActualHttpCommandQueryAndGeneratedIngressRefuseAgentBeforeEffectsAndMetadataExcludesHistory()
    {
        var original = new RouteCatalogTestFixture().Envelope;
        string removed = original.Declarations.First(d => d.Kind == "command" && d.RouteId != "counter-increment-counter").RouteId;
        var declarations = original.Declarations.Where(d => d.RouteId != removed).ToArray();
        var operations = declarations.Select(d => new RouteOperationContract(d.RouteId, d.ContractVersion,
            d.Kind == "projection" || d.Execution!.RequiresDelegatedContext ? "internal" : "public", "UI-only",
            JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }), string.Empty,
            d.Kind, d.Domain, d.MessageType ?? d.ProjectionType)).Select(o => o with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(o) }).ToArray();
        var policies = original.Idempotency.Where(i => i.RouteId != removed).Select(i => i with { Generation = 2 })
            .Select(i => i with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(i) }).ToArray();
        var envelope = VersionedRouteCatalogBuilder.Build(new(2, "Development", declarations, operations,
            original.Routes.Where(r => r.RouteId != removed).Select(r => new RouteCompositionBinding(r.RouteId, r.AppId, r.Method, r.ContractVersion, false, r.ResolverTenantPattern)).ToArray(),
            policies, RouteCatalogCodec.Encode(original), RouteCatalogCodec.ComputeRootDigest(original)));
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        var removedDeclaration = original.Declarations.Single(d => d.RouteId == removed);
        Should.Throw<RouteCatalogUnavailableException>(() => snapshot.Resolve("command", "tenant-a", removedDeclaration.Domain, removedDeclaration.MessageType!));
        snapshot.Envelope.RetainedIdempotency!.ShouldContain(r => r.Entry.RouteId == removed);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        int leases = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            leases++;
            return new RouteCatalogDispatchLease(snapshot, () => { leases--; return Task.CompletedTask; });
        });
        var tenant = Substitute.For<ITenantValidator>();
        tenant.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(TenantValidationResult.Allowed);
        var rbac = Substitute.For<IRbacValidator>();
        rbac.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(RbacValidationResult.Allowed);
        var commandEffects = new CatalogGeneratedEffectCounter();
        var generated = new CatalogGeneratedEffectCounter();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" }); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        const string signingText = "schema-three-http-test-signing-key-0123456789abcdef";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Issuer"] = "catalog-test", ["Authentication:JwtBearer:Audience"] = "catalog-test",
            ["Authentication:JwtBearer:SigningKey"] = signingText,
        });
        builder.Services.AddEventStore();
        builder.Services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
        builder.Services.RemoveAll<IRequestHandler<SubmitCommand, SubmitCommandResult>>();
        builder.Services.AddSingleton<IRequestHandler<SubmitCommand, SubmitCommandResult>>(new CatalogV3CommandHandler(commandEffects, () => leases > 0));
        builder.Services.AddSingleton(authority); builder.Services.AddSingleton(tenant); builder.Services.AddSingleton(rbac);
        builder.Services.AddSingleton(generated); builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(Substitute.For<IETagService>());
        builder.Services.AddOptions<ExtensionMetadataOptions>(); builder.Services.AddSingleton<ExtensionMetadataSanitizer>();
        builder.Services.AddScoped<GeneratedRouteCatalogFilter>();
        byte[] signing = System.Text.Encoding.UTF8.GetBytes(signingText);
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = "catalog-test", ValidateAudience = true,
                ValidAudience = "catalog-test", ValidateLifetime = true, IssuerSigningKey = new SymmetricSecurityKey(signing) };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy, policy => policy.RequireAuthenticatedUser()));
        builder.Services.AddControllers(options => options.Filters.AddService<GeneratedRouteCatalogFilter>())
            .AddApplicationPart(typeof(Controllers.CommandsController).Assembly).AddApplicationPart(typeof(CatalogGeneratedMvcProbeController).Assembly)
            .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new CatalogV3MvcFeature()));
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (CommandAuthorizationException) { context.Response.StatusCode = 403; }
            catch (RouteCatalogUnavailableException) { context.Response.StatusCode = 503; }
        });
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient();
        string Token(string surface) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "catalog-test", Audience = "catalog-test", Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new(new SymmetricSecurityKey(signing), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new("sub", "test-user"), new(RouteCatalogSurfacePolicy.ClaimType, surface)]),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("agent"));
        var command = declarations.Single(d => d.RouteId == "counter-increment-counter");
        using var commandResponse = await client.PostAsJsonAsync("/api/v1/commands", new SubmitCommandRequest("01JAAAAAAAAAAAAAAAAAAAAAAA", "tenant-a", command.Domain,
            "counter-a", command.MessageType!, JsonSerializer.SerializeToElement(new { amount = 1 })));
        commandResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden); commandEffects.Effects.ShouldBe(0); leases.ShouldBe(0);
        tenant.ReceivedCalls().Count().ShouldBe(1); rbac.ReceivedCalls().Count().ShouldBe(1);
        var query = declarations.First(d => d.Kind == "query" && d.Domain == "counter");
        using var queryResponse = await client.PostAsJsonAsync("/api/v1/queries", new SubmitQueryRequest("tenant-a", query.Domain, "counter-a", query.MessageType!));
        queryResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden); leases.ShouldBe(0);
        using var generatedResponse = await client.GetAsync("/catalog-generated-probe/valid");
        generatedResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable); generated.Effects.ShouldBe(0); leases.ShouldBe(0);
        using var metadata = await client.GetAsync("/api/v1/routing/catalog");
        metadata.StatusCode.ShouldBe(HttpStatusCode.OK);
        string content = await metadata.Content.ReadAsStringAsync();
        content.ShouldNotContain("retainedIdempotency"); content.ShouldNotContain("keyMap"); content.ShouldNotContain(removed);
        foreach (var operation in operations.Where(o => o.Ingress == "internal")) content.ShouldNotContain(operation.RouteId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("ui"));
        using var allowed = await client.PostAsJsonAsync("/api/v1/commands", new SubmitCommandRequest("01JAAAAAAAAAAAAAAAAAAAAAAB", "tenant-a", command.Domain,
            "counter-a", command.MessageType!, JsonSerializer.SerializeToElement(new { amount = 1 })));
        allowed.StatusCode.ShouldBe(HttpStatusCode.Accepted); commandEffects.Effects.ShouldBe(1); leases.ShouldBe(0);
    }
}
