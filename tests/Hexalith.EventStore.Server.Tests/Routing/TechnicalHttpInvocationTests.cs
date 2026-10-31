using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Qualifies the real SDK admin Dapr-construction/factory-send sink and independently enforcing receiver over sockets.</summary>
public sealed class TechnicalHttpInvocationTests
{
    [Theory]
    [InlineData("positive", true, 1)]
    [InlineData("inactive", false, 0)]
    [InlineData("cross-run", false, 1)]
    [InlineData("changed-observation", false, 1)]
    [InlineData("changed-caller", false, 0)]
    [InlineData("tampered-membership", false, 1)]
    [InlineData("tampered-peer", false, 1)]
    [InlineData("missing-binding", false, 1)]
    [InlineData("tampered-binding", false, 1)]
    [InlineData("wrong-target", false, 0)]
    [InlineData("wrong-host", false, 0)]
    [InlineData("wrong-port", false, 0)]
    [InlineData("wrong-scheme", false, 0)]
    [InlineData("redirect", false, 1)]
    [InlineData("missing-grants", false, 0)]
    [InlineData("foreign-grant-caller", false, 0)]
    [InlineData("ungranted-verb", false, 0)]
    public async Task ActualAdminServiceFactorySendRetainsWireAndRefusesBeforeEffects(string scenario, bool admitted, int wires)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpInvocationFixture(); await fixture.StartAsync(scenario, TestContext.Current.CancellationToken);
        if (scenario == "changed-observation") fixture.Receiver.App.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions.PropertyNamingPolicy = null;
        if (scenario == "changed-caller")
        {
            int caller = Array.FindIndex(fixture.Caller.Observations, observation => observation.AppId == "eventstore-admin-server");
            fixture.Caller.Observations[caller] = fixture.Caller.Observations[caller] with { AuthenticationProfile = "changed-installed-caller"u8.ToArray() };
        }
        if (scenario is "tampered-membership" or "tampered-peer")
        {
            var cell = await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken);
            var committed = cell.State.Committed!;
            if (scenario == "tampered-membership") committed = committed with { TechnicalHttpReceivers = committed.TechnicalHttpReceivers!.Select(receiver => receiver with
                { Endpoints = receiver.Endpoints.Select(endpoint => endpoint with { RequestDigest = RouteCatalogTestFixture.Hash("changed") }).ToArray() }).ToArray() };
            else committed = committed with { RequiredHosts = committed.RequiredHosts.Select(host => host.AppId == "eventstore" ? host with { TopologyDigest = RouteCatalogTestFixture.Hash("changed-peer") } : host).ToArray() };
            committed = committed with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(committed) };
            (await fixture.Caller.Store.CompareExchangeAsync(cell.State with { Committed = committed }, cell.ETag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        if (scenario is "missing-grants" or "foreign-grant-caller" or "ungranted-verb")
        {
            var cell = await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken);
            var committed = cell.State.Committed! with { TechnicalHttpReceivers = cell.State.Committed!.TechnicalHttpReceivers!.Select(receiver => receiver with
            {
                Invocations = scenario == "missing-grants" ? null : receiver.Invocations!.Select(invocation => invocation with
                { CallerAppId = scenario == "foreign-grant-caller" ? "foreign-peer" : invocation.CallerAppId,
                    HttpMethods = scenario == "ungranted-verb" ? ["GET"] : invocation.HttpMethods }).ToArray(),
            }).ToArray() };
            committed = committed with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(committed) };
            (await fixture.Caller.Store.CompareExchangeAsync(cell.State with { Committed = committed }, cell.ETag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
        var result = await fixture.Service.SetSnapshotPolicyAsync("tenant-a", "orders", "order", 25, TestContext.Current.CancellationToken);
        result.Success.ShouldBe(admitted, result.ErrorCode);
        fixture.WireRequests.ShouldBe(wires);
        fixture.RedirectedRequests.ShouldBe(0);
        fixture.Receiver.State.ByteStoreContains("admin:storage-snapshot-policies:tenant-a").ShouldBe(admitted);
        if (admitted)
        {
            fixture.Verb.ShouldBe("PUT"); fixture.Bearer.ShouldBe(fixture.Receiver.Client.DefaultRequestHeaders.Authorization!.ToString());
            fixture.Body.ShouldBe(JsonSerializer.Serialize(new SnapshotPolicySetRequest("tenant-a", "orders", "order", 25), JsonSerializerOptions.Web));
            var receiver = (await fixture.Receiver.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State;
            fixture.Binding.ShouldBe(RouteCatalogActivationBinding.ComputeDigest(receiver.Committed!));
            JsonSerializer.Deserialize<List<SnapshotPolicy>>(fixture.Receiver.State.ByteStoreValue("admin:storage-snapshot-policies:tenant-a"), JsonSerializerOptions.Web)!
                .ShouldHaveSingleItem().IntervalEvents.ShouldBe(25);
        }
        else if (wires == 0) result.ErrorCode.ShouldBe(nameof(RouteCatalogUnavailableException));
        (await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
        (await fixture.Receiver.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("base-path")]
    [InlineData("wrong-verb")]
    [InlineData("constraint")]
    public async Task ActualFactorySinkRejectsConcealedPathsAndNonmatchingVerbOrConstraint(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpInvocationFixture(); await fixture.StartAsync("positive", TestContext.Current.CancellationToken);
        string path = scenario switch
        {
            "base-path" => "/base/v1.0/invoke/eventstore/method/api/v1/admin/storage/snapshot-policy",
            "constraint" => "/v1.0/invoke/eventstore/method/api/v1/admin/streams/tenant-a/orders/order-1/events/not-a-long",
            _ => "/v1.0/invoke/eventstore/method/api/v1/admin/storage/snapshot-policy",
        };
        using var request = fixture.Request(scenario == "constraint" ? HttpMethod.Get : HttpMethod.Post, path);
        request.Content = JsonContent.Create(new { });
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Client.SendAsync(request, TestContext.Current.CancellationToken));
        fixture.WireRequests.ShouldBe(0); fixture.Receiver.State.TrySaveByteOperations.ShouldBeEmpty();
        (await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
