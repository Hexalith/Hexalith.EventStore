using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Physical replicas share a logical route while independently attesting and retaining durable dispatch fences.</summary>
public sealed class RouteCatalogReplicaTests
{
    [Fact]
    public async Task LogicalReplicaRequiresItsOwnReadinessAndRetainsIndependentLeases()
    {
        var fixture = new RouteCatalogTestFixture(replicaAppId: "sample-api");
        var candidate = fixture.Candidate();
        fixture.Hosts.Where(host => host.AppId == "sample-api").Select(host => host.TopologyDigest).Distinct().Count().ShouldBe(2);
        await fixture.Authorities[0].PrepareAsync(candidate);
        foreach (var authority in fixture.Authorities.SkipLast(1)) await authority.ReadyAsync(candidate.AttemptId);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].CommitAsync(candidate.AttemptId));
        await fixture.Authorities[^1].ReadyAsync(candidate.AttemptId);
        (await fixture.Store.ReadAsync(default)).State.Ready.Select(receipt => receipt.Host.HostId).ShouldContain("sample-api-ha");
        await fixture.Authorities[0].CommitAsync(candidate.AttemptId);
        await using var replica = await fixture.Authorities[^1].AcquireAsync();
        var state = (await fixture.Store.ReadAsync(default)).State;
        state.Leases.ShouldHaveSingleItem();
        var successor = fixture.Candidate(2, candidate.RootDigest);
        await fixture.Authorities[0].PrepareAsync(successor);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(successor.AttemptId));
    }

    [Theory]
    [InlineData("code")]
    [InlineData("inventory")]
    [InlineData("policy")]
    [InlineData("missing-code")]
    [InlineData("duplicate-host")]
    public void ConflictingReplicaCannotBindAnActivation(string fault)
    {
        var fixture = new RouteCatalogTestFixture(replicaAppId: "sample-api");
        var candidate = fixture.Candidate();
        var hosts = candidate.RequiredHosts.ToArray();
        hosts[^1] = fault switch
        {
            "code" => hosts[^1] with { ExecutableDigest = RouteCatalogTestFixture.Hash("different executable") },
            "missing-code" => hosts[^1] with { ExecutableDigest = null },
            "inventory" => hosts[^1] with { InventoryDigest = RouteCatalogTestFixture.Hash("different inventory") },
            "policy" => hosts[^1] with { AuthenticationFingerprint = RouteCatalogTestFixture.Hash("different policy") },
            _ => hosts[^1] with { HostId = "sample-api" },
        };
        Should.Throw<RouteCatalogUnavailableException>(() => RouteCatalogActivationBinding.ComputeDigest(candidate with { RequiredHosts = hosts }));
    }
}
