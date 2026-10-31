using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Provides complete credential-free run identity to held ordinary-package readbacks while preserving the legacy wire/constructor.</summary>
public sealed class TechnicalHttpReadbackTests
{
    [Fact]
    public void LegacyConstructorRetainsItsExactSerializedBytes()
    {
        var legacy = new RouteCatalogGenerationReadback(1, 2, "root", "route", "admission", "Development", [], true);
        JsonSerializer.Serialize(legacy).ShouldBe("{\"Epoch\":1,\"Generation\":2,\"RootDigest\":\"root\",\"RouteFacetDigest\":\"route\",\"IdempotencyFacetDigest\":\"admission\",\"Profile\":\"Development\",\"RequiredHosts\":[],\"Serving\":true}");
    }

    [Fact]
    public async Task HeldPeersReadBackTheActualCommonAttemptBindingAndEveryHostDigest()
    {
        await using var fixture = new TechnicalHttpCatalogFixture(compiledSchemaThree: true);
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        await using var held = await fixture.Catalog.Authorities[0].AcquireAsync(TestContext.Current.CancellationToken);
        var committed = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
        foreach (var authority in fixture.Catalog.Authorities)
        {
            var actual = await authority.ReadbackAsync(TestContext.Current.CancellationToken);
            actual.Serving.ShouldBeTrue(); actual.RootDigest.ShouldBe(held.Snapshot.RootDigest); actual.Epoch.ShouldBe(held.Snapshot.Epoch);
            actual.AttemptId.ShouldBe(committed.AttemptId); actual.ActivationBindingDigest.ShouldBe(held.Snapshot.ActivationBindingDigest);
            actual.RequiredHosts.OrderBy(host => host.HostId, StringComparer.Ordinal).ShouldBe(committed.RequiredHosts.OrderBy(host => host.HostId, StringComparer.Ordinal));
        }
        var foreign = committed with { AttemptId = "different-run-same-root-and-epoch" };
        RouteCatalogActivationBinding.ComputeDigest(foreign).ShouldNotBe(held.Snapshot.ActivationBindingDigest);
    }

    [Fact]
    public async Task LegacyAuthorityReadbackOmitsTheNewEvidenceMembers()
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var actual = await fixture.Authorities[0].ReadbackAsync(TestContext.Current.CancellationToken);
        actual.AttemptId.ShouldBeNull(); actual.ActivationBindingDigest.ShouldBeNull();
        using var json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(actual, JsonSerializerOptions.Web));
        json.RootElement.TryGetProperty("attemptId", out _).ShouldBeFalse(); json.RootElement.TryGetProperty("activationBindingDigest", out _).ShouldBeFalse();
    }
}
