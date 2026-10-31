using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Pre-capture substitutions cannot become a fresh trusted MVC or policy baseline.</summary>
public sealed class TechnicalHttpInstalledMetadataTests
{
    [Fact]
    public async Task MinimalDelegateCannotBorrowRealSdkMvcMetadataAndPerformEffects()
    {
        await using var fixture = new TechnicalHttpCatalogFixture(); int effects = 0;
        await fixture.StartAsync(TestContext.Current.CancellationToken, endpoints: app =>
        {
            var actual = ActualPut(app);
            app.MapPut("/api/v1/admin/copied-action", () => { effects++; return Results.Ok(); }).WithMetadata(actual.Metadata.ToArray());
        });
        using var response = await fixture.Client.PutAsJsonAsync("/api/v1/admin/copied-action", new { }, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden); effects.ShouldBe(0); fixture.State.TrySaveByteOperations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("parameter-type")]
    [InlineData("binder")]
    [InlineData("source")]
    [InlineData("model-name")]
    [InlineData("filter-factory")]
    [InlineData("human-policy")]
    [InlineData("control-policy")]
    public async Task CompiledBindingAndInstalledGuardsCannotBeWeakenedBeforeCapture(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture();
        var refusal = await Should.ThrowAsync<InvalidOperationException>(() => fixture.StartAsync(TestContext.Current.CancellationToken,
            services: services =>
            {
                if (scenario == "filter-factory") services.AddScoped(_ => Substitute.For<Hexalith.EventStore.Authorization.AdminTenantAuthorizationFilter>(NullLogger<Hexalith.EventStore.Authorization.AdminTenantAuthorizationFilter>.Instance));
                if (scenario == "human-policy") services.PostConfigure<AuthorizationOptions>(options => options.AddPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy,
                    new AuthorizationPolicyBuilder("Bearer").RequireAuthenticatedUser().Build()));
                if (scenario == "control-policy") services.PostConfigure<AuthorizationOptions>(options => options.AddPolicy(
                    EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, EventStoreOperationIdentifiers.RouteCatalogActivate),
                    new AuthorizationPolicyBuilder(RouteCatalogEndpointExtensions.WorkloadScheme).RequireAuthenticatedUser()
                        .RequireClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreOperationIdentifiers.RouteCatalogActivate).Build()));
            }, endpoints: app =>
            {
                if (scenario is not ("parameter-type" or "binder" or "source" or "model-name")) return;
                var action = ActualPut(app).Metadata.GetMetadata<ControllerActionDescriptor>()!;
                var parameter = action.Parameters.OfType<ControllerParameterDescriptor>().Single(value => value.Name == "request");
                if (scenario == "parameter-type") parameter.ParameterType = typeof(string);
                if (scenario == "binder") parameter.BindingInfo!.BinderType = typeof(SimpleTypeModelBinder);
                if (scenario == "source") parameter.BindingInfo!.BindingSource = BindingSource.Query;
                if (scenario == "model-name") parameter.BindingInfo!.BinderModelName = "changed-installed-name";
            }));
        if (scenario == "human-policy")
        {
            refusal.ShouldBeOfType<InvalidOperationException>();
            refusal.Message.ShouldContain("HTTP endpoint inventory failed");
        }
        else
        {
            refusal.ShouldBeOfType<RouteCatalogUnavailableException>();
            refusal.Message.ShouldContain(scenario == "control-policy" ? "control endpoint" : "SDK technical action");
        }
        fixture.State.TrySaveByteOperations.ShouldBeEmpty();
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed.ShouldBeNull();
    }

    private static RouteEndpoint ActualPut(WebApplication app) => ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
        .Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "api/v1/admin/storage/snapshot-policy"
            && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
}
