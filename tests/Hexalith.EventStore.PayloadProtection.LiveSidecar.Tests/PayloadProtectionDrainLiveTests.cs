// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; PF-02; amendment sections 2-4 and 7.
using System.Diagnostics;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Inspects real persisted barriers, claims, lease expiry, clearing evidence and indexed deletion on PostgreSQL v2/Dapr 1.18.7.</summary>
public sealed class PayloadProtectionDrainLiveTests(PayloadProtectionDaprFixture fixture) : IClassFixture<PayloadProtectionDaprFixture>
{
    [Fact]
    public async Task FullMonotonicDrainWithholdsDeletionWithoutAckAndDeletesAllIndexedStateOnlyAfterExactProof()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        CancellationToken token = deadline.Token;
        await new PayloadProtectionCapabilityProbe(fixture.Store).ProbeAsync(token);
        var identity = new AggregateIdentity("tenant-drain", "qualification", "aggregate-01");
        string root = PayloadProtectionStateKeys.TenantDomainDigest(identity.TenantId, identity.Domain);
        string scope = PayloadProtectionStateKeys.ScopeDigest(identity);
        const string operation = "01J00000000000000000000201";
        const string keyRef = "01J00000000000000000000202";
        const string create = "01J00000000000000000000203";
        string now = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        var roots = new PayloadProtectionScopeRegistry([new(identity.TenantId, identity.Domain)]);
        var profile = new PayloadProtectionInstanceProfile(new('b', 40), new('c', 64), "fixture-backend", ["json", "json+pdenc-v2"], ["V2"]);
        var catalogs = new PayloadProtectionCatalogs(fixture.Store);
        (await catalogs.InitializeAsync(root, token)).ShouldBe(PayloadStateCommitResult.Committed);
        var initial = new LifecycleV1(identity.TenantId, identity.Domain, identity.AggregateId, scope, "Active", 1, "none", create, now);
        var key = PayloadProtectionStateCodec.Seal(new WrappedDekV1(identity.TenantId, identity.Domain, identity.AggregateId, scope,
            keyRef, 1, "Active", "fixture-backend", "fixture:version-01", "RSA-OAEP-256", "AA", 1, 1, 1, create, now, now, new('0', 64), new('0', 64)));
        var indexes = new PayloadKeyIndexes(fixture.Store);
        ScopeKeyIndexSnapshot emptyScope = await indexes.ReadScopeAsync(scope, token);
        string backendDigest = PayloadProtectionStateKeys.Digest(key.BackendId);
        string providerDigest = PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId);
        KekKeyIndexSnapshot emptyInventory = await indexes.ReadKekAsync(backendDigest, providerDigest, token);
        (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(initial), PayloadProtectionStateMutation.Put(key),
            PayloadProtectionStateMutation.Put(new OperationV1(create, "Create", scope, keyRef, 1, "Completed", 1, now, now, "Succeeded", "none")),
            PayloadProtectionStateMutation.Put(new DeploymentGenerationV1(identity.TenantId, identity.Domain, root, 1, PayloadProtectionStateKeys.AuthorityDigest, profile.SourceSha, profile.PackageSetSha256, "Open", now)),
            PayloadProtectionStateMutation.Put(new FenceV1(identity.TenantId, identity.Domain, root, "V2", 1, create, PayloadProtectionStateKeys.AuthorityDigest, 1, "V2", 1, now)),
            .. indexes.SetScopeEntry(emptyScope, new(keyRef, 1, providerDigest, "Active"), true),
            .. indexes.SetInventoryEntry(emptyInventory, new(scope, keyRef, 1), true)], token)).ShouldBe(PayloadStateCommitResult.Committed);
        var leases = new PayloadProtectionLeaseCoordinator(fixture.Store, roots, profile);
        await leases.RegisterAsync(identity, token);
        PayloadProtectionStateValue<LifecycleV1> current = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token))!;
        PayloadProtectionLease read = await leases.AcquireReadAsync(identity, keyRef, 1, "01J00000000000000000000204", current, token);
        await using var cache = new PayloadKeyCache();
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, roots, leases, cache, key.BackendId, _ => true, "qualification-actor");
        var approval = new CryptoShreddingWorkflowDecision(new("01J00000000000000000000205", identity.TenantId, identity.Domain,
            CryptoShreddingWorkflowScope.Aggregate, identity.AggregateId, null, null, KeyReferencePolicy.NoKeyReference, null),
            CryptoShreddingWorkflowState.Approved, "approved", CryptoShreddingNextAction.None, null, null, "qualification-actor", DateTimeOffset.UtcNow, false, false);
        InvalidationDrainV1 drain = await lifecycle.BeginAsync(identity, approval, operation, 2, true, token);
        drain.MemberCutoff.ShouldBe(1u);
        (await fixture.Store.ReadAsync<InvalidationDrainV1>(PayloadProtectionStateKeys.Drain(scope, operation), token))!.Value.ShouldBe(drain);
        var claims = new PayloadReconciliationClaims(fixture.Store);
        ReconciliationV1 first = (await claims.TryClaimAsync(new(operation, scope), token))!.Value;
        (await claims.TryClaimAsync(new(operation, scope), token)).ShouldBeNull();
        long started = Stopwatch.GetTimestamp();
        (await lifecycle.ResumeAsync(identity, first, token)).ShouldBeFalse();
        Stopwatch.GetElapsedTime(started).ShouldBeLessThan(TimeSpan.FromSeconds(10));
        (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(scope, "01J00000000000000000000204"), token)).ShouldNotBeNull();
        read.AssertCurrent(token);
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(key), token))!.Value.State.ShouldBe("Active");
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token))!.Value.State.ShouldBe("Invalidating");
        (await catalogs.ReadWorkAsync(root, token)).Head.Value.EntryCount.ShouldBe(1u);
        (await claims.TryClaimAsync(new(operation, scope), token)).ShouldBeNull();
        DateTimeOffset due = PayloadProtectionStateCodec.ParseTimestamp(first.NotBeforeUtc);
        while (due > DateTimeOffset.UtcNow) { await Task.Delay(due - DateTimeOffset.UtcNow, token); }
        (await fixture.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(scope, "01J00000000000000000000204"), token)).ShouldBeNull();
        Should.Throw<PayloadProtectionStateException>(() => read.AssertCurrent(token));
        await read.DisposeAsync();
        ReconciliationV1 replacement = (await claims.TryClaimAsync(new(operation, scope), token))!.Value;
        replacement.Attempt.ShouldBe(first.Attempt + 1);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => lifecycle.ResumeAsync(identity, first, token).AsTask());
        await lifecycle.AcknowledgeAsync(identity, operation, token);
        CacheClearAckV1 ack = (await fixture.Store.ReadAsync<CacheClearAckV1>(PayloadProtectionStateKeys.ClearAck(scope, 2, leases.InstanceId), token))!.Value;
        ack.OperationId.ShouldBe(operation); ack.LifecycleEpoch.ShouldBe(2ul);
        started = Stopwatch.GetTimestamp();
        (await lifecycle.ResumeAsync(identity, replacement, token)).ShouldBeTrue();
        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(60));
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(key), token)).ShouldBeNull();
        (await indexes.ReadScopeAsync(scope, token)).Head!.Value.EntryCount.ShouldBe(0u);
        (await indexes.ReadKekAsync(backendDigest, providerDigest, token)).Head!.Value.EntryCount.ShouldBe(0ul);
        (await catalogs.ReadWorkAsync(root, token)).Head.Value.EntryCount.ShouldBe(0u);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(scope), token))!.Value.State.ShouldBe("Deleted");
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation), token))!.Value.Phase.ShouldBe("Completed");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation), token))!.Value.Status.ShouldBe("Completed");
    }
}
