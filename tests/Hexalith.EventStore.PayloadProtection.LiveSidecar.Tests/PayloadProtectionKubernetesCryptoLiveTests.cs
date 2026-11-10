// Approved Dapr Kubernetes cryptography supplement: 14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881; sections 2-8.
// Profile authority: 29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44.
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Owned kind + daprd 1.19.1 qualification of the stock crypto.dapr.kubernetes.secrets component, asserted through the Dapr state boundary.</summary>
/// <remarks>Explicit tests run only in the opt-in Kubernetes cryptography lane (<c>--filter-trait Lane=KubernetesCrypto --explicit only</c>).
/// The generated gRPC transport surfaces each call's status: a missing or wrong API token is Denied, a stopped sidecar is Unavailable, and
/// daprd 1.19.1 reports a Kubernetes 403 (denied ServiceAccount or ungranted Secret), a missing Secret and a tampered envelope as Internal,
/// which amendment section 7 maps to ConsistencyMismatch.</remarks>
[Trait("Lane", "KubernetesCrypto")]
public sealed class PayloadProtectionKubernetesCryptoLiveTests(KubernetesCryptoFixture fixture) : IClassFixture<KubernetesCryptoFixture>
{
    [Fact(Explicit = true)]
    public async Task WrapAndUnwrapPersistWrappedDekV2AndItsIndexedAuthorityThroughTheDaprStateBoundary()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        fixture.ApiServer.ShouldBe(fixture.OwnedNodeEndpoint);
        File.Exists(fixture.RestrictedKubeconfig).ShouldBeTrue();
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-wrap", token);
        var backend = Backend(fixture.Wrap);
        var write = await env.WriteAsync(backend, token);
        PayloadProtectionStateValue<WrappedDekV1> stored = await env.KeyAsync(write.Completion, token);
        WrappedDekV2 key = stored.Value.ShouldBeOfType<WrappedDekV2>();
        Encoding.UTF8.GetString(stored.Bytes.Span).ShouldStartWith("{\"schemaVersion\":2,");
        key.State.ShouldBe("Active"); key.ProviderKeyVersionId.ShouldBe(fixture.Wrap);
        key.BackendId.ShouldBe("dapr-crypto-kubernetes-secrets-v1"); key.WrapAlgorithmId.ShouldBe("dapr-enc-v1-rsa-oaep-256-aes-gcm");
        byte[] envelope = Base64UrlCodec.Decode(key.WrappedDek);
        envelope.AsSpan().StartsWith(WrappedDekV2.SchemePrefix).ShouldBeTrue(); envelope.Length.ShouldBeLessThanOrEqualTo(2048);
        await env.VerifyIndexesAsync(key, token);
        (await env.InventoryAsync(fixture.Wrap, token)).ShouldBe(1ul);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!.Value.State.ShouldBe("Active");
        FenceV1 fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        fence.ApprovedSpecDigest.ShouldBe(PayloadProtectionAuthorityProfile.DaprKubernetesCryptoDigest); fence.HighestWrittenFormat.ShouldBe("V2");
        (await fixture.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(env.Root), token))!.Value.ApprovedSpecDigest
            .ShouldBe(PayloadProtectionAuthorityProfile.DaprKubernetesCryptoDigest);
        (await fixture.Store.ReadAsync<CapabilityLeaseV1>(PayloadProtectionStateKeys.Capability(env.Root, 1, env.Leases.InstanceId), token))!.Value.ReadFormats
            .ShouldBe(["json", "json+pdenc-v2", "wrapped-dek-v2"]);
        OperationV1 operation = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(key.CreatingOperationId), token))!.Value;
        operation.Phase.ShouldBe("Completed"); operation.Result.ShouldBe("Succeeded");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(key.CreatingOperationId), token))!.Value.Status.ShouldBe("Completed");
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Head.Value.EntryCount.ShouldBe(0u);
        await env.AssertReadableAsync(backend, write.Payload, token);
    }

    [Fact(Explicit = true)]
    public async Task RestartedSidecarAndFreshProcessIncarnationReadTheHistoricalWrappedKey()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-restart", token);
        var write = await env.WriteAsync(Backend(fixture.V1), token);
        WrappedDekV1 before = (await env.KeyAsync(write.Completion, token)).Value;
        await fixture.StopSidecarAsync();
        await fixture.StartSidecarAsync(token);
        await new PayloadProtectionCapabilityProbe(fixture.Store).ProbeAsync(token);
        PayloadProtectionLeaseCoordinator restarted = await env.RegisterAsync(token, fixture.Store);
        restarted.InstanceId.ShouldNotBe(env.Leases.InstanceId);
        (await env.KeyAsync(write.Completion, token, fixture.Store)).Value.ShouldBe(before);
        await env.AssertReadableAsync(Backend(fixture.V1), write.Payload, token, process: restarted, store: fixture.Store);
    }

    [Fact(Explicit = true)]
    public async Task RotationRetainsHistoricalReadsAndRewrapsV2ToV2WithTheSameDek()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-rotation", token);
        var first = await env.WriteAsync(Backend(fixture.RotationV1), token);
        WrappedDekV1 original = (await env.KeyAsync(first.Completion, token)).Value;
        // Rollout phase 1 admits the new address everywhere; phase 2 makes it current and retains the old one.
        await env.AssertReadableAsync(Backend(fixture.RotationV1, fixture.RotationV2), first.Payload, token);
        var current = Backend(fixture.RotationV2, fixture.RotationV1);
        var second = await env.WriteAsync(current, token, sequence: 2);
        (await env.KeyAsync(second.Completion, token)).Value.ProviderKeyVersionId.ShouldBe(fixture.RotationV2);
        await env.AssertReadableAsync(current, first.Payload, token);
        var rotation = new PayloadKeyRotationCoordinator(fixture.Store, env.Roots, env.Leases, current, new(), _ => true, "qualification-actor");
        const string rewrap = "01J00000000000000000000903";
        (await rotation.RewrapAsync(env.Identity, first.Completion.KeyReference, 1, rewrap, fixture.RotationV2, token)).ShouldBeTrue();
        WrappedDekV1 rotated = (await env.KeyAsync(first.Completion, token)).Value;
        rotated.ShouldBeOfType<WrappedDekV2>(); rotated.State.ShouldBe("Active"); rotated.ProviderKeyVersionId.ShouldBe(fixture.RotationV2);
        rotated.WrappedDek.ShouldNotBe(original.WrappedDek); rotated.CreatingOperationId.ShouldBe(original.CreatingOperationId);
        await env.VerifyIndexesAsync(rotated, token);
        (await env.InventoryAsync(fixture.RotationV1, token)).ShouldBe(0ul);
        (await env.InventoryAsync(fixture.RotationV2, token)).ShouldBe(2ul);
        OperationV1 operation = (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(rewrap), token))!.Value;
        operation.Phase.ShouldBe("Completed"); operation.Result.ShouldBe("Succeeded");
        using (PayloadBackendResult before = await current.UnwrapAsync(rewrap, fixture.RotationV1, Base64UrlCodec.Decode(original.WrappedDek), token))
        using (PayloadBackendResult after = await current.UnwrapAsync(rewrap, fixture.RotationV2, Base64UrlCodec.Decode(rotated.WrappedDek), token))
        {
            byte[] a = before.TakeBytes(); byte[] b = after.TakeBytes();
            try { CryptographicOperations.FixedTimeEquals(a, b).ShouldBeTrue(); }
            finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
        }
        await env.AssertReadableAsync(Backend(fixture.RotationV2), first.Payload, token);
        await env.AssertReadableAsync(Backend(fixture.RotationV2), second.Payload, token, sequence: 2);
    }

    [Fact(Explicit = true)]
    public async Task InvalidationDeletionAndRestoreLeaveTheExactPersistedEndState()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-erasure", token);
        var backend = Backend(fixture.Erasure);
        var write = await env.WriteAsync(backend, token);
        WrappedDekV1 backup = (await env.KeyAsync(write.Completion, token)).Value;
        FenceV1 fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(env.Root), token))!.Value;
        LifecycleV1 backupLifecycle = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!.Value;
        var decisions = new List<RestoredBackupAdmissionResult>();
        Func<RestoredBackupAdmissionResult, IReadOnlyList<PayloadProtectionStateMutation>, CancellationToken, ValueTask<RestoredBackupAdmissionResult>> record =
            async (decision, conditions, ct) =>
            {
                var audit = new PayloadProtectionAuditRecord(decision.AdmissionId, decision.AdmissionId, decision.AdmissionId, null,
                    env.Identity.TenantId, env.Identity.Domain, null, "Reconcile", null, null, backupLifecycle.Epoch, fence.ApprovalEpoch,
                    backend.BackendId, "v2", null, null, 1, 0, decision.State == RestoredBackupAdmissionState.Accepted ? "Succeeded" : "Failed",
                    decision.State == RestoredBackupAdmissionState.Accepted ? "none" : "consistency-mismatch", "qualification-actor", PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow));
                (await fixture.Store.CommitAsync([.. conditions, PayloadProtectionStateMutation.Put(audit)], ct)).ShouldBe(PayloadStateCommitResult.Committed);
                decisions.Add(decision); return decision;
            };
        var admission = new PayloadRestoreAdmission(fixture.Store, env.Roots, backend, new(), env.Leases, _ => true, record);
        var request = new RestoredBackupAdmissionRequest("01J00000000000000000000904", env.Identity.TenantId, env.Identity.Domain, env.Identity.AggregateId,
            null, null, "01J00000000000000000000905", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, KeyReferencePolicy.NoKeyReference, null, null, null, "qualification-actor");
        // An exact live backup is verified by a real decrypt under the recorded address before acceptance.
        (await admission.AdmitAsync(request, fence, backupLifecycle, [backup], token)).State.ShouldBe(RestoredBackupAdmissionState.Accepted);

        await using var cache = new PayloadKeyCache();
        var lifecycle = new KeyLifecycleCoordinator(fixture.Store, env.Roots, env.Leases, cache, backend.BackendId, _ => true, "qualification-actor");
        var approval = new CryptoShreddingWorkflowDecision(new("01J00000000000000000000906", env.Identity.TenantId, env.Identity.Domain,
            CryptoShreddingWorkflowScope.Aggregate, env.Identity.AggregateId, null, null, KeyReferencePolicy.NoKeyReference, null),
            CryptoShreddingWorkflowState.Approved, "approved", CryptoShreddingNextAction.None, null, null, "qualification-actor", DateTimeOffset.UtcNow, false, false);
        const string erase = "01J00000000000000000000907";
        (await lifecycle.BeginAsync(env.Identity, approval, erase, 2, true, token)).MemberCutoff.ShouldBe(1u);
        await lifecycle.AcknowledgeAsync(env.Identity, erase, token);
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store).TryClaimAsync(new(erase, env.Scope), token))!.Value;
        long started = Stopwatch.GetTimestamp();
        (await lifecycle.ResumeAsync(env.Identity, claim, token)).ShouldBeTrue();
        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(60));
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(backup), token)).ShouldBeNull();
        (await new PayloadKeyIndexes(fixture.Store).ReadScopeAsync(env.Scope, token)).Head!.Value.EntryCount.ShouldBe(0u);
        (await env.InventoryAsync(fixture.Erasure, token)).ShouldBe(0ul);
        (await new PayloadProtectionCatalogs(fixture.Store).ReadWorkAsync(env.Root, token)).Head.Value.EntryCount.ShouldBe(0u);
        PayloadProtectionStateValue<LifecycleV1> deleted = (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!;
        deleted.Value.State.ShouldBe("Deleted");
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(erase), token))!.Value.Phase.ShouldBe("Completed");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(erase), token))!.Value.Status.ShouldBe("Completed");
        // The acknowledging member has blocked all further work for the scope.
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => admission.AdmitAsync(request with { AdmissionId = "01J00000000000000000000908" },
            fence, backupLifecycle, [backup], token).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        // From a fresh process, a restored backup cannot resurrect the deleted scope; the KEK Secret itself is never claimed erased.
        PayloadProtectionLeaseCoordinator fresh = await env.RegisterAsync(token);
        (await new PayloadRestoreAdmission(fixture.Store, env.Roots, backend, new(), fresh, _ => true, record)
            .AdmitAsync(request with { AdmissionId = "01J00000000000000000000917" }, fence, backupLifecycle, [backup], token)).State
            .ShouldBe(RestoredBackupAdmissionState.Quarantined);
        (await fixture.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(env.Scope), token))!.Bytes.ToArray().ShouldBe(deleted.Bytes.ToArray());
        (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateCodec.KeyFor(backup), token)).ShouldBeNull();
        (await env.ReadAsync(backend, write.Payload, token, process: fresh)).UnreadableReason.ShouldBe(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        decisions.Select(d => d.State).ShouldBe([RestoredBackupAdmissionState.Accepted, RestoredBackupAdmissionState.Quarantined]);
    }

    [Fact(Explicit = true)]
    public async Task DeniedServiceAccountAndUngrantedSecretFailClosedWithoutPersistingAKey()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-denied", token);
        // A KEK inventory spans every scope using the address, so only this case's change to it is asserted.
        ulong inventoryBefore = await env.InventoryAsync(fixture.V1, token);
        var granted = Backend(fixture.V1);
        byte[] dek = RandomNumberGenerator.GetBytes(32);
        using PayloadBackendResult wrapped = await granted.WrapAsync("01J00000000000000000000909", dek, token);
        byte[] envelope = wrapped.TakeBytes();
        var denied = new DaprCryptographyPayloadProtectionBackend(fixture.Transport, new(KubernetesCryptoFixture.DeniedComponent, fixture.V1, []));
        // daprd 1.19.1 reports the Kubernetes 403 as Internal, so a denied ServiceAccount stays ConsistencyMismatch with one attempt per call.
        (await AssertFailureAsync(denied, envelope, fixture.V1, PayloadBackendFailure.ConsistencyMismatch, token)).ShouldBe([1, 1, 1]);
        // The restricted ServiceAccount may get only the named Secrets: an existing but unnamed Secret is refused by RBAC.
        (await AssertFailureAsync(Backend(fixture.Ungranted, fixture.V1), envelope, fixture.Ungranted, PayloadBackendFailure.ConsistencyMismatch, token))
            .ShouldBe([1, 1, 1]);
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => env.Reservations(denied)
            .ProtectEventAsync(KubernetesCryptoLiveEnvironment.Plain(), ["/secret"], new(env.Identity, "qualification-event", PayloadProtectionPayloadKind.Event, 1), token).AsTask());
        (await new PayloadKeyIndexes(fixture.Store).ReadScopeAsync(env.Scope, token)).Head.ShouldBeNull();
        (await env.InventoryAsync(fixture.V1, token)).ShouldBe(inventoryBefore);
        CryptographicOperations.ZeroMemory(dek); CryptographicOperations.ZeroMemory(envelope);
    }

    [Fact(Explicit = true)]
    public async Task MissingSecretIsAnOperatorConditionAndNeverProofOfAbsence()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        var missing = Backend(fixture.Missing, fixture.V1);
        using PayloadBackendResult wrapped = await Backend(fixture.V1).WrapAsync("01J00000000000000000000910", new byte[32], token);
        byte[] envelope = wrapped.TakeBytes();
        (await AssertFailureAsync(missing, envelope, fixture.Missing, PayloadBackendFailure.ConsistencyMismatch, token)).ShouldBe([1, 1, 1]);
        using PayloadBackendResult reconcile = await new PayloadBackendExecutor().ExecuteAsync(missing, ct => missing.ReconcileAsync("01J00000000000000000000910", ct),
            token, allowRetry: false, operation: PayloadProtectionOperation.Reconcile);
        // Wrap and unwrap never report Missing for a missing Secret; Reconcile is Missing without transport because Dapr keeps no mutation state.
        reconcile.Failure.ShouldBe(PayloadBackendFailure.Missing);
        CryptographicOperations.ZeroMemory(envelope);
    }

    [Fact(Explicit = true)]
    public async Task StoppedSidecarIsUnavailableRetriedUntilTheBreakerOpensAndTheRestartedSidecarServesAgain()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        using PayloadBackendResult wrapped = await Backend(fixture.V1).WrapAsync("01J00000000000000000000911", new byte[32], token);
        byte[] envelope = wrapped.TakeBytes();
        var stale = Backend(fixture.V1);
        await fixture.StopSidecarAsync();
        try
        {
            // The executor retries Unavailable. The fifth failure within 30 s opens the breaker, so the exact wrap stops after two attempts
            // and the unwrap that follows is refused without any transport attempt.
            (await AssertFailureAsync(stale, envelope, fixture.V1, PayloadBackendFailure.Unavailable, token)).ShouldBe([3, 2, 0]);
        }
        finally { await fixture.StartSidecarAsync(token); }
        using PayloadBackendResult served = await Backend(fixture.V1).UnwrapAsync("01J00000000000000000000911", fixture.V1, envelope, token);
        served.Failure.ShouldBe(PayloadBackendFailure.None);
        CryptographicOperations.ZeroMemory(served.TakeBytes());
    }

    [Fact(Explicit = true)]
    public async Task MissingOrWrongApiTokenIsDeniedWithoutRetryAndPersistsNothing()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-unauthenticated", token);
        ulong inventoryBefore = await env.InventoryAsync(fixture.V1, token);
        using PayloadBackendResult wrapped = await Backend(fixture.V1).WrapAsync("01J00000000000000000000918", RandomNumberGenerator.GetBytes(32), token);
        byte[] envelope = wrapped.TakeBytes();
        // The sidecar requires the per-run API token: a call without it or with another token is Unauthenticated, which section 7 maps to Denied.
        foreach (string? apiToken in new[] { null, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) })
        {
            using DaprCryptographyGrpcTransport transport = fixture.CreateTransport(apiToken);
            var unauthenticated = new DaprCryptographyPayloadProtectionBackend(transport, new(KubernetesCryptoFixture.Component, fixture.V1, []));
            (await AssertFailureAsync(unauthenticated, envelope, fixture.V1, PayloadBackendFailure.Denied, token)).ShouldBe([1, 1, 1]);
            if (apiToken is null)
            {
                await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => env.Reservations(unauthenticated)
                    .ProtectEventAsync(KubernetesCryptoLiveEnvironment.Plain(), ["/secret"], new(env.Identity, "qualification-event", PayloadProtectionPayloadKind.Event, 1), token).AsTask());
            }
        }

        (await new PayloadKeyIndexes(fixture.Store).ReadScopeAsync(env.Scope, token)).Head.ShouldBeNull();
        (await env.InventoryAsync(fixture.V1, token)).ShouldBe(inventoryBefore);
        CryptographicOperations.ZeroMemory(envelope);
    }

    [Fact(Explicit = true)]
    public async Task EnvelopesWrittenThroughDaprClientEncryptAsyncAndThroughTheTransportOpenInBothDirections()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        byte[] dek = RandomNumberGenerator.GetBytes(32);
        var backend = Backend(fixture.V1);
        // The legacy envelope is created at run time, because decrypting a committed one would need committed key material.
#pragma warning disable DAPR_CRYPTOGRAPHY // Test-only legacy SDK calls; no production source suppresses the diagnostic.
        ReadOnlyMemory<byte> legacy = await fixture.Client.EncryptAsync(KubernetesCryptoFixture.Component, dek, fixture.V1,
            new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256) { EncryptionCipher = DataEncryptionCipher.AesGcm }, token);
#pragma warning restore DAPR_CRYPTOGRAPHY
        byte[] legacyEnvelope = legacy.ToArray();
        legacyEnvelope.Length.ShouldBe(666); Header(legacyEnvelope).ShouldNotContain("\"k\":");
        using (PayloadBackendResult read = await backend.UnwrapAsync("01J00000000000000000000919", fixture.V1, legacyEnvelope, token))
        {
            read.Failure.ShouldBe(PayloadBackendFailure.None);
            byte[] plaintext = read.TakeBytes(); plaintext.ShouldBe(dek); CryptographicOperations.ZeroMemory(plaintext);
        }

        using PayloadBackendResult wrapped = await backend.WrapAsync("01J00000000000000000000919", dek, token);
        byte[] envelope = wrapped.TakeBytes();
        envelope.Length.ShouldBe(legacyEnvelope.Length); Header(envelope).ShouldNotContain("\"k\":");
#pragma warning disable DAPR_CRYPTOGRAPHY // Test-only legacy SDK call.
        ReadOnlyMemory<byte> opened = await fixture.Client.DecryptAsync(KubernetesCryptoFixture.Component, envelope, fixture.V1, token);
#pragma warning restore DAPR_CRYPTOGRAPHY
        byte[] legacyPlaintext = opened.ToArray();
        legacyPlaintext.ShouldBe(dek);
        CryptographicOperations.ZeroMemory(legacyPlaintext); CryptographicOperations.ZeroMemory(dek);
        CryptographicOperations.ZeroMemory(legacyEnvelope); CryptographicOperations.ZeroMemory(envelope);
    }

    [Fact(Explicit = true)]
    public async Task CallerCancellationRaisesCancellationAndReturnsNoKeyBytes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        var backend = Backend(fixture.V1);
        using PayloadBackendResult wrapped = await backend.WrapAsync("01J00000000000000000000912", new byte[32], token);
        byte[] envelope = wrapped.TakeBytes();
        foreach (bool unwrap in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            Task<PayloadBackendResult> pending = unwrap ? backend.UnwrapAsync("01J00000000000000000000912", fixture.V1, envelope, cancellation.Token).AsTask()
                : backend.WrapAsync("01J00000000000000000000912", new byte[32], cancellation.Token).AsTask();
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => pending);
        }
        using var precancelled = new CancellationTokenSource(); precancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => new PayloadBackendExecutor().ExecuteAsync(backend,
            ct => backend.WrapAsync("01J00000000000000000000912", new byte[32], ct), precancelled.Token).AsTask());
        CryptographicOperations.ZeroMemory(envelope);
    }

    [Fact(Explicit = true)]
    public async Task TamperedEnvelopeIsConsistencyMismatchAndYieldsNoPlaintext()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        var backend = Backend(fixture.V1);
        using PayloadBackendResult wrapped = await backend.WrapAsync("01J00000000000000000000913", RandomNumberGenerator.GetBytes(32), token);
        byte[] envelope = wrapped.TakeBytes();
        foreach (int position in new[] { envelope.Length - 1, envelope.Length / 2, WrappedDekV2.SchemePrefix.Length + 8 })
        {
            byte[] tampered = envelope.ToArray(); tampered[position] ^= 0x01;
            using PayloadBackendResult result = await backend.UnwrapAsync("01J00000000000000000000913", fixture.V1, tampered, token);
            result.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
            Should.Throw<PayloadProtectionFormatException>(() => result.TakeBytes());
        }
        using PayloadBackendResult truncated = await backend.UnwrapAsync("01J00000000000000000000913", fixture.V1, envelope.AsMemory(0, envelope.Length - 5), token);
        truncated.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
        CryptographicOperations.ZeroMemory(envelope);
    }

    [Fact(Explicit = true)]
    public async Task TheRecordedAddressAloneSelectsTheKeyAndAnEmbeddedEnvelopeNameNeverDoes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        byte[] dek = RandomNumberGenerator.GetBytes(32);
        // Another writer may embed a key name; the SDK requests that only when a decryption key name is supplied.
#pragma warning disable DAPR_CRYPTOGRAPHY // Test-only envelope construction; no production source suppresses the diagnostic.
        ReadOnlyMemory<byte> named = await fixture.Client.EncryptAsync(KubernetesCryptoFixture.Component, dek, fixture.V1,
            new EncryptionOptions(KeyWrapAlgorithm.RsaOaep256) { DecryptionKeyName = "embedded" }, token);
#pragma warning restore DAPR_CRYPTOGRAPHY
        byte[] envelope = named.ToArray();
        Header(envelope).ShouldContain("\"k\":\"" + fixture.V1 + "\"");
        var backend = Backend(fixture.V1, fixture.Alias, fixture.Other);
        // The same key bytes under another recorded address decrypt: the recorded address overrides the embedded name.
        using (PayloadBackendResult alias = await backend.UnwrapAsync("01J00000000000000000000914", fixture.Alias, envelope, token))
        {
            alias.Failure.ShouldBe(PayloadBackendFailure.None); alias.ProviderKeyVersionId.ShouldBe(fixture.Alias);
            byte[] plaintext = alias.TakeBytes(); plaintext.ShouldBe(dek); CryptographicOperations.ZeroMemory(plaintext);
        }
        // A different recorded key fails even though the envelope names the correct one.
        using (PayloadBackendResult other = await backend.UnwrapAsync("01J00000000000000000000914", fixture.Other, envelope, token)) { other.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch); }
        // EventStore's own envelopes embed no name at all.
        using PayloadBackendResult own = await backend.WrapAsync("01J00000000000000000000914", dek, token);
        byte[] ownEnvelope = own.TakeBytes();
        Header(ownEnvelope).ShouldNotContain("\"k\":");
        CryptographicOperations.ZeroMemory(dek); CryptographicOperations.ZeroMemory(envelope); CryptographicOperations.ZeroMemory(ownEnvelope);
    }

    [Fact(Explicit = true)]
    public async Task MaximumLengthAddressEnvelopeStaysWithinTheBoundAndPersistsAsWrappedDekV2()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        fixture.Maximum.Length.ShouldBe(256);
        DaprCryptographyPayloadProtectionOptions.ValidateKeyAddress(fixture.Maximum);
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-maximum", token);
        var backend = Backend(fixture.Maximum);
        var write = await env.WriteAsync(backend, token);
        WrappedDekV1 key = (await env.KeyAsync(write.Completion, token)).Value;
        key.ProviderKeyVersionId.ShouldBe(fixture.Maximum);
        byte[] envelope = Base64UrlCodec.Decode(key.WrappedDek);
        envelope.Length.ShouldBeLessThanOrEqualTo(WrappedDekV2.MaximumEnvelopeBytes);
        TestContext.Current.TestOutputHelper?.WriteLine($"Maximum-length address envelope: {envelope.Length} bytes.");
        await env.VerifyIndexesAsync(key, token);
        await env.AssertReadableAsync(backend, write.Payload, token);
    }

    [Fact(Explicit = true)]
    public async Task NoKeyAddressEnvelopeKeyByteOrPayloadSentinelReachesDiagnosticsResultsOrExceptions()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        CancellationToken token = timeout.Token;
        await fixture.StartAsync(token);
        var observed = new List<string>();
        var gate = new object();
        void Observe(string? value) { if (value is not null) { lock (gate) { observed.Add(value); } } }
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore.PayloadProtection",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                Observe(activity.DisplayName); Observe(activity.StatusDescription);
                foreach (var tag in activity.TagObjects) { Observe(tag.Key); Observe(tag.Value?.ToString()); }
            },
        };
        ActivitySource.AddActivityListener(activities);
        using var meters = new MeterListener
        {
            InstrumentPublished = (instrument, listener) => { if (instrument.Meter.Name == "Hexalith.EventStore.PayloadProtection") { listener.EnableMeasurementEvents(instrument); } },
        };
        meters.SetMeasurementEventCallback<long>((instrument, _, tags, _) => { Observe(instrument.Name); foreach (var tag in tags) { Observe(tag.Key); Observe(tag.Value?.ToString()); } });
        meters.SetMeasurementEventCallback<double>((instrument, _, tags, _) => { Observe(instrument.Name); foreach (var tag in tags) { Observe(tag.Key); Observe(tag.Value?.ToString()); } });
        meters.Start();
        KubernetesCryptoLiveEnvironment env = await KubernetesCryptoLiveEnvironment.CreateAsync(fixture.Store, "tenant-k8s-sentinel", token);
        var backend = Backend(fixture.V1, fixture.Missing);
        var write = await env.WriteAsync(backend, token);
        WrappedDekV1 key = (await env.KeyAsync(write.Completion, token)).Value;
        await env.AssertReadableAsync(backend, write.Payload, token);
        using (PayloadBackendResult unwrapped = await new PayloadBackendExecutor().ExecuteAsync(backend,
            ct => backend.UnwrapAsync("01J00000000000000000000915", fixture.V1, Base64UrlCodec.Decode(key.WrappedDek), ct), token, operation: PayloadProtectionOperation.Unwrap))
        {
            byte[] dek = unwrapped.TakeBytes();
            string dekHex = Convert.ToHexStringLower(dek);
            CryptographicOperations.ZeroMemory(dek);
            using PayloadBackendResult failed = await new PayloadBackendExecutor().ExecuteAsync(backend,
                ct => backend.WrapExactAsync("01J00000000000000000000915", fixture.Missing, new byte[32], ct), token);
            failed.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
            Exception failure = await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => env.Reservations(Backend(fixture.Missing))
                .ProtectEventAsync(KubernetesCryptoLiveEnvironment.Plain(), ["/secret"], new(env.Identity, "qualification-event", PayloadProtectionPayloadKind.Event, 2), token).AsTask());
            meters.RecordObservableInstruments();
            string[] sentinels = [fixture.V1, fixture.Missing, fixture.Namespace, "kek-v1", "kek-missing", key.WrappedDek, dekHex, KubernetesCryptoLiveEnvironment.Canary];
            string[] captured;
            lock (gate) { captured = [.. observed]; }
            string[] surfaces = [.. captured, failure.ToString(), failed.ToString(), unwrapped.ToString(), backend.ToString()!,
                Encoding.UTF8.GetString((await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(key.CreatingOperationId), token))!.Bytes.Span),
                Encoding.UTF8.GetString((await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(key.CreatingOperationId), token))!.Bytes.Span)];
            captured.ShouldNotBeEmpty();
            foreach (string sentinel in sentinels) { surfaces.ShouldAllBe(surface => !surface.Contains(sentinel, StringComparison.Ordinal)); }
            // The sidecar's own log never carries envelopes, key bytes or payload plaintext either.
            string log = fixture.SidecarLog();
            foreach (string sentinel in new[] { key.WrappedDek, dekHex, KubernetesCryptoLiveEnvironment.Canary }) { log.ShouldNotContain(sentinel); }
        }
    }

    private DaprCryptographyPayloadProtectionBackend Backend(string current, params string[] retained)
        => new(fixture.Transport, new(KubernetesCryptoFixture.Component, current, retained));

    // Runs wrap, exact wrap and unwrap through one executor and returns each call's transport attempt count.
    private async ValueTask<int[]> AssertFailureAsync(DaprCryptographyPayloadProtectionBackend backend, byte[] envelope, string address,
        PayloadBackendFailure expected, CancellationToken token)
    {
        var executor = new PayloadBackendExecutor();
        int[] attempts = new int[3];
        using PayloadBackendResult wrap = await executor.ExecuteAsync(backend, ct => backend.WrapAsync("01J00000000000000000000916", new byte[32], ct), token,
            attempted: _ => attempts[0]++);
        using PayloadBackendResult exact = await executor.ExecuteAsync(backend, ct => backend.WrapExactAsync("01J00000000000000000000916", address, new byte[32], ct),
            token, attempted: _ => attempts[1]++);
        using PayloadBackendResult unwrap = await executor.ExecuteAsync(backend, ct => backend.UnwrapAsync("01J00000000000000000000916", address, envelope, ct), token,
            operation: PayloadProtectionOperation.Unwrap, attempted: _ => attempts[2]++);
        foreach (PayloadBackendResult result in new[] { wrap, exact, unwrap })
        {
            // Provider and transport failures are never Missing, Invalidated or a deletion signal.
            result.Failure.ShouldBe(expected);
            result.ProviderKeyVersionId.ShouldBeNull();
            Should.Throw<PayloadProtectionFormatException>(() => result.TakeBytes());
        }

        // Every failed call ended on the calling task: nothing reaches the unobserved-task-exception channel.
        await fixture.AssertNoUnobservedTaskExceptionAsync();
        return attempts;
    }

    private static string Header(byte[] envelope)
    {
        int first = Array.IndexOf(envelope, (byte)'\n');
        int second = Array.IndexOf(envelope, (byte)'\n', first + 1);
        return Encoding.ASCII.GetString(envelope, first + 1, second - first - 1);
    }
}
