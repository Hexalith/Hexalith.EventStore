using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Server.Controllers;
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
    /// <summary>Accepts equivalent runtime wrappers while preserving body execution and persisted state.</summary>
    [Fact]
    public async Task DistinctRuntimeParameterWithExactMetadataQualifiesBeforeCapture()
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken, endpoints: app =>
        {
            var action = ActualPut(app).Metadata.GetMetadata<ControllerActionDescriptor>()!;
            var parameter = action.Parameters.OfType<ControllerParameterDescriptor>().Single(value => value.Name == "request");
            var original = parameter.ParameterInfo;
            var clone = CloneParameter(original);
            clone.ShouldNotBeSameAs(original);
            clone.GetType().ShouldBe(original.GetType());
            clone.Member.ShouldBe(original.Member);
            clone.MetadataToken.ShouldBe(original.MetadataToken);
            clone.Position.ShouldBe(original.Position);
            clone.Name.ShouldBe(original.Name);
            clone.ParameterType.ShouldBe(original.ParameterType);
            clone.Attributes.ShouldBe(original.Attributes);
            parameter.ParameterInfo = clone;
        });
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed.ShouldNotBeNull();
        using var response = await fixture.Client.PutAsJsonAsync("/api/v1/admin/storage/snapshot-policy",
            new SnapshotPolicySetRequest("tenant-a", "orders", "order", 25), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AdminOperationResult>(TestContext.Current.CancellationToken))!.Success.ShouldBeTrue();
        JsonSerializer.Deserialize<List<SnapshotPolicy>>(fixture.State.ByteStoreValue("admin:storage-snapshot-policies:tenant-a"), JsonSerializerOptions.Web)!
            .ShouldHaveSingleItem().IntervalEvents.ShouldBe(25);
    }

    /// <summary>Accepts equivalent query and optional cancellation wrappers without changing backup state.</summary>
    [Fact]
    public async Task DistinctRuntimeQueryAndOptionalParametersQualifyForTheOriginalBackupAction()
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        BackupJob job = new("backup-one", "tenant-a", null, "clone acceptance", BackupJobType.Backup, BackupJobStatus.Completed,
            true, DateTimeOffset.UtcNow, null, 25, 100, true, null);
        await fixture.State.SaveStateAsync<IReadOnlyList<BackupJob>>("statestore", "admin:backup-jobs:tenant-a", [job], stateOptions: null!, metadata: null!, TestContext.Current.CancellationToken);
        byte[] before = fixture.State.ByteStoreValue("admin:backup-jobs:tenant-a");
        await fixture.StartAsync(TestContext.Current.CancellationToken, fullInventory: true, inventoryAssembly: "Hexalith.EventStore.Admin.Server", endpoints: app =>
        {
            var action = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
                .Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "api/v1/admin/backups"
                    && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"))
                .Metadata.GetMetadata<ControllerActionDescriptor>()!;
            action.MethodInfo.DeclaringType.ShouldBe(typeof(AdminBackupsController));
            action.MethodInfo.Name.ShouldBe(nameof(AdminBackupsController.GetBackupJobs));
            action.Parameters.Count.ShouldBe(2);
            foreach (var parameter in action.Parameters.OfType<ControllerParameterDescriptor>())
            {
                var original = parameter.ParameterInfo;
                var clone = CloneParameter(original);
                clone.ShouldNotBeSameAs(original);
                clone.GetType().ShouldBe(original.GetType());
                clone.Member.ShouldBe(original.Member); clone.MetadataToken.ShouldBe(original.MetadataToken);
                clone.Position.ShouldBe(original.Position); clone.Name.ShouldBe(original.Name); clone.ParameterType.ShouldBe(original.ParameterType);
                clone.Attributes.ShouldBe(original.Attributes);
                clone.IsOptional.ShouldBe(parameter.Name == "ct");
                parameter.BindingInfo!.BindingSource!.Id.ShouldBe(parameter.Name == "ct" ? "Special" : "Query");
                parameter.ParameterInfo = clone;
            }
        });
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed.ShouldNotBeNull();
        using var response = await fixture.Client.GetAsync("/api/v1/admin/backups?tenantId=tenant-a", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<BackupJob>>(TestContext.Current.CancellationToken))!.ShouldHaveSingleItem().ShouldBe(job);
        fixture.State.TrySaveByteOperations.ShouldBeEmpty();
        fixture.State.ByteStoreValue("admin:backup-jobs:tenant-a").ShouldBe(before);
    }

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
    [InlineData("parameter-member")]
    [InlineData("parameter-position")]
    [InlineData("parameter-name")]
    [InlineData("parameter-metadata-type")]
    [InlineData("parameter-token")]
    [InlineData("parameter-wrapper")]
    [InlineData("parameter-wrapper-source")]
    [InlineData("parameter-optional-required-body")]
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
                if (!scenario.StartsWith("parameter-", StringComparison.Ordinal) && scenario is not ("binder" or "source" or "model-name")) return;
                var endpoint = scenario == "parameter-optional-required-body"
                    ? ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
                        .Single(value => value.RoutePattern.RawText?.Trim('/') == "api/v1/admin/tenants/{tenantId}/users"
                            && value.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("POST"))
                    : ActualPut(app);
                var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;
                var parameter = action.Parameters.OfType<ControllerParameterDescriptor>().Single(value => value.Name == "request");
                if (scenario == "parameter-optional-required-body")
                {
                    parameter.ParameterInfo.IsOptional.ShouldBeFalse();
                    new NullabilityInfoContext().Create(parameter.ParameterInfo).ReadState.ShouldBe(NullabilityState.NotNull);
                    parameter.BindingInfo!.EmptyBodyBehavior.ShouldBe(Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Default);
                    parameter.ParameterInfo = CloneParameter(parameter.ParameterInfo);
                    ParameterField(parameter.ParameterInfo, "AttrsImpl").SetValue(parameter.ParameterInfo, parameter.ParameterInfo.Attributes | ParameterAttributes.Optional);
                    parameter.ParameterInfo.IsOptional.ShouldBeTrue();
                    parameter.BindingInfo.EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow;
                }
                if (scenario == "parameter-member")
                {
                    parameter = action.Parameters.OfType<ControllerParameterDescriptor>().Single(value => value.Name == "ct");
                    parameter.ParameterInfo = action.MethodInfo.DeclaringType!.GetMethod("DeleteSnapshotPolicy")!.GetParameters().Single(value => value.Name == "ct");
                }
                if (scenario is "parameter-position" or "parameter-name" or "parameter-metadata-type" or "parameter-token")
                {
                    parameter.ParameterInfo = CloneParameter(parameter.ParameterInfo);
                    if (scenario == "parameter-position") ParameterField(parameter.ParameterInfo, "PositionImpl").SetValue(parameter.ParameterInfo, parameter.ParameterInfo.Position + 1);
                    if (scenario == "parameter-name") ParameterField(parameter.ParameterInfo, "NameImpl").SetValue(parameter.ParameterInfo, "changed-parameter-name");
                    if (scenario == "parameter-metadata-type")
                    {
                        ParameterField(parameter.ParameterInfo, "ClassImpl").SetValue(parameter.ParameterInfo, typeof(string));
                        parameter.ParameterType = typeof(string);
                    }
                    if (scenario == "parameter-token") ParameterField(parameter.ParameterInfo, "m_tkParamDef").SetValue(parameter.ParameterInfo, parameter.ParameterInfo.MetadataToken + 1);
                }
                if (scenario is "parameter-wrapper" or "parameter-wrapper-source")
                {
                    var original = parameter.ParameterInfo;
                    var wrapper = new TechnicalHttpParameterInfoWrapper(original, scenario == "parameter-wrapper-source");
                    wrapper.Member.ShouldBe(original.Member); wrapper.MetadataToken.ShouldBe(original.MetadataToken);
                    wrapper.Position.ShouldBe(original.Position); wrapper.Name.ShouldBe(original.Name); wrapper.ParameterType.ShouldBe(original.ParameterType);
                    parameter.ParameterInfo = wrapper;
                    if (scenario == "parameter-wrapper-source")
                    {
                        parameter.BindingInfo!.BindingSource = BindingSource.Query;
                        parameter.BindingInfo.EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Default;
                    }
                }
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

    private static ParameterInfo CloneParameter(ParameterInfo parameter) => (ParameterInfo)typeof(object)
        .GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(parameter, null)!;

    private static FieldInfo ParameterField(ParameterInfo parameter, string name) => parameter.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();

    private static RouteEndpoint ActualPut(WebApplication app) => ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
        .Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == "api/v1/admin/storage/snapshot-policy"
            && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
}
