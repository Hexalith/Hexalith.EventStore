using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks fresh exact auxiliary membership against actual SDK HTTP inventory and nested grant witnesses.</summary>
public sealed class ObservedAuxiliaryMembershipTests
{
    /// <summary>Matching actual inventory passes; missing or nested-field alterations retain the same refusal.</summary>
    [Theory]
    [InlineData("matching")]
    [InlineData("missing")]
    [InlineData("nested-grant")]
    public async Task FreshExactMembershipPreservesNestedSerializedWitnesses(string scenario)
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken, legacyFinite: true,
            invocations: [new("eventstore-admin-server", "/api/v1/admin/storage/snapshot-policy", ["PUT"])]);
        RouteCatalogHostObservation actual = fixture.Catalog.Observations.Single(observation => observation.AppId == "eventstore");
        RouteCatalogActivation candidate = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
        RouteCatalogAuxiliaryOperation required = actual.AuxiliaryOperations!.First(operation => operation.Http?.Grants is { Count: > 0 });
        if (scenario == "missing") actual = actual with
        { AuxiliaryOperations = actual.AuxiliaryOperations!.Where(operation => operation != required).ToArray() };
        if (scenario == "nested-grant") actual = actual with
        {
            AuxiliaryOperations = actual.AuxiliaryOperations!.Select(operation => operation != required ? operation : operation with
            { Http = operation.Http! with { Grants = [operation.Http!.Grants![0] with { Method = "altered/exact/witness" }, .. operation.Http.Grants.Skip(1)] } }).ToArray(),
        };
        var observer = new ObservedRouteCatalogHost(_ => Task.FromResult(actual));

        if (scenario == "matching")
            (await observer.ObserveAsync(candidate, fixture.Catalog.Store.Coordinates, TestContext.Current.CancellationToken))
                .ShouldBe(ObservedRouteCatalogHost.Identity(actual));
        else
            (await Should.ThrowAsync<RouteCatalogUnavailableException>(() => observer.ObserveAsync(candidate,
                fixture.Catalog.Store.Coordinates, TestContext.Current.CancellationToken))).Category.ShouldBe(RouteCatalogRefusalReason.Inventory);
    }
}
