using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Caller-only registration, independent observation and actual HTTP transport over the unchanged baseline catalog.</summary>
public sealed class DevelopmentCallerEnrollmentTests
{
    /// <summary>Enrollment owns no executable declarations; actual authority/transport checks refuse scope drift before socket effects.</summary>
    [Theory]
    [InlineData(2, "valid")]
    [InlineData(2, "unenrolled")]
    [InlineData(2, "cross-run")]
    [InlineData(2, "tampered-input")]
    [InlineData(2, "redirect")]
    [InlineData(3, "valid")]
    [InlineData(3, "unenrolled")]
    [InlineData(3, "cross-run")]
    [InlineData(3, "tampered-input")]
    [InlineData(3, "redirect")]
    public async Task ActualCallerRegistrationRetainsAuthorityAndNoRedirectBoundary(int schemaVersion, string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string directory = Directory.CreateTempSubdirectory("catalog-caller-proof-").FullName;
        try
        {
            var fixture = new RouteCatalogTestFixture(new("routing-caller-proof", "caller-proof", "http://127.0.0.1:3500/"));
            var catalog = schemaVersion == 3 ? GeneratedRestProbeDeclarations.Catalog() : fixture.Envelope;
            string[] required = schemaVersion == 3 ? ["domain", "caller-ui"] : [.. fixture.Observations.Select(observation => observation.HostId), "caller-ui"];
            string[] apps = schemaVersion == 3 ? ["domain", "caller-ui"] : [.. fixture.Observations.Select(observation => observation.AppId), "caller-ui"];
            byte[] component = JsonSerializer.SerializeToUtf8Bytes(new
            {
                kind = "Component", metadata = new { name = fixture.Store.Coordinates.StateStore,
                    labels = new Dictionary<string, string> { [RouteCatalogAuthorityComponentPolicy.RoleLabel] = RouteCatalogAuthorityComponentPolicy.IsolatedRole } },
                spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "name" } } }, scopes = apps,
            });
            byte[] acl = JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public", policies = Array.Empty<object>() } } });
            string componentPath = Path.Combine(directory, "loaded-component.json"), aclPath = Path.Combine(directory, "loaded-acl.json");
            await File.WriteAllBytesAsync(componentPath, component, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(aclPath, acl, TestContext.Current.CancellationToken);
            var target = catalog.Routes.First(route => catalog.Declarations.Any(declaration => declaration.RouteId == route.RouteId
                && declaration.Kind == "query" && declaration.Execution!.TargetKind == "domain-service"));
            int effects = 0, redirected = 0;
            var sinkBuilder = WebApplication.CreateBuilder(); sinkBuilder.Logging.ClearProviders(); sinkBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var sink = sinkBuilder.Build();
            sink.MapPost("/" + target.Method, (HttpContext context) =>
            {
                effects++;
                context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString().ShouldBe(RouteCatalogCodec.ComputeRootDigest(catalog));
                context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader].ToString().ShouldBe("1");
                return scenario == "redirect" ? Results.Redirect("/redirected") : Results.NoContent();
            });
            sink.MapPost("/redirected", () => { redirected++; return Results.NoContent(); });
            await sink.StartAsync(TestContext.Current.CancellationToken);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            var section = builder.Configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
            section["Directory"] = directory; section["HostId"] = "caller-ui"; section["AppId"] = "caller-ui";
            section["CoordinatorHostId"] = schemaVersion == 3 ? "domain" : "eventstore"; section["AttemptId"] = "caller-registration";
            section["AuthorityRole"] = RouteCatalogAuthorityComponentPolicy.IsolatedRole;
            section["ComponentPath"] = componentPath; section["AccessControlPath"] = aclPath;
            for (int index = 0; index < required.Length; index++) section["RequiredHostIds:" + index] = required[index];
            builder.Configuration["EventStore:RouteCatalog:StateStore"] = fixture.Store.Coordinates.StateStore;
            builder.Configuration["EventStore:RouteCatalog:StateKey"] = fixture.Store.Coordinates.StateKey;
            builder.Configuration["APP_API_TOKEN"] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var existingHosted = builder.Services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToHashSet();
            builder.Services.AddEventStoreDevelopmentCallerCatalog(builder.Configuration, "caller-ui");
            // This unit driver executes the real lifecycle explicitly against a serialized CAS cell.
            // Background distributed coordination and Redis are qualified by disposable owner CI.
            foreach (var hosted in builder.Services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) && !existingHosted.Contains(descriptor)).ToArray()) builder.Services.Remove(hosted);
            builder.Services.RemoveAll<IRouteCatalogStateStore>(); builder.Services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
            builder.Services.AddHttpClient("caller", client => client.BaseAddress = new Uri(sink.Urls.Single()))
                .AddEventStoreDaprServiceInvocation(target.AppId, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));
            await using var caller = builder.Build();
            caller.UseRouting(); caller.UseEventStoreDevelopmentCallerCatalog(); caller.MapGet("/", () => Results.Ok("caller-ui")); caller.MapDefaultEndpoints();
            await caller.StartAsync(TestContext.Current.CancellationToken);
            var surface = caller.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            surface.Endpoints.Where(endpoint => endpoint.Admitted).ShouldAllBe(endpoint => endpoint.Pattern.StartsWith("/internal/routing/", StringComparison.Ordinal));
            var auxiliary = surface.Endpoints.Where(endpoint => endpoint.Admitted).Select(endpoint => new RouteCatalogAuxiliaryOperation("caller-ui",
                endpoint.Pattern.TrimStart('/'), endpoint.Operation!, "catalog-control")).ToArray();
            RouteCatalogHostObservation[] observations = schemaVersion == 3
                ? [new("domain", "domain", CompiledRouteCatalogDeclarations.Read([typeof(GeneratedRestProbeDeclarations).Assembly]),
                    GeneratedRestProbeDeclarations.Routes.Select(declaration => new RouteCatalogEntry(declaration.RouteId, 1,
                        RouteCatalogCodec.ComputeDeclarationDigest(declaration), "domain", "query", declaration.ContractVersion,
                        declaration.CredentialKind, declaration.Operation, false, "*")).ToArray(), required, [component, acl],
                    JsonSerializer.SerializeToUtf8Bytes(new { ownedSocketFixture = true, executableOwner = typeof(GeneratedRestProbeDeclarations).FullName }),
                    [], [], [], fixture.Store.Coordinates, Operations: CompiledRouteCatalogDeclarations.ReadOperations([typeof(GeneratedRestProbeDeclarations).Assembly]))]
                : fixture.Observations.Select(observation => observation with { RequiredHostIds = required, TopologyDocuments = [component, observation.TopologyDocuments[1]] }).ToArray();
            var observedHosts = observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
            string digest = new('0', 64);
            var provisional = new RouteCatalogActivation("caller-registration", RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), null,
                [.. observedHosts, new("caller-ui", "caller-ui", digest, digest, digest)], "Development-fixture", string.Empty, auxiliary);
            var observer = caller.Services.GetRequiredService<IRouteCatalogHostObserver>();
            var actualCaller = await observer.ObserveAsync(provisional, fixture.Store.Coordinates, TestContext.Current.CancellationToken);
            caller.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = actualCaller;
            var candidate = provisional with { RequiredHosts = [.. observedHosts, actualCaller] };
            candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
            var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
            var authorities = observedHosts.Select(host =>
            {
                var options = Options.Create(new RouteCatalogHostOptions { Identity = host, Profile = "Development", DevelopmentFixture = true });
                return new RouteCatalogAuthority(fixture.Store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
                    new ObservedRouteCatalogHost(_ => Task.FromResult(observations.Single(observation => observation.HostId == host.HostId))));
            }).ToArray();
            await authorities[0].PrepareAsync(candidate, TestContext.Current.CancellationToken);
            foreach (var authority in authorities) await authority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
            var actualAuthority = caller.Services.GetRequiredService<IRouteCatalogAuthority>();
            await actualAuthority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
            await authorities[0].CommitAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
            if (scenario == "unenrolled") caller.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value.Identity = null;
            if (scenario == "cross-run") fixture.Store.Coordinates = fixture.Store.Coordinates with { StateKey = "another-run" };
            if (scenario == "tampered-input")
            {
                var changed = JsonNode.Parse(component)!.AsObject(); changed["metadata"]!["labels"]!["public-observation"] = "changed-after-activation";
                await File.WriteAllBytesAsync(componentPath, JsonSerializer.SerializeToUtf8Bytes(changed), TestContext.Current.CancellationToken);
            }
            using var client = caller.Services.GetRequiredService<IHttpClientFactory>().CreateClient("caller");
            if (scenario is "valid" or "redirect")
            {
                using var response = await client.PostAsync("/" + target.Method, new StringContent("{}"), TestContext.Current.CancellationToken);
                response.StatusCode.ShouldBe(scenario == "redirect" ? HttpStatusCode.Found : HttpStatusCode.NoContent); effects.ShouldBe(1);
            }
            else
            {
                await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.PostAsync("/" + target.Method, new StringContent("{}"), TestContext.Current.CancellationToken));
                effects.ShouldBe(0);
            }
            redirected.ShouldBe(0);
            (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
