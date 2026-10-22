using Hexalith.EventStore.Contracts.Identity;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class ActorTenantIsolationTests
{
    /// <summary>
    /// Task 4.2: Test storage keys are structurally disjoint per tenant (D1 key pattern).
    /// Verifies that different tenants produce non-overlapping key prefixes,
    /// ensuring no tenant can access another tenant's event stream, metadata, or snapshot.
    /// </summary>
    [Fact]
    public void AggregateIdentity_DifferentTenants_ProduceDisjointKeyPrefixes()
    {
        var identityA = new AggregateIdentity("tenant-a", "counter", "agg-001");
        var identityB = new AggregateIdentity("tenant-b", "counter", "agg-001");

        string eventsA = identityA.EventStreamKeyPrefix;
        string eventsB = identityB.EventStreamKeyPrefix;
        string metaA = identityA.MetadataKey;
        string metaB = identityB.MetadataKey;
        string snapA = identityA.SnapshotKey;
        string snapB = identityB.SnapshotKey;

        eventsA.ShouldNotStartWith(eventsB);
        eventsB.ShouldNotStartWith(eventsA);
        metaA.ShouldNotBe(metaB);
        snapA.ShouldNotBe(snapB);

        eventsA.ShouldBe("tenant-a:counter:agg-001:events:");
        eventsB.ShouldBe("tenant-b:counter:agg-001:events:");
        metaA.ShouldBe("tenant-a:counter:agg-001:metadata");
        metaB.ShouldBe("tenant-b:counter:agg-001:metadata");
        snapA.ShouldBe("tenant-a:counter:agg-001:snapshot");
        snapB.ShouldBe("tenant-b:counter:agg-001:snapshot");

        identityA.ActorId.ShouldNotBe(identityB.ActorId);
        identityA.ActorId.ShouldBe("tenant-a:counter:agg-001");
        identityB.ActorId.ShouldBe("tenant-b:counter:agg-001");
    }
}
