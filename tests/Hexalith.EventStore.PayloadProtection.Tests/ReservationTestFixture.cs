using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Initializes enrolled, approved in-memory authority for deterministic reservation fault tests only.</summary>
internal sealed class ReservationTestFixture
{
    /// <summary>Gets the authenticated fixture scope.</summary>
    internal AggregateIdentity Identity { get; } = new("tenant-a", "parties", "party-01");
    /// <summary>Gets the monotonic test clock.</summary>
    internal LifecycleTestClock Clock { get; } = new();
    /// <summary>Gets the atomic fault fixture.</summary>
    internal MemoryPayloadProtectionStateStore Store { get; private set; } = null!;
    /// <summary>Gets the explicit backend fixture.</summary>
    internal LifecycleTestBackend Backend { get; } = new();
    /// <summary>Gets the owned erasure adapter.</summary>
    internal LifecycleTestErasureProvider Erasure { get; } = new();
    /// <summary>Gets the registered instance lease authority.</summary>
    internal PayloadProtectionLeaseCoordinator Leases { get; private set; } = null!;
    /// <summary>Gets the reservation coordinator under test.</summary>
    internal KeyReservationCoordinator Reservations { get; private set; } = null!;
    /// <summary>Gets the exact enrolled root.</summary>
    internal string Root => PayloadProtectionStateKeys.TenantDomainDigest(Identity.TenantId, Identity.Domain);
    /// <summary>Gets the exact scope address.</summary>
    internal string Scope => PayloadProtectionStateKeys.ScopeDigest(Identity);

    /// <summary>Creates explicit catalogs, lifecycle, deployment and operator fence before registration.</summary>
    internal static async ValueTask<ReservationTestFixture> CreateAsync(bool allowAudit = true)
    {
        var fixture = new ReservationTestFixture();
        fixture.Store = new(fixture.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var profile = new PayloadProtectionInstanceProfile(new('b', 40), new('c', 64), fixture.Backend.BackendId, ["json", "json+pdenc-v2"], ["V2"]);
        string now = PayloadProtectionStateCodec.Timestamp(fixture.Clock.GetUtcNow());
        await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).InitializeAsync(fixture.Root);
        await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(new DeploymentGenerationV1("tenant-a", "parties", fixture.Root, 1,
            PayloadProtectionStateKeys.AuthorityDigest, profile.SourceSha, profile.PackageSetSha256, "Open", now)),
            PayloadProtectionStateMutation.Put(new LifecycleV1("tenant-a", "parties", "party-01", fixture.Scope, "Active", 1, "none", "01J00000000000000000000001", now)),
            PayloadProtectionStateMutation.Put(new FenceV1("tenant-a", "parties", fixture.Root, "V2", 7, "01J00000000000000000000002",
                PayloadProtectionStateKeys.AuthorityDigest, 1, "Legacy", 0, now))]);
        fixture.Leases = new(fixture.Store, roots, profile, fixture.Clock);
        await fixture.Leases.RegisterAsync(fixture.Identity);
        fixture.Reservations = new(fixture.Store, roots, fixture.Leases, new([new("parties", fixture.Erasure)]), fixture.Backend,
            new(fixture.Clock), _ => allowAudit, "fixture-actor", timeProvider: fixture.Clock);
        return fixture;
    }
}
