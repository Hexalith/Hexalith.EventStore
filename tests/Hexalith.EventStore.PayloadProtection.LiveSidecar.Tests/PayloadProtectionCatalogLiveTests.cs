// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base PF-02/section 10; amendment sections 2-4 and 7.
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Qualifies catalog, serving-authority and indexed-key transactions through the isolated real sidecar.</summary>
public sealed class PayloadProtectionCatalogLiveTests(PayloadProtectionDaprFixture fixture) : IClassFixture<PayloadProtectionDaprFixture>
{
    [Fact]
    public async Task RealPostgresqlCatalogLeaseAndKeyIndexTransactionsHavePersistedAtomicEndStates()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        CancellationToken token = deadline.Token;
        await new PayloadProtectionCapabilityProbe(fixture.Store).ProbeAsync(token);
        var identity = new AggregateIdentity("tenant-pf02", "catalogs", "aggregate-01");
        string root = PayloadProtectionStateKeys.TenantDomainDigest(identity.TenantId, identity.Domain);
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        string timestamp = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        var profile = new PayloadProtectionInstanceProfile(new('b', 40), new('c', 64), "fixture-backend", ["json", "json+pdenc-v2"], ["V2"]);
        var deployment = new DeploymentGenerationV1(identity.TenantId, identity.Domain, root, 1, PayloadProtectionStateKeys.AuthorityDigest,
            profile.SourceSha, profile.PackageSetSha256, "Open", timestamp);
        var lifecycle = new LifecycleV1(identity.TenantId, identity.Domain, identity.AggregateId, scope, "Active", 1, "none",
            "01J00000000000000000000001", timestamp);
        var catalogs = new PayloadProtectionCatalogs(fixture.Store);
        (await catalogs.InitializeAsync(root, token)).ShouldBe(PayloadStateCommitResult.Committed);
        (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(deployment), PayloadProtectionStateMutation.Put(lifecycle)], token)).ShouldBe(PayloadStateCommitResult.Committed);
        var leases = new PayloadProtectionLeaseCoordinator(fixture.Store, new([new(identity.TenantId, identity.Domain)]), profile);
        await leases.RegisterAsync(identity, token);
        await leases.RenewAsync(identity, token);
        MemberCatalogSnapshot registered = await catalogs.ReadMembersAsync(root, token);
        registered.Head.Value.EntryCount.ShouldBe(1u);
        registered.Pages[0].Value.Entries.Single().State.ShouldBe("Serving");
        PayloadProtectionStateValue<LifecycleV1> observedLifecycle = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token))!;
        string keyRef = "01J00000000000000000000002";
        string operation = "01J00000000000000000000003";
        await using (PayloadProtectionLease read = await leases.AcquireReadAsync(identity, keyRef, 1, operation, observedLifecycle, token))
        {
            (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(scope, operation), token))!.Value.LifecycleEpoch.ShouldBe(1ul);
            read.AssertCurrent(token);
        }
        (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(scope, operation), token)).ShouldBeNull();
        observedLifecycle = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token))!;

        var indexes = new PayloadKeyIndexes(fixture.Store);
        string backend = PayloadProtectionStateKeys.Digest("fixture-backend");
        string provider = PayloadProtectionStateKeys.Digest("fixture:version-01");
        ScopeKeyIndexSnapshot scopeIndex = await indexes.ReadScopeAsync(scope, token);
        KekKeyIndexSnapshot inventory = await indexes.ReadKekAsync(backend, provider, token);
        WorkCatalogSnapshot work = await catalogs.ReadWorkAsync(root, token);
        WrappedDekV1 key = PayloadProtectionStateCodec.Seal(new(identity.TenantId, identity.Domain, identity.AggregateId, scope,
            keyRef, 1, "Reserved", "fixture-backend", "fixture:version-01", "RSA-OAEP-256", "AA", 1, 1, 1,
            operation, timestamp, timestamp, new('0', 64), new('0', 64)));
        var intent = new OperationV1(operation, "Create", scope, keyRef, 1, "Reserved", 1, timestamp, timestamp, "Pending", "none");
        var reconciliation = new ReconciliationV1(operation, scope, keyRef, 1, "Create", timestamp, 1, "Pending", "none", timestamp);
        List<PayloadProtectionStateMutation> transaction = [PayloadProtectionStateMutation.Put(key),
            .. indexes.SetScopeEntry(scopeIndex, new(keyRef, 1, provider, "Reserved"), true),
            .. indexes.SetInventoryEntry(inventory, new(scope, keyRef, 1), true),
            .. catalogs.AddWork(work, new(operation, scope)), PayloadProtectionStateMutation.Put(intent),
            PayloadProtectionStateMutation.Put(reconciliation), PayloadProtectionStateMutation.Compare(observedLifecycle),
            PayloadProtectionStateMutation.Compare(registered.Head)];
        // A changed authority ETag must roll back the complete key/index/work transaction.
        (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Compare(observedLifecycle)], token)).ShouldBe(PayloadStateCommitResult.Committed);
        (await fixture.Store.CommitAsync(transaction, token)).ShouldNotBe(PayloadStateCommitResult.Committed);
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(key), token)).ShouldBeNull();
        (await indexes.ReadScopeAsync(scope, token)).Head.ShouldBeNull();
        (await indexes.ReadKekAsync(backend, provider, token)).Head.ShouldBeNull();
        (await catalogs.ReadWorkAsync(root, token)).Head.Value.EntryCount.ShouldBe(0u);
        observedLifecycle = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token))!;
        transaction[^2] = PayloadProtectionStateMutation.Compare(observedLifecycle);
        (await fixture.Store.CommitAsync(transaction, token)).ShouldBe(PayloadStateCommitResult.Committed);
        WrappedDekV1 persisted = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(key), token))!.Value;
        PayloadKeyIndexes.VerifyMembership(persisted, await indexes.ReadScopeAsync(scope, token), await indexes.ReadKekAsync(backend, provider, token));
        (await catalogs.ReadWorkAsync(root, token)).Head.Value.EntryCount.ShouldBe(1u);
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation), token))!.Value.Phase.ShouldBe("Reserved");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation), token))!.Value.Status.ShouldBe("Pending");

        PayloadProtectionStateValue<DeploymentGenerationV1> current = (await fixture.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(root), token))!;
        (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { Status = "Closed" }, current.ETag)], token)).ShouldBe(PayloadStateCommitResult.Committed);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => leases.RenewAsync(identity, token).AsTask());
        (await fixture.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(root), token))!.Value.Status.ShouldBe("Closed");
    }
}
