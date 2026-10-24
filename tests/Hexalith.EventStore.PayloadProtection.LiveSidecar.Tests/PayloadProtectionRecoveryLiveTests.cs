// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base PF-02/sections 10 and 13-15; amendment sections 3-7.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Inspects persisted recovery, KEK inventory movement, generation closure and restore refusal on the qualified component.</summary>
public sealed class PayloadProtectionRecoveryLiveTests(PayloadProtectionDaprFixture fixture) : IClassFixture<PayloadProtectionDaprFixture>
{
    [Fact]
    public async Task SharedKekInventoryAccountsForBothEnrolledScopesUntilBothActualKeysAreRewrapped()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        CancellationToken token = timeout.Token;
        var roots = new PayloadProtectionScopeRegistry([new("tenant-shared-a", "qualification"), new("tenant-shared-b", "qualification")]);
        using var backend = new LifecycleQualificationBackend("shared-kek");
        await using var first = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-shared-a", token, backend, roots);
        await using var second = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-shared-b", token, backend, roots);
        var a = await first.Reservations.ProtectEventAsync(Plain(), ["/secret"], Context(first), token);
        var b = await second.Reservations.ProtectEventAsync(Plain(), ["/secret"], Context(second), token);
        await first.Reservations.AcquireCompletionAsync(a.Completion!, token);
        await first.Reservations.CompleteAsync(a.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        await second.Reservations.AcquireCompletionAsync(b.Completion!, token);
        await second.Reservations.CompleteAsync(b.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        var rotationA = new PayloadKeyRotationCoordinator(fixture.Store, roots, first.Leases, backend, new(), _ => true, "qualification-actor");
        var rotationB = new PayloadKeyRotationCoordinator(fixture.Store, roots, second.Leases, backend, new(), _ => true, "qualification-actor");
        (await rotationA.VerifyInventoryAsync(backend.OldVersion, token)).Head!.Value.EntryCount.ShouldBe(2ul);
        backend.CurrentVersion = backend.NewVersion;
        (await rotationA.RewrapAsync(first.Identity, a.Completion!.KeyReference, 1, "01J00000000000000000000508", backend.NewVersion, token)).ShouldBeTrue();
        KekKeyIndexSnapshot retained = await rotationA.VerifyInventoryAsync(backend.OldVersion, token);
        retained.Head!.Value.EntryCount.ShouldBe(1ul);
        retained.Pages.SelectMany(p => p.Value.Entries).Single().ScopeDigest.ShouldBe(second.Scope);
        (await rotationB.RewrapAsync(second.Identity, b.Completion!.KeyReference, 1, "01J00000000000000000000509", backend.NewVersion, token)).ShouldBeTrue();
        (await rotationA.VerifyInventoryAsync(backend.OldVersion, token)).Head!.Value.EntryCount.ShouldBe(0ul);
        (await rotationA.VerifyInventoryAsync(backend.NewVersion, token)).Head!.Value.EntryCount.ShouldBe(2ul);
        await VerifyIndexesAsync(first, await KeyAsync(first, a.Completion, token), token);
        await VerifyIndexesAsync(second, await KeyAsync(second, b.Completion, token), token);
        await AssertReadableAsync(first, a.Result.PayloadBytes, token);
        await AssertReadableAsync(second, b.Result.PayloadBytes, token);
        foreach (byte[] output in backend.Outputs) { output.ShouldAllBe(v => v == 0); }
    }

    [Fact]
    public async Task RestartedWorkerActivatesOnlyTheExactTrustedPersistedReservationAndAtomicallyRemovesItsWork()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-recovery", token);
        var write = await env.Reservations.ProtectEventAsync(Plain(), ["/secret"], Context(env), token);
        await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown, token);
        WrappedDekV1 before = await KeyAsync(env, write.Completion!, token);
        before.State.ShouldBe("Reserved");
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion!.OperationId), token))!.Value.Phase.ShouldBe("Ambiguous");
        using var worker = new PayloadProtectionReconciler(fixture.Store, env.Roots, env.Lifecycle, env.Backend.BackendId, _ => true,
            "qualification-actor", persistence: new LifecycleQualificationEvidence(write.Completion.OperationId));
        (await worker.RunPassAsync(token)).ShouldBe(1);
        WrappedDekV1 active = await KeyAsync(env, write.Completion, token); active.State.ShouldBe("Active");
        active.WrappedDek.ShouldBe(before.WrappedDek);
        await VerifyIndexesAsync(env, active, token);
        (await fixture.Store.ReadAsync<CompletionLeaseV1>(PayloadProtectionStateKeys.Completion(env.Scope, active.KeyRef, 1, active.CreatingOperationId), token)).ShouldBeNull();
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Head.Value.EntryCount.ShouldBe(0u);
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(active.CreatingOperationId), token))!.Value.Status.ShouldBe("Completed");
        (await worker.RunPassAsync(token)).ShouldBe(0);
        env.Backend.WrapCount.ShouldBe(1);
        await AssertReadableAsync(env, write.Result.PayloadBytes, token);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    [Fact]
    public async Task LostRewrapResponseIsRecoveredByStableOperationWithoutRepeatingWrapAndPreservesTheImmutablePayload()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-rotation", token);
        var write = await env.Reservations.ProtectEventAsync(Plain(), ["/secret"], Context(env), token);
        await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        byte[] immutable = write.Result.PayloadBytes.ToArray();
        env.Backend.CurrentVersion = env.Backend.NewVersion; env.Backend.AmbiguousNextWrap = true;
        const string rewrap = "01J00000000000000000000502";
        var rotation = new PayloadKeyRotationCoordinator(fixture.Store, env.Roots, env.Leases, env.Backend, new(), _ => true, "qualification-actor");
        (await rotation.RewrapAsync(env.Identity, write.Completion!.KeyReference, 1, rewrap, env.Backend.CurrentVersion, token)).ShouldBeFalse();
        (await KeyAsync(env, write.Completion, token)).State.ShouldBe("RewrapPending");
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Head.Value.EntryCount.ShouldBe(1u);
        // A new worker reconstructs the stable operation from its durable catalog and increasing claim.
        using var worker = new PayloadProtectionReconciler(fixture.Store, env.Roots, env.Lifecycle, env.Backend.BackendId, _ => true,
            "qualification-actor", rotation: new(fixture.Store, env.Roots, env.Leases, env.Backend, new(), _ => true, "qualification-actor"));
        (await worker.RunPassAsync(token)).ShouldBe(1);
        WrappedDekV1 rotated = await KeyAsync(env, write.Completion, token);
        rotated.State.ShouldBe("Active"); rotated.ProviderKeyVersionId.ShouldBe(env.Backend.CurrentVersion);
        await VerifyIndexesAsync(env, rotated, token);
        (await rotation.VerifyInventoryAsync(env.Backend.OldVersion, token)).Head!.Value.EntryCount.ShouldBe(0ul);
        (await rotation.VerifyInventoryAsync(env.Backend.NewVersion, token)).Head!.Value.EntryCount.ShouldBe(1ul);
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(rewrap), token))!.Value.Phase.ShouldBe("Completed");
        env.Backend.WrapCount.ShouldBe(2); write.Result.PayloadBytes.ShouldBe(immutable);
        await AssertReadableAsync(env, immutable, token);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    [Fact]
    public async Task ClosedGenerationWaitsFullCapabilityExpiryAndRequiresANewServingReaderBeforeMonotonicFenceAdvance()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-generation", token);
        var deployments = new PayloadProtectionDeploymentCoordinator(fixture.Store, env.Roots);
        await deployments.CloseAsync(env.Identity, 1, token);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => env.Leases.RenewAsync(env.Identity, token).AsTask());
        DeploymentGenerationV1 closed = (await fixture.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(env.Root), token))!.Value;
        long started = Stopwatch.GetTimestamp();
        await deployments.OpenSuccessorAsync(env.Identity, closed with { Generation = 2, Status = "Open" }, token);
        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(60));
        (await fixture.Store.ReadAsync<CapabilityLeaseV1>(PayloadProtectionStateKeys.Capability(env.Root, 1, env.Leases.InstanceId), token)).ShouldBeNull();
        FenceV1 original = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        FenceV1 successor = original with { DeploymentGeneration = 2, ApprovalEpoch = 2, ApprovalId = "01J00000000000000000000503" };
        await Should.ThrowAsync<PayloadProtectionStateException>(() => deployments.AdvanceFenceAsync(env.Identity, successor, token).AsTask());
        var newProcess = new PayloadProtectionLeaseCoordinator(fixture.Store, env.Roots, env.Profile);
        await newProcess.RegisterAsync(env.Identity, token);
        await deployments.AdvanceFenceAsync(env.Identity, successor, token);
        FenceV1 current = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        current.ShouldBe(successor); current.HighestWrittenEpoch.ShouldBe(original.HighestWrittenEpoch);
        MemberCatalogSnapshot members = await new PayloadProtectionCatalogs(fixture.Store).ReadMembersAsync(env.Root, token);
        members.Head.Value.EntryCount.ShouldBe(2u);
        members.Pages.SelectMany(p => p.Value.Entries).Select(e => e.Ordinal).ShouldBe([1u, 2u]);
    }

    [Fact]
    public async Task RestoreRequiresTheCompleteCurrentKeySetAndCannotResurrectADeletedLifecycle()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        CancellationToken token = timeout.Token;
        await using var env = await LifecycleQualificationEnvironment.CreateAsync(fixture.Store, "tenant-restore", token);
        var write = await env.Reservations.ProtectEventAsync(Plain(), ["/secret"], Context(env), token);
        await env.Reservations.AcquireCompletionAsync(write.Completion!, token);
        await env.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        WrappedDekV1 backup = await KeyAsync(env, write.Completion!, token);
        FenceV1 fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        PayloadProtectionStateValue<LifecycleV1> lifecycle = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!;
        var decisions = new List<RestoredBackupAdmissionResult>();
        var admission = new PayloadRestoreAdmission(fixture.Store, env.Roots, env.Backend, new(), _ => true,
            (decision, _) => { decisions.Add(decision); return ValueTask.FromResult(decision); });
        var request = new RestoredBackupAdmissionRequest("01J00000000000000000000504", env.Identity.TenantId, env.Identity.Domain, env.Identity.AggregateId,
            null, null, "01J00000000000000000000505", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, KeyReferencePolicy.NoKeyReference, null, null, null, "qualification-actor");
        (await admission.AdmitAsync(request, fence, lifecycle.Value, [backup], token)).State.ShouldBe(RestoredBackupAdmissionState.Accepted);
        (await admission.AdmitAsync(request with { AdmissionId = "01J00000000000000000000506" }, fence, lifecycle.Value, [], token)).State.ShouldBe(RestoredBackupAdmissionState.Quarantined);
        (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(lifecycle.Value with { State = "Deleted", Epoch = 2, ReasonCode = "domain-deleted" }, lifecycle.ETag)], token)).ShouldBe(PayloadStateCommitResult.Committed);
        (await admission.AdmitAsync(request with { AdmissionId = "01J00000000000000000000507" }, fence, lifecycle.Value, [backup], token)).State.ShouldBe(RestoredBackupAdmissionState.Quarantined);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!.Value.State.ShouldBe("Deleted");
        (await KeyAsync(env, write.Completion!, token)).ShouldBe(backup); decisions.Count.ShouldBe(3);
        foreach (byte[] output in env.Backend.Outputs) { output.ShouldAllBe(b => b == 0); }
    }

    private static byte[] Plain() => Encoding.UTF8.GetBytes("{\"secret\":\"pf02-sensitive-canary-8z63\"}");
    private static PayloadProtectionContext Context(LifecycleQualificationEnvironment env) => new(env.Identity, "qualification-event", PayloadProtectionPayloadKind.Event, 1);
    private static async ValueTask<WrappedDekV1> KeyAsync(LifecycleQualificationEnvironment env, PayloadProtectionCompletionContext context, CancellationToken token)
        => (await env.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(env.Scope, context.KeyReference, context.DekVersion), token))!.Value;
    private static async ValueTask VerifyIndexesAsync(LifecycleQualificationEnvironment env, WrappedDekV1 key, CancellationToken token)
    {
        var indexes = new PayloadKeyIndexes(env.Store);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(env.Scope, token),
            await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId), token));
    }
    private static async ValueTask AssertReadableAsync(LifecycleQualificationEnvironment env, byte[] payload, CancellationToken token)
    {
        var resolver = new PayloadKeyResolver(env.Store, env.Roots, new(env.Roots, [new("qualification", new LifecycleQualificationErasure())]), env.Leases, env.Backend, new(), env.Cache);
        CoreUnprotectionResult result = await new PayloadProtectionCore().TryUnprotectEventAsync(payload, Context(env),
            (key, version, ct) => resolver.ResolveAsync(env.Identity, key, version, new CryptographicPayloadProtectionEntropy().CreateKeyReference(), ct), token);
        try { result.PayloadBytes.ShouldBe(Plain()); }
        finally { if (result.PayloadBytes is not null) { CryptographicOperations.ZeroMemory(result.PayloadBytes); } }
        await env.Cache.ClearScopeAsync(env.Scope, token);
    }
}
