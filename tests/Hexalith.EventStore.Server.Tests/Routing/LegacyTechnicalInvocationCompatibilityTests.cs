using System.Net.Http.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Runs actual SDK factory dispatch to a finite legacy receiver without admitting fallback for an opted-in composite.</summary>
public sealed class LegacyTechnicalInvocationCompatibilityTests
{
    [Fact]
    public async Task ActualAdminFactoryReachesOnlyTheCommittedLegacyActionAndReleasesBothLeases()
    {
        await using var fixture = new TechnicalHttpInvocationFixture();
        await fixture.StartAsync("positive", TestContext.Current.CancellationToken, legacyFinite: true);
        var result = await fixture.Service.SetSnapshotPolicyAsync("tenant-a", "orders", "order", 25, TestContext.Current.CancellationToken);
        result.Success.ShouldBeTrue(result.ErrorCode);
        fixture.WireRequests.ShouldBe(1);
        fixture.Receiver.State.ByteStoreContains("admin:storage-snapshot-policies:tenant-a").ShouldBeTrue();
        fixture.Binding.ShouldBeNullOrEmpty();
        (await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
        (await fixture.Receiver.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("foreign-target")]
    [InlineData("foreign-path")]
    [InlineData("wrong-verb")]
    public async Task LegacyWitnessRefusesUncommittedTargetPathOrVerbBeforeAnyWire(string scenario)
    {
        await using var fixture = new TechnicalHttpInvocationFixture();
        await fixture.StartAsync("positive", TestContext.Current.CancellationToken, legacyFinite: true);
        string target = scenario == "foreign-target" ? "foreign-peer" : "eventstore";
        string path = scenario == "foreign-path" ? "api/v1/admin/storage/snapshot-policy/extra" : "api/v1/admin/storage/snapshot-policy";
        using var request = fixture.Request(scenario == "wrong-verb" ? HttpMethod.Post : HttpMethod.Put,
            "/v1.0/invoke/" + target + "/method/" + path);
        request.Content = JsonContent.Create(new SnapshotPolicySetRequest("tenant-a", "orders", "order", 25));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Client.SendAsync(request, TestContext.Current.CancellationToken));
        fixture.WireRequests.ShouldBe(0);
        fixture.Receiver.State.TrySaveByteOperations.ShouldBeEmpty();
        (await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompositeMembershipCannotFallBackThroughAnActualLegacyWitness()
    {
        await using var legacy = new TechnicalHttpCatalogFixture();
        await legacy.StartAsync(TestContext.Current.CancellationToken, legacyFinite: true,
            invocations: [new("eventstore-admin-server", "/api/v1/admin/storage/snapshot-policy", ["PUT"])]);
        await using var fixture = new TechnicalHttpInvocationFixture();
        await fixture.StartAsync("positive", TestContext.Current.CancellationToken);
        var cell = await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken);
        var committed = cell.State.Committed! with
        {
            AuxiliaryOperations = legacy.Catalog.Observations[0].AuxiliaryOperations,
            TechnicalHttpReceivers = cell.State.Committed!.TechnicalHttpReceivers!.Select(receiver => receiver with
            {
                Endpoints = receiver.Endpoints.Where(endpoint => endpoint.Pattern != "/api/v1/admin/storage/snapshot-policy").ToArray(),
            }).ToArray(),
        };
        committed = committed with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(committed) };
        (await fixture.Caller.Store.CompareExchangeAsync(cell.State with { Committed = committed }, cell.ETag, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var result = await fixture.Service.SetSnapshotPolicyAsync("tenant-a", "orders", "order", 25, TestContext.Current.CancellationToken);
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe(nameof(RouteCatalogUnavailableException));
        fixture.WireRequests.ShouldBe(0);
        fixture.Receiver.State.TrySaveByteOperations.ShouldBeEmpty();
        (await fixture.Caller.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
