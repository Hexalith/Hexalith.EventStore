using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises genuine installed SDK admin MVC actions and control authorization over real HTTP.</summary>
public sealed class TechnicalHttpCatalogReceiverTests
{
    [Theory]
    [InlineData("Hexalith.EventStore.Gateway")]
    [InlineData("Hexalith.EventStore.Admin.Server")]
    public async Task EveryRealRegisteredSdkTechnicalMvcActionQualifiesCompletely(string inventoryAssembly)
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        await fixture.StartAsync(TestContext.Current.CancellationToken, fullInventory: true, inventoryAssembly: inventoryAssembly);
        TimeSpan startup = timer.Elapsed;
        timer.Restart();
        var surface = fixture.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>().Observe();
        TestContext.Current.TestOutputHelper!.WriteLine($"Actual full SDK MVC inventory: owner={inventoryAssembly}; startup/capture={startup.TotalMilliseconds:F0}ms; fresh recapture={timer.Elapsed.TotalMilliseconds:F0}ms; actions={surface.TechnicalHttp!.Count}.");
        var actions = fixture.App.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(TechnicalHttpActionObservation.IsMarked).ToArray();
        actions.ShouldNotBeEmpty();
        surface.TechnicalHttp!.Count.ShouldBe(actions.Length);
        foreach (var action in actions)
            surface.TechnicalHttp.ShouldContain(endpoint => endpoint.ActionIdentity.Contains(action.ControllerTypeInfo.Assembly.FullName!, StringComparison.Ordinal)
                && endpoint.ActionIdentity.Contains(":" + action.MethodInfo.MetadataToken + ":", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ArtificialCombinedHostCannotCommitOverlappingRealActionMembership()
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        var refusal = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.StartAsync(TestContext.Current.CancellationToken, fullInventory: true));
        refusal.Category.ShouldBe(RouteCatalogRefusalReason.Inventory);
        refusal.Message.ShouldContain("Duplicate action:");
    }

    [Theory]
    [InlineData("same-path-two-verbs", true)]
    [InlineData("duplicate-verb", false)]
    [InlineData("mismatched-verb", false)]
    public async Task ActualGetAndPostFamilyHasExactDescriptorVerbJoins(string scenario, bool admitted)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        var surface = fixture.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>().Observe();
        const string path = "/api/v1/admin/tenants/{tenantId}/users";
        surface.TechnicalHttp!.Where(endpoint => endpoint.Pattern == path).SelectMany(endpoint => endpoint.Methods).Order(StringComparer.Ordinal).ShouldBe(["GET", "POST"]);
        if (scenario == "duplicate-verb") surface = surface with { TechnicalHttp = surface.TechnicalHttp.Select(endpoint => endpoint.Pattern == path && endpoint.Methods.Contains("GET")
            ? endpoint with { Methods = ["POST"] } : endpoint).ToArray() };
        if (scenario == "mismatched-verb") surface = surface with { Endpoints = surface.Endpoints.Select(endpoint => endpoint.Pattern == path && endpoint.Methods.Contains("GET")
            ? endpoint with { Methods = ["PUT"] } : endpoint).ToArray() };
        RouteCatalogTechnicalHttpPolicy.AdmitsGrant(surface, "api/v1/admin/tenants", ["GET", "POST"], new Dictionary<string, string>(), participant: false).ShouldBe(admitted);
    }

    [Theory]
    [InlineData("put", 200)]
    [InlineData("admin-post", 202)]
    [InlineData("missing-surface", 200)]
    [InlineData("duplicate-surface", 200)]
    [InlineData("agent", 200)]
    [InlineData("service", 200)]
    [InlineData("anonymous", 401)]
    [InlineData("wrong-channel", 401)]
    [InlineData("mixed-workload", 403)]
    [InlineData("role-denied", 200)]
    [InlineData("admin-role-denied", 403)]
    [InlineData("unmarked", 403)]
    [InlineData("wrong-verb", 403)]
    [InlineData("metadata-changed", 403)]
    [InlineData("policy-changed", 403)]
    [InlineData("schema-changed", 403)]
    [InlineData("filter-changed", 403)]
    [InlineData("health", 200)]
    [InlineData("probe-descendant", 403)]
    [InlineData("probe-post", 403)]
    [InlineData("channel-callback", 200)]
    [InlineData("channel-mixed-human", 401)]
    public async Task RealSdkActionsRetainCredentialPolicyMetadataAndEffects(string scenario, int expected)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        var fence = fixture.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>();
        var surface = fence.Observe();
        surface.FenceVersion.ShouldBe(RouteCatalogTechnicalHttpPolicy.FenceVersion);
        surface.TechnicalHttp!.Any(endpoint => endpoint.Pattern == "/api/v1/admin/storage/snapshot-policy" && endpoint.Methods.SequenceEqual(new[] { "PUT" })).ShouldBeTrue(JsonSerializer.Serialize(surface));
        surface.TechnicalHttp.ShouldContain(endpoint => endpoint.Pattern == "/api/v1/admin/tenants/{tenantId}/users" && endpoint.Policies.Contains("AdminFull"));
        if (scenario is "missing-surface" or "duplicate-surface" or "agent" or "service")
            fixture.Human(scenario == "missing-surface" ? null : scenario is "agent" or "service" ? scenario : "ui", duplicate: scenario == "duplicate-surface");
        if (scenario is "role-denied" or "admin-role-denied") fixture.Human(role: "ReadOnly");
        if (scenario is "anonymous" or "channel-callback") fixture.Client.DefaultRequestHeaders.Authorization = null;
        if (scenario == "wrong-channel")
        {
            fixture.Client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            fixture.Client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, Guid.NewGuid().ToString("N"));
        }
        if (scenario == "mixed-workload") fixture.Client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Guid.NewGuid().ToString("N"));
        if (scenario is "metadata-changed" or "policy-changed" or "filter-changed")
        {
            var selected = ((IEndpointRouteBuilder)fixture.App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
                .Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') == (scenario == "filter-changed" ? "api/v1/admin/streams/commands" : "api/v1/admin/storage/snapshot-policy")
                    && endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IHttpMethodMetadata>()!.HttpMethods.Contains(scenario == "filter-changed" ? "GET" : "PUT"));
            if (scenario == "metadata-changed") selected.Metadata.GetOrderedMetadata<ProducesResponseTypeAttribute>().First().Type = typeof(string);
            if (scenario == "policy-changed") fixture.App.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.AddPolicy("EventStoreHuman",
                new AuthorizationPolicyBuilder("Bearer").RequireAssertion(_ => true).Build());
            if (scenario == "filter-changed") selected.Metadata.GetMetadata<ControllerActionDescriptor>()!.FilterDescriptors.Clear();
        }
        if (scenario == "schema-changed") fixture.App.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions.PropertyNamingPolicy = null;
        using HttpResponseMessage response = scenario is "admin-post" or "admin-role-denied"
            ? await fixture.Client.PostAsJsonAsync("/api/v1/admin/tenants/tenant-a/users", new { userId = "new-user", role = "Reader" }, TestContext.Current.CancellationToken)
            : scenario == "unmarked" ? await fixture.Client.PostAsJsonAsync("/api/v1/admin/unmarked", new { }, TestContext.Current.CancellationToken)
            : scenario == "wrong-verb" ? await fixture.Client.PatchAsJsonAsync("/api/v1/admin/storage/snapshot-policy", new { }, TestContext.Current.CancellationToken)
            : scenario == "health" ? await fixture.Client.GetAsync("/health", TestContext.Current.CancellationToken)
            : scenario == "probe-descendant" ? await fixture.Client.GetAsync("/health/child", TestContext.Current.CancellationToken)
            : scenario == "probe-post" ? await fixture.Client.PostAsJsonAsync("/health", new { }, TestContext.Current.CancellationToken)
            : scenario is "channel-callback" or "channel-mixed-human" ? await fixture.Client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken)
            : scenario == "filter-changed" ? await fixture.Client.GetAsync("/api/v1/admin/streams/commands?tenantId=tenant-a", TestContext.Current.CancellationToken)
            : await fixture.Client.PutAsJsonAsync("/api/v1/admin/storage/snapshot-policy", new SnapshotPolicySetRequest("tenant-a", "orders", "order", 25), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe((HttpStatusCode)expected);
        if (scenario is "put" or "missing-surface" or "duplicate-surface" or "agent" or "service")
        {
            (await response.Content.ReadFromJsonAsync<AdminOperationResult>(TestContext.Current.CancellationToken))!.Success.ShouldBeTrue();
            fixture.State.ByteStoreContains("admin:storage-snapshot-policies:tenant-a").ShouldBeTrue();
            JsonSerializer.Deserialize<List<SnapshotPolicy>>(fixture.State.ByteStoreValue("admin:storage-snapshot-policies:tenant-a"), JsonSerializerOptions.Web)!
                .ShouldHaveSingleItem().IntervalEvents.ShouldBe(25);
        }
        else fixture.State.TrySaveByteOperations.ShouldBeEmpty();
        fixture.TenantCommands.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "AddUserToTenantAsync").ShouldBe(scenario == "admin-post" ? 1 : 0);
    }

    [Theory]
    [InlineData("prepare")]
    [InlineData("ready")]
    [InlineData("commit")]
    [InlineData("rollback")]
    [InlineData("reconcile")]
    public async Task HumanUiProxyCannotMutateAnyCoordinatorControl(string stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        await using var fixture = new TechnicalHttpCatalogFixture(); await fixture.StartAsync(TestContext.Current.CancellationToken, activate: false);
        var first = fixture.Catalog.Candidate(); await fixture.Catalog.ActivateAsync(first);
        var pending = fixture.Catalog.Candidate(2, first.RootDigest); await fixture.Catalog.Authorities[0].PrepareAsync(pending);
        var before = await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken);
        object body = stage switch
        {
            "prepare" => pending,
            "rollback" => new RouteCatalogRollbackRequest(pending.AttemptId, RouteCatalogActivationBinding.ComputeDigest(pending), before.State.Epoch, "pending"),
            "reconcile" => new RouteCatalogQuiescenceRequest("owned-lease", first.RootDigest, before.State.Epoch, "owned-proof"),
            _ => new RouteCatalogLifecycleRequest(pending.AttemptId),
        };
        using var response = await fixture.Client.PostAsJsonAsync("/internal/routing/catalog/" + stage, body, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        var after = await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken);
        after.ETag.ShouldBe(before.ETag);
        JsonSerializer.Serialize(after.State).ShouldBe(JsonSerializer.Serialize(before.State));
    }

    [Fact]
    public async Task RealCoordinatorCanPrepareReadyCommitAndAcquireTheCommittedLease()
    {
        await using var fixture = new TechnicalHttpCatalogFixture(); await fixture.StartAsync(TestContext.Current.CancellationToken, activate: false);
        var candidate = fixture.Catalog.Candidate(); fixture.Coordinator(EventStoreOperationIdentifiers.RouteCatalogActivate);
        using var prepared = await fixture.Client.PostAsJsonAsync("/internal/routing/catalog/prepare", candidate, TestContext.Current.CancellationToken);
        prepared.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        foreach (var peer in fixture.Catalog.Authorities.Skip(1)) await peer.ReadyAsync(candidate.AttemptId);
        using var ready = await fixture.Client.PostAsJsonAsync("/internal/routing/catalog/ready", new RouteCatalogLifecycleRequest(candidate.AttemptId), TestContext.Current.CancellationToken);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var committed = await fixture.Client.PostAsJsonAsync("/internal/routing/catalog/commit", new RouteCatalogLifecycleRequest(candidate.AttemptId), TestContext.Current.CancellationToken);
        committed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!.RootDigest.ShouldBe(candidate.RootDigest);
        await using var lease = await fixture.Catalog.Authorities[0].AcquireAsync(TestContext.Current.CancellationToken);
        lease.Snapshot.RootDigest.ShouldBe(candidate.RootDigest);
    }
}
