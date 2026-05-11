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
    public void BuildSnapshot_DoesNotInventProjection_WhenMetadataMissing() {
        var registration = new DomainServiceRegistration("sample", "process", "tenant-a", "orders", "v1");

        AdminOperationalIndexSnapshot snapshot = AdminOperationalIndexHostedService.BuildSnapshot(
            [],
            [registration],
            new ProjectionOptions());

        snapshot.Projections.ShouldBeEmpty();
        snapshot.TenantProjections.ShouldBeEmpty();
    }
}
