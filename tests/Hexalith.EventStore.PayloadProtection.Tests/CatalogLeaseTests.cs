using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Catalog paging, indexed work terminality and serving-generation lease races.</summary>
public sealed class CatalogLeaseTests
{
    private static readonly AggregateIdentity Identity = new("tenant-a", "parties", "party-01");
    private static readonly string Root = PayloadProtectionStateKeys.TenantDomainDigest("tenant-a", "parties");
    private static readonly string Scope = PayloadProtectionStateKeys.ScopeDigest(Identity);
    private const string Operation = "01J00000000000000000000001";
    private const string Instance = "01J00000000000000000000002";
    private const string KeyRef = "01J00000000000000000000003";
    private const string Timestamp = "2026-10-23T00:00:00.0000000Z";
    private static PayloadProtectionInstanceProfile Profile => new(new('b', 40), new('c', 64), "lifecycle-test", ["json", "json+pdenc-v2"], ["V2"]);
    private static DeploymentGenerationV1 Deployment => new("tenant-a", "parties", Root, 1, PayloadProtectionStateKeys.AuthorityDigest,
        Profile.SourceSha, Profile.PackageSetSha256, "Open", Timestamp);

    [Fact]
    public async Task V137_MemberPagingPreservesAllOrdinalsAndTombstones()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var catalogs = new PayloadProtectionCatalogs(store);
        (await catalogs.InitializeAsync(Root)).ShouldBe(PayloadStateCommitResult.Committed);
        for (int i = 0; i < 257; i++)
        {
            MemberCatalogSnapshot members = await catalogs.ReadMembersAsync(Root);
            (await store.CommitAsync(catalogs.AppendMember(members, 1, i.ToString("D26")))).ShouldBe(PayloadStateCommitResult.Committed);
        }
        MemberCatalogSnapshot full = await catalogs.ReadMembersAsync(Root);
        full.Head.Value.EntryCount.ShouldBe(257u);
        full.Pages.Count.ShouldBe(2);
        full.Pages[0].Value.Entries.Length.ShouldBe(256);
        full.Pages[1].Value.Entries.Single().Ordinal.ShouldBe(257u);
        (await store.CommitAsync(catalogs.TransitionMember(full, 0.ToString("D26"), "Draining"))).ShouldBe(PayloadStateCommitResult.Committed);
        full = await catalogs.ReadMembersAsync(Root);
        (await store.CommitAsync(catalogs.TransitionMember(full, 0.ToString("D26"), "Cleared"))).ShouldBe(PayloadStateCommitResult.Committed);
        full = await catalogs.ReadMembersAsync(Root);
        full.Pages[0].Value.Entries[0].State.ShouldBe("Cleared");
        full.Head.Value.EntryCount.ShouldBe(257u);
        Should.Throw<PayloadProtectionStateException>(() => catalogs.AppendMember(full, 2, 0.ToString("D26")));
        Should.Throw<PayloadProtectionStateException>(() => catalogs.TransitionMember(full, 0.ToString("D26"), "Serving"));
    }

    [Fact]
    public async Task MissingCatalogPagesAndMissingNonemptyHeadNeverBecomeEmpty()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var catalogs = new PayloadProtectionCatalogs(store);
        await store.CommitAsync([PayloadProtectionStateMutation.Put(new MemberPageV1(Root, 0, [new(1, 1, Instance, "Serving")]))]);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => catalogs.InitializeAsync(Root).AsTask());
        await Should.ThrowAsync<PayloadProtectionStateException>(() => catalogs.ReadMembersAsync(Root).AsTask());
        await store.CommitAsync([PayloadProtectionStateMutation.Put(new WorkHeadV1(Root, 1, 1, 1, Timestamp))]);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => catalogs.ReadWorkAsync(Root).AsTask());
    }

    [Fact]
    public async Task WorkTerminalRemovalRetainsPagesAndFencesOldClaims()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var catalogs = new PayloadProtectionCatalogs(store);
        await catalogs.InitializeAsync(Root);
        WorkCatalogSnapshot initial = await catalogs.ReadWorkAsync(Root);
        var claim = new ReconciliationV1(Operation, Scope, KeyRef, 1, "Create", Timestamp, 1, "Running", "none", Timestamp);
        await store.CommitAsync([.. catalogs.AddWork(initial, new(Operation, Scope)), PayloadProtectionStateMutation.Put(claim)]);
        WorkCatalogSnapshot pending = await catalogs.ReadWorkAsync(Root);
        pending.Head.Value.EntryCount.ShouldBe(1u);
        Should.Throw<PayloadProtectionStateException>(() => catalogs.AddWork(pending, new(Operation, new('e', 64))));
        PayloadProtectionStateValue<ReconciliationV1> old = (await store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(Operation)))!;
        await store.CommitAsync([PayloadProtectionStateMutation.Put(claim with { Attempt = 2 }, old.ETag)]);
        IReadOnlyList<PayloadProtectionStateMutation> stale = catalogs.CompleteWork(pending, old, claim with { Status = "Quarantined", ReasonCode = "orphan-quarantined" });
        (await store.CommitAsync(stale)).ShouldBe(PayloadStateCommitResult.Conflict);
        (await catalogs.ReadWorkAsync(Root)).Head.Value.EntryCount.ShouldBe(1u);
        PayloadProtectionStateValue<ReconciliationV1> current = (await store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(Operation)))!;
        (await store.CommitAsync(catalogs.CompleteWork(pending, current, current.Value with { Status = "Completed" }))).ShouldBe(PayloadStateCommitResult.Committed);
        WorkCatalogSnapshot completed = await catalogs.ReadWorkAsync(Root);
        completed.Head.Value.EntryCount.ShouldBe(0u);
        completed.Pages.Count.ShouldBe(1);
        completed.Pages[0].Value.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task V121_RegistrationReconcilesLostAcknowledgementAndTrafficExpiresAtFortySeconds()
    {
        var clock = new LifecycleTestClock();
        var store = new MemoryPayloadProtectionStateStore(clock);
        var catalogs = new PayloadProtectionCatalogs(store, clock);
        await catalogs.InitializeAsync(Root);
        await store.CommitAsync([PayloadProtectionStateMutation.Put(Deployment)]);
        PayloadProtectionLeaseCoordinator leases = Coordinator(store, clock);
        store.LoseNextAcknowledgement = true;
        await leases.RegisterAsync(Identity);
        MemberCatalogSnapshot members = await catalogs.ReadMembersAsync(Root);
        members.Pages[0].Value.Entries.Single().InstanceId.ShouldBe(Instance);
        PayloadProtectionStateValue<CapabilityLeaseV1> capability = (await store.ReadAsync<CapabilityLeaseV1>(PayloadProtectionStateKeys.Capability(Root, 1, Instance)))!;
        capability.Value.SourceSha.ShouldBe(Profile.SourceSha);
        clock.Advance(TimeSpan.FromSeconds(40));
        var lifecycle = new LifecycleV1("tenant-a", "parties", "party-01", Scope, "Active", 1, "none", Operation, Timestamp);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.AdmissionAsync(Identity, lifecycle, false).AsTask());
        await leases.RenewAsync(Identity);
        (await leases.AdmissionAsync(Identity, lifecycle, false)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task V120_V121_ClosedGenerationAndDrainingMemberCannotRenew()
    {
        var clock = new LifecycleTestClock();
        var store = new MemoryPayloadProtectionStateStore(clock);
        var catalogs = new PayloadProtectionCatalogs(store, clock);
        await catalogs.InitializeAsync(Root);
        await store.CommitAsync([PayloadProtectionStateMutation.Put(Deployment)]);
        PayloadProtectionLeaseCoordinator leases = Coordinator(store, clock);
        await leases.RegisterAsync(Identity);
        MemberCatalogSnapshot members = await catalogs.ReadMembersAsync(Root);
        store.BeforeCommit = () => store.CommitAsync(catalogs.TransitionMember(members, Instance, "Draining")).GetAwaiter().GetResult();
        await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.RenewAsync(Identity).AsTask());
        (await catalogs.ReadMembersAsync(Root)).Pages[0].Value.Entries.Single().State.ShouldBe("Draining");
        PayloadProtectionStateValue<DeploymentGenerationV1> generation = (await store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(Root)))!;
        await store.CommitAsync([PayloadProtectionStateMutation.Put(generation.Value with { Status = "Closed" }, generation.ETag)]);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.RenewAsync(Identity).AsTask());
    }

    [Fact]
    public async Task V064_ReadLeaseCASSuspensionAndLifecycleBarrierRejectStaleAuthority()
    {
        var clock = new LifecycleTestClock();
        var store = new MemoryPayloadProtectionStateStore(clock);
        var catalogs = new PayloadProtectionCatalogs(store, clock);
        await catalogs.InitializeAsync(Root);
        await store.CommitAsync([PayloadProtectionStateMutation.Put(Deployment), PayloadProtectionStateMutation.Put(new LifecycleV1(
            "tenant-a", "parties", "party-01", Scope, "Active", 1, "none", Operation, Timestamp))]);
        PayloadProtectionLeaseCoordinator leases = Coordinator(store, clock);
        await leases.RegisterAsync(Identity);
        PayloadProtectionStateValue<LifecycleV1> lifecycle = (await store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(Scope)))!;
        await using PayloadProtectionLease read = await leases.AcquireReadAsync(Identity, KeyRef, 1, Operation, lifecycle);
        read.AssertCurrent();
        clock.Advance(TimeSpan.FromSeconds(45));
        Should.Throw<PayloadProtectionStateException>(() => read.AssertCurrent());
        await leases.RenewAsync(Identity);
        lifecycle = (await store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(Scope)))!;
        store.BeforeCommit = () => store.CommitAsync([PayloadProtectionStateMutation.Put(lifecycle.Value with {
            State = "Invalidating", ReasonCode = "domain-invalidating", Epoch = 2 }, lifecycle.ETag)]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.AcquireReadAsync(Identity, KeyRef, 1,
            "01J00000000000000000000004", lifecycle).AsTask());
        (await store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(Scope, "01J00000000000000000000004"))).ShouldBeNull();
    }

    private static PayloadProtectionLeaseCoordinator Coordinator(MemoryPayloadProtectionStateStore store, LifecycleTestClock clock)
        => new(store, new([new("tenant-a", "parties")]), Profile, clock, new SequenceEntropy([Instance]));
}
