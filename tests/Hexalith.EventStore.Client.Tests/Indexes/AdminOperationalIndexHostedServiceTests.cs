using System.Reflection;

using Hexalith.EventStore.Indexes;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Indexes;

public class AdminOperationalIndexHostedServiceTests {
    [Fact]
    public void BuildSnapshot_RejectsProjectionActorKeysAndBuildsCatalogPayloads() {
        var metadata = new AdminOperationalIndexDomainMetadata(
            "counter",
            ["Hexalith.EventStore.Sample.Counter.Events.CounterIncremented"],
            [],
            ["Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter"],
            ["Hexalith.EventStore.Sample.Counter.CounterAggregate"],
            ["counter", "eventstore||ProjectionActor||counter||projection-state"]);
        var wildcardRegistration = new DomainServiceRegistration("sample", "process", "*", "counter", "v1");
        var tenantRegistration = new DomainServiceRegistration("sample", "process", "tenant-a", "orders", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [metadata],
            [wildcardRegistration, tenantRegistration],
            new ProjectionOptions());

        snapshot.Projections.Count.ShouldBe(1);
        snapshot.Projections[0].Name.ShouldBe("counter");
        snapshot.Projections[0].TenantId.ShouldBe("all");
        snapshot.TenantProjections["tenant-a"].ShouldContain(p => p.Name == "counter" && p.TenantId == "tenant-a");
        snapshot.Projections.ShouldNotContain(p => p.Name == "orders");
        snapshot.EventTypes.Single().SchemaVersion.ShouldBe(1);
        snapshot.CommandTypes.Single().TargetAggregateType.ShouldBe("Hexalith.EventStore.Sample.Counter.CounterAggregate");
        snapshot.AggregateTypes.Single().HasProjections.ShouldBeTrue();
    }

    [Fact]
    public void BuildSnapshot_MaterializesHandlerQueryTypesByDomain() {
        var metadata = new AdminOperationalIndexDomainMetadata(
            "tenants",
            ["Hexalith.Tenants.Contracts.Events.TenantCreated"],
            [],
            ["Hexalith.Tenants.Contracts.Commands.CreateTenant"],
            ["Hexalith.Tenants.TenantAggregate"],
            ["tenants"],
            ["list-tenants", "get-tenant"]);
        var registration = new DomainServiceRegistration("tenants", "process", "*", "tenants", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [metadata],
            [registration],
            new ProjectionOptions());

        _ = snapshot.QueryTypesByDomain.ShouldNotBeNull();
        // Materialized de-duplicated and ordered (get-tenant < list-tenants).
        snapshot.QueryTypesByDomain!["tenants"].ShouldBe(["get-tenant", "list-tenants"]);
    }

    [Fact]
    public void BuildSnapshot_MergesQueryTypesWithoutLosingCatalogData() {
        var first = new AdminOperationalIndexDomainMetadata(
            "tenants",
            ["Hexalith.Tenants.Contracts.Events.TenantCreated"],
            [],
            ["Hexalith.Tenants.Contracts.Commands.CreateTenant"],
            ["Hexalith.Tenants.TenantAggregate"],
            ["tenants"],
            ["get-tenant"]);
        var second = new AdminOperationalIndexDomainMetadata(
            "tenants",
            ["Hexalith.Tenants.Contracts.Events.TenantRenamed"],
            [],
            ["Hexalith.Tenants.Contracts.Commands.RenameTenant"],
            ["Hexalith.Tenants.TenantAggregate"],
            ["tenant-search"],
            ["list-tenants", "get-tenant"]);
        var registration = new DomainServiceRegistration("tenants", "process", "*", "tenants", "v1");

        AdminOperationalIndexDomainMetadata merged = MergeDomainMetadata("tenants", [first, second]);

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [merged],
            [registration],
            new ProjectionOptions());

        snapshot.EventTypes.Select(e => e.TypeName).ShouldBe([
            "Hexalith.Tenants.Contracts.Events.TenantCreated",
            "Hexalith.Tenants.Contracts.Events.TenantRenamed",
        ]);
        snapshot.CommandTypes.Select(c => c.TypeName).ShouldBe([
            "Hexalith.Tenants.Contracts.Commands.CreateTenant",
            "Hexalith.Tenants.Contracts.Commands.RenameTenant",
        ]);
        snapshot.AggregateTypes.ShouldContain(a => a.TypeName == "Hexalith.Tenants.TenantAggregate");
        snapshot.Projections.Select(p => p.Name).ShouldBe(["tenant-search", "tenants"]);
        _ = snapshot.QueryTypesByDomain.ShouldNotBeNull();
        snapshot.QueryTypesByDomain!["tenants"].ShouldBe(["get-tenant", "list-tenants"]);
    }

    [Fact]
    public void BuildSnapshot_DoesNotInventProjection_WhenMetadataMissing() {
        var registration = new DomainServiceRegistration("sample", "process", "tenant-a", "orders", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [],
            [registration],
            new ProjectionOptions());

        snapshot.Projections.ShouldBeEmpty();
        snapshot.TenantProjections.ShouldBeEmpty();
    }

    [Fact]
    public void BuildSnapshot_PersistsExactMergedNamedProjectionTypes() {
        var first = new AdminOperationalIndexDomainMetadata(
            "counter",
            [],
            [],
            [],
            ["CounterAggregate"],
            []) {
            NamedProjectionTypes = ["counter-index"],
        };
        var second = new AdminOperationalIndexDomainMetadata(
            "counter",
            [],
            [],
            [],
            ["CounterAggregate"],
            []) {
            NamedProjectionTypes = ["counter-detail", "counter-index"],
        };
        AdminOperationalIndexDomainMetadata merged = MergeDomainMetadata("counter", [first, second]);
        var registration = new DomainServiceRegistration("sample", "process", "tenant-a", "counter", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [merged],
            [registration],
            new ProjectionOptions());

        merged.NamedProjectionTypes.ShouldBe(["counter-detail", "counter-index"]);
        snapshot.Projections.Select(static projection => projection.Name)
            .ShouldBe(["counter-detail", "counter-index"]);
        snapshot.AggregateTypes.ShouldHaveSingleItem().HasProjections.ShouldBeTrue();
    }

    private static AdminOperationalIndexDomainMetadata MergeDomainMetadata(
        string domain,
        IEnumerable<AdminOperationalIndexDomainMetadata> metadata) {
        MethodInfo method = typeof(AdminOperationalIndexHostedService).GetMethod(
            "MergeDomainMetadata",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (AdminOperationalIndexDomainMetadata)method.Invoke(null, [domain, metadata])!;
    }
}
