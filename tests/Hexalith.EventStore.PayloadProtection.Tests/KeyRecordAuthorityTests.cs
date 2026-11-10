// Approved Dapr Kubernetes cryptography supplement: 14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881; sections 4-6.
// Profile authority: 29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44.
using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Third authority profile binding, reader capability, V2 lifecycle through unchanged callers and mixed-reader refusals.</summary>
public sealed class KeyRecordAuthorityTests
{
    private const string V1Address = "pp-keys/kek-v1/key.pem";
    private const string V2Address = "pp-keys/kek-v2/key.pem";
    private const string Outside = "pp-keys/kek-v3/key.pem";
    private static readonly PayloadProtectionAuthorityProfile Kubernetes = PayloadProtectionAuthorityProfile.DaprKubernetesCrypto;

    [Fact]
    public void ConstantsBindTheExactApprovedSupplementEffectiveDigestAndPairedFrozenIntent()
    {
        byte[] amendment = Artifact("spec-8-12-dapr-kubernetes-cryptography-amendment.md");
        string normative = Convert.ToHexStringLower(SHA256.HashData(Between(amendment, "<!-- HX-PP-DAPR-K8S-BEGIN -->\n", "<!-- HX-PP-DAPR-K8S-END -->\n")));
        normative.ShouldBe(PayloadProtectionAuthorityProfile.DaprKubernetesCryptoSupplementDigest);
        byte[] preimage = [.. "HXPA"u8, 0x03, .. Convert.FromHexString(PayloadProtectionStateKeys.AuthorityDigest), .. Convert.FromHexString(normative)];
        Convert.ToHexStringLower(SHA256.HashData(preimage)).ShouldBe(PayloadProtectionAuthorityProfile.DaprKubernetesCryptoDigest);
        Kubernetes.Digest.ShouldBe("29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44");
        Kubernetes.KeyRecordSchemaVersion.ShouldBe(2);
        string text = Encoding.UTF8.GetString(amendment);
        text.ShouldContain("status: 'approved-authorized'");
        text.ShouldContain("candidate_normative_sha256: '" + normative + "'");
        text.ShouldContain("proposed_effective_authority_sha256: '" + Kubernetes.Digest + "'");
        text.ShouldContain("predecessor_effective_authority_sha256: '" + PayloadProtectionStateKeys.AuthorityDigest + "'");
        byte[] spec = Artifact("spec-8-12-dapr-kubernetes-cryptography.md");
        byte[] frozen = Block(spec, "<frozen-after-approval", "</frozen-after-approval>");
        text.ShouldContain("paired_spec_frozen_intent_sha256: '" + Convert.ToHexStringLower(SHA256.HashData(frozen)) + "'");
    }

    [Fact]
    public void NoProductionSourceSuppressesTheExperimentalCryptographyDiagnostic()
    {
        // Revision 3 section 2: the generated Dapr.Protos client is not experimental, so no production source or build setting suppresses
        // DAPR_CRYPTOGRAPHY: no pragma, NoWarn or diagnostic severity under src/ or in the repository-root build files (references/ excluded).
        string[] patterns = ["*.cs", "*.csproj", "*.props", "*.targets", ".editorconfig", "*.globalconfig"];
        string[] sources = [.. patterns.SelectMany(pattern => Directory.GetFiles(Path.Combine(Root(), "src"), pattern, SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)),
            .. patterns.SelectMany(pattern => Directory.GetFiles(Root(), pattern, SearchOption.TopDirectoryOnly))];
        sources.ShouldContain(path => path.EndsWith("DaprCryptographyGrpcTransport.cs", StringComparison.Ordinal));
        sources.ShouldContain(path => path.EndsWith("Hexalith.EventStore.PayloadProtection.csproj", StringComparison.Ordinal));
        sources.ShouldContain(path => Path.GetFileName(path) == "Directory.Build.props" && Path.GetDirectoryName(path) == Root());
        sources.ShouldContain(path => Path.GetFileName(path) == ".editorconfig" && Path.GetDirectoryName(path) == Root());
        sources.Where(path => File.ReadAllText(path).Contains("DAPR_CRYPTOGRAPHY", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public void PredecessorApprovalsAndProfilesAreByteUnchanged()
    {
        byte[] baseRange = Between(Artifact("spec-shared-payload-protection-engine.md"), "<!-- HX-PP-V2-NORMATIVE-BEGIN -->\n", "<!-- HX-PP-V2-NORMATIVE-END -->\n");
        Convert.ToHexStringLower(SHA256.HashData(baseRange)).ShouldBe("de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e");
        byte[] lifecycle = SHA256.HashData(Between(Artifact("spec-8-5-lifecycle-protocol-amendment.md"), "<!-- HX-PP-LIFECYCLE-AMENDMENT-BEGIN -->\n", "<!-- HX-PP-LIFECYCLE-AMENDMENT-END -->\n"));
        Convert.ToHexStringLower(lifecycle).ShouldBe("a853201a6df1e8e88730a40a582b87e20c5e5cf7a223f01ca905e9c9ee7df94d");
        Convert.ToHexStringLower(SHA256.HashData([.. "HXPA"u8, 0x01, .. SHA256.HashData(baseRange), .. lifecycle])).ShouldBe(PayloadProtectionStateKeys.AuthorityDigest);
        byte[] onPremises = SHA256.HashData(Between(Artifact("spec-8-7-on-premises-backend-amendment.md"), "<!-- HX-PP-ONPREM-CANDIDATE-BEGIN -->\n", "<!-- HX-PP-ONPREM-CANDIDATE-END -->\n"));
        Convert.ToHexStringLower(onPremises).ShouldBe("43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3");
        Convert.ToHexStringLower(SHA256.HashData([.. "HXPA"u8, 0x02, .. Convert.FromHexString(PayloadProtectionStateKeys.AuthorityDigest), .. onPremises]))
            .ShouldBe(PayloadProtectionAuthorityProfile.PlatformCustody.Digest);
        PayloadProtectionAuthorityProfile.Predecessor.KeyRecordSchemaVersion.ShouldBe(1);
        PayloadProtectionAuthorityProfile.PlatformCustody.KeyRecordSchemaVersion.ShouldBe(1);
        Kubernetes.Digest.ShouldNotBe(PayloadProtectionAuthorityProfile.PlatformCustody.Digest);
    }

    [Fact]
    public async Task ReservationRotationRewrapAndReadPersistOnlyWrappedDekV2ThroughTheUnchangedCallers()
    {
        var sidecar = new DaprCryptoTestSidecar(V1Address, V2Address);
        KubernetesCryptoTestEnvironment env = await KubernetesCryptoTestEnvironment.CreateAsync();
        var phase0 = Backend(sidecar, V1Address);
        var first = await env.WriteAsync(phase0);
        PayloadProtectionStateValue<WrappedDekV1> stored = await env.KeyAsync(first.Completion);
        WrappedDekV2 key = stored.Value.ShouldBeOfType<WrappedDekV2>();
        Encoding.UTF8.GetString(stored.Bytes.Span).ShouldStartWith("{\"schemaVersion\":2,");
        key.State.ShouldBe("Active"); key.BackendId.ShouldBe(WrappedDekV2.ClosedBackendId); key.WrapAlgorithmId.ShouldBe(WrappedDekV2.ClosedWrapAlgorithmId);
        key.ProviderKeyVersionId.ShouldBe(V1Address);
        (await env.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(KubernetesCryptoTestEnvironment.Root)))!.Value.ApprovedSpecDigest.ShouldBe(Kubernetes.Digest);
        await env.AssertReadableAsync(phase0, first.Result.PayloadBytes);

        // Two-phase rollout: first admit the new address everywhere, then make it current and retain the old one.
        await env.AssertReadableAsync(Backend(sidecar, V1Address, V2Address), first.Result.PayloadBytes);
        var phase2 = Backend(sidecar, V2Address, V1Address);
        var second = await env.WriteAsync(phase2, sequence: 2);
        (await env.KeyAsync(second.Completion)).Value.ProviderKeyVersionId.ShouldBe(V2Address);
        await env.AssertReadableAsync(phase2, first.Result.PayloadBytes);
        var rotation = new PayloadKeyRotationCoordinator(env.Store, env.Roots, env.Leases, phase2, new(env.Clock), _ => true, "fixture-actor", env.Clock);
        (await rotation.VerifyInventoryAsync(V1Address)).Head!.Value.EntryCount.ShouldBe(1ul);
        (await rotation.RewrapAsync(KubernetesCryptoTestEnvironment.Identity, first.Completion.KeyReference, 1, "01J00000000000000000000821", V2Address)).ShouldBeTrue();
        WrappedDekV1 rewrapped = (await env.KeyAsync(first.Completion)).Value;
        rewrapped.ShouldBeOfType<WrappedDekV2>(); rewrapped.ProviderKeyVersionId.ShouldBe(V2Address); rewrapped.State.ShouldBe("Active");
        rewrapped.WrappedDek.ShouldNotBe(key.WrappedDek); rewrapped.CreatingOperationId.ShouldBe(key.CreatingOperationId);
        (await rotation.VerifyInventoryAsync(V1Address)).Head!.Value.EntryCount.ShouldBe(0ul);
        (await rotation.VerifyInventoryAsync(V2Address)).Head!.Value.EntryCount.ShouldBe(2ul);
        await env.AssertReadableAsync(Backend(sidecar, V2Address), first.Result.PayloadBytes);
        sidecar.Inputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
        sidecar.Outputs.ShouldAllBe(buffer => buffer.All(b => b == 0));
        CryptographicOperations.ZeroMemory(first.Result.PayloadBytes); CryptographicOperations.ZeroMemory(second.Result.PayloadBytes);
    }

    [Fact]
    public async Task AnAddressOutsideTheInventoryIsRefusedBeforeTransportAndReconcileRestoresThePriorKey()
    {
        var sidecar = new DaprCryptoTestSidecar(V1Address, Outside);
        KubernetesCryptoTestEnvironment env = await KubernetesCryptoTestEnvironment.CreateAsync();
        var backend = Backend(sidecar, V1Address);
        var write = await env.WriteAsync(backend);
        WrappedDekV1 before = (await env.KeyAsync(write.Completion)).Value;
        var rotation = new PayloadKeyRotationCoordinator(env.Store, env.Roots, env.Leases, backend, new(env.Clock), _ => true, "fixture-actor", env.Clock);
        const string operation = "01J00000000000000000000822";
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(KubernetesCryptoTestEnvironment.Identity, write.Completion.KeyReference, 1, operation, Outside).AsTask());
        sidecar.Calls.ShouldNotContain((true, Outside));
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(env.Store, env.Clock).TryClaimAsync(new(operation, KubernetesCryptoTestEnvironment.Scope)))!.Value;
        int calls = sidecar.Calls.Count;
        (await rotation.ReconcileAsync(KubernetesCryptoTestEnvironment.Identity, claim)).ShouldBeFalse();
        sidecar.Calls.Count.ShouldBe(calls);
        await AssertRestoredAsync(env, write.Completion, before, operation);
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Fact]
    public async Task InterruptedReservationWithoutAKeyRecordIsReconciledToNotPersistedFromDurableStateAlone()
    {
        using var cancellation = new CancellationTokenSource();
        var sidecar = new DaprCryptoTestSidecar(V1Address) { Fault = (encrypt, _, _, _) => { if (encrypt) { cancellation.Cancel(); } return Task.FromResult<byte[]?>(null); } };
        KubernetesCryptoTestEnvironment env = await KubernetesCryptoTestEnvironment.CreateAsync();
        var backend = Backend(sidecar, V1Address);
        await Should.ThrowAsync<OperationCanceledException>(() => env.Reservations(backend)
            .ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context(), cancellation.Token).AsTask());
        OperationV1 interrupted = (await env.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(env.Store.Audits.Single(a => a.Operation == "Create").OperationId)))!.Value;
        interrupted.Phase.ShouldBe("Ambiguous");
        (await env.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(KubernetesCryptoTestEnvironment.Scope, interrupted.KeyRef, interrupted.DekVersion))).ShouldBeNull();
        int calls = sidecar.Calls.Count;
        await using var cache = new PayloadKeyCache(timeProvider: env.Clock);
        using var worker = new PayloadProtectionReconciler(env.Store, env.Roots, Lifecycle(env, cache, backend), backend.BackendId, _ => true, "fixture-actor", env.Clock,
            backend: backend, executor: new(env.Clock), authorityProfile: Kubernetes);
        (await worker.RunPassAsync()).ShouldBe(1);
        OperationV1 failed = (await env.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(interrupted.OperationId)))!.Value;
        failed.Phase.ShouldBe("Failed"); failed.Result.ShouldBe("Failed"); failed.ReasonCode.ShouldBe("not-persisted");
        (await env.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(interrupted.OperationId)))!.Value.ReasonCode.ShouldBe("not-persisted");
        env.Store.Audits.ShouldContain(a => a.OperationId == interrupted.OperationId && a.Operation == "Reconcile" && a.ReasonCode == "not-persisted");
        sidecar.Calls.Count.ShouldBe(calls);
    }

    [Fact]
    public async Task InterruptedV2RewrapIsRestoredToItsPriorStateAndOldWrapFromDurableStateAlone()
    {
        var sidecar = new DaprCryptoTestSidecar(V1Address, V2Address);
        KubernetesCryptoTestEnvironment env = await KubernetesCryptoTestEnvironment.CreateAsync();
        var backend = Backend(sidecar, V2Address, V1Address);
        var write = await env.WriteAsync(Backend(sidecar, V1Address));
        WrappedDekV1 before = (await env.KeyAsync(write.Completion)).Value;
        using var cancellation = new CancellationTokenSource();
        sidecar.Fault = (encrypt, _, _, _) => { if (encrypt) { cancellation.Cancel(); } return Task.FromResult<byte[]?>(null); };
        var rotation = new PayloadKeyRotationCoordinator(env.Store, env.Roots, env.Leases, backend, new(env.Clock), _ => true, "fixture-actor", env.Clock);
        const string operation = "01J00000000000000000000823";
        await Should.ThrowAsync<OperationCanceledException>(() => rotation.RewrapAsync(KubernetesCryptoTestEnvironment.Identity, write.Completion.KeyReference, 1, operation, V2Address, cancellation.Token).AsTask());
        sidecar.Fault = null;
        WrappedDekV1 pending = (await env.KeyAsync(write.Completion)).Value;
        pending.State.ShouldBe("RewrapPending"); pending.WrappedDek.ShouldBe(before.WrappedDek);
        int calls = sidecar.Calls.Count;
        await using var cache = new PayloadKeyCache(timeProvider: env.Clock);
        using var worker = new PayloadProtectionReconciler(env.Store, env.Roots, Lifecycle(env, cache, backend), backend.BackendId, _ => true, "fixture-actor", env.Clock,
            rotation: rotation, backend: backend, executor: new(env.Clock), authorityProfile: Kubernetes);
        (await worker.RunPassAsync()).ShouldBe(1);
        sidecar.Calls.Count.ShouldBe(calls);
        await AssertRestoredAsync(env, write.Completion, before, operation);
        await env.AssertReadableAsync(backend, write.Result.PayloadBytes);
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Fact]
    public async Task ProviderFailureDuringReservationIsTypedAndCreatesNoKey()
    {
        var sidecar = new DaprCryptoTestSidecar(V1Address) { Fault = (_, _, _, _) => Task.FromResult<byte[]?>([]) };
        KubernetesCryptoTestEnvironment env = await KubernetesCryptoTestEnvironment.CreateAsync();
        // The core reports only a bounded cryptographic failure; the typed provider outcome is the durable audit reason.
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => env.Reservations(Backend(sidecar, V1Address))
            .ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        env.Store.Audits.Single(a => a.Operation == "Create").ReasonCode.ShouldBe("consistency-mismatch");
        env.Store.Audits.ShouldAllBe(a => a.ToState != "Reserved");
        (await new PayloadKeyIndexes(env.Store, env.Clock).ReadScopeAsync(KubernetesCryptoTestEnvironment.Scope)).Head.ShouldBeNull();
    }

    [Fact]
    public async Task DeniedProviderStatusDuringReservationIsAuditedAsProviderDeniedAndCreatesNoKey()
    {
        var sidecar = new DaprCryptoTestSidecar(V1Address) { End = (DaprCryptoTestSidecar.AtRead, StatusCode.PermissionDenied) };
        KubernetesCryptoTestEnvironment env = await KubernetesCryptoTestEnvironment.CreateAsync();
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => env.Reservations(Backend(sidecar, V1Address))
            .ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        PayloadProtectionAuditRecord create = env.Store.Audits.Single(a => a.Operation == "Create");
        create.ReasonCode.ShouldBe("provider-denied");
        OperationV1 operation = (await env.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(create.OperationId)))!.Value;
        operation.Phase.ShouldBe("Failed"); operation.Result.ShouldBe("Failed"); operation.ReasonCode.ShouldBe("provider-denied");
        (await new PayloadKeyIndexes(env.Store, env.Clock).ReadScopeAsync(KubernetesCryptoTestEnvironment.Scope)).Head.ShouldBeNull();
    }

    [Fact]
    public async Task MixedProcessesAndBackendsFailClosedWithTypedOutcomesWithoutRestamping()
    {
        KubernetesCryptoTestEnvironment kubernetes = await KubernetesCryptoTestEnvironment.CreateAsync();
        var predecessorProcess = new PayloadProtectionLeaseCoordinator(kubernetes.Store, kubernetes.Roots,
            new(new('b', 40), new('c', 64), "lifecycle-test", ["json", "json+pdenc-v2"], ["V2"]), kubernetes.Clock);
        int commits = kubernetes.Store.Commits;
        PayloadProtectionStateValue<DeploymentGenerationV1> root = (await kubernetes.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(KubernetesCryptoTestEnvironment.Root)))!;
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => predecessorProcess.RegisterAsync(KubernetesCryptoTestEnvironment.Identity).AsTask())).Reason
            .ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        kubernetes.Store.Commits.ShouldBe(commits);
        (await kubernetes.Store.ReadAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(KubernetesCryptoTestEnvironment.Root)))!.Bytes.ToArray().ShouldBe(root.Bytes.ToArray());

        ReservationTestFixture predecessor = await ReservationTestFixture.CreateAsync();
        var predecessorRoots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var v2Process = new PayloadProtectionLeaseCoordinator(predecessor.Store, predecessorRoots,
            new(new('b', 40), new('c', 64), WrappedDekV2.ClosedBackendId, ["json", "json+pdenc-v2", "wrapped-dek-v2"], ["V2"]), predecessor.Clock, authorityProfile: Kubernetes);
        commits = predecessor.Store.Commits;
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => v2Process.RegisterAsync(KubernetesCryptoTestEnvironment.Identity).AsTask())).Reason
            .ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        predecessor.Store.Commits.ShouldBe(commits);
        (await predecessor.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(predecessor.Root)))!.Value.ApprovedSpecDigest.ShouldBe(PayloadProtectionStateKeys.AuthorityDigest);

        var sidecar = new DaprCryptoTestSidecar(V1Address);
        Should.Throw<PayloadProtectionFormatException>(() => kubernetes.Reservations(new LifecycleTestBackend()));
        Should.Throw<PayloadProtectionFormatException>(() => new KeyReservationCoordinator(predecessor.Store, predecessorRoots, predecessor.Leases,
            new(predecessorRoots, [new("parties", predecessor.Erasure)]), Backend(sidecar, V1Address), new(predecessor.Clock), _ => true, "fixture-actor", timeProvider: predecessor.Clock));
    }

    private static KeyLifecycleCoordinator Lifecycle(KubernetesCryptoTestEnvironment env, PayloadKeyCache cache, PayloadProtectionBackend backend)
        => new(env.Store, env.Roots, env.Leases, cache, backend.BackendId, _ => true, "fixture-actor", env.Clock);

    // Missing restores the exact prior state, address and old wrap, and terminalizes only the rewrap's own work.
    private static async ValueTask AssertRestoredAsync(KubernetesCryptoTestEnvironment env, PayloadProtectionCompletionContext completion, WrappedDekV1 before, string operation)
    {
        WrappedDekV1 restored = (await env.KeyAsync(completion)).Value;
        restored.ShouldBeOfType<WrappedDekV2>(); restored.State.ShouldBe(before.State);
        restored.ProviderKeyVersionId.ShouldBe(before.ProviderKeyVersionId); restored.WrappedDek.ShouldBe(before.WrappedDek);
        restored.WrappedDekSha256.ShouldBe(before.WrappedDekSha256);
        (await env.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(operation)))!.Value.ReasonCode.ShouldBe("not-persisted");
        (await env.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation)))!.Value.Status.ShouldBe("Completed");
        (await new PayloadProtectionCatalogs(env.Store, env.Clock).ReadWorkAsync(KubernetesCryptoTestEnvironment.Root)).Pages
            .SelectMany(p => p.Value.Entries).ShouldNotContain(e => e.OperationId == operation);
        env.Store.Audits.ShouldNotContain(a => a.ReasonCode == "missing-key" || a.ReasonCode == "key-invalidated" || a.ToState == "Deleted");
    }

    private static DaprCryptographyPayloadProtectionBackend Backend(DaprCryptoTestSidecar sidecar, string current, params string[] retained)
        => new(sidecar.Transport, new(sidecar.Component, current, retained));

    private static byte[] Artifact(string name)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(Root(), "_bmad-output", "implementation-artifacts", name));
        bytes.ShouldNotContain((byte)'\r'); bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]).ShouldBeFalse();
        return bytes;
    }

    private static string Root()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) { return directory.FullName; }
        }

        throw new DirectoryNotFoundException("The repository root was not found.");
    }

    // Exact bytes strictly between two unique marker lines.
    private static byte[] Between(byte[] document, string begin, string end) => Slice(document, begin, end, includeMarkers: false);

    // Exact bytes of one unique block, including its opening and closing markers.
    private static byte[] Block(byte[] document, string begin, string end) => Slice(document, begin, end, includeMarkers: true);

    private static byte[] Slice(byte[] document, string begin, string end, bool includeMarkers)
    {
        string text = Encoding.UTF8.GetString(document);
        int first = text.IndexOf(begin, StringComparison.Ordinal);
        first.ShouldBeGreaterThanOrEqualTo(0); text.IndexOf(begin, first + 1, StringComparison.Ordinal).ShouldBe(-1);
        int last = text.IndexOf(end, first + begin.Length, StringComparison.Ordinal);
        last.ShouldBeGreaterThan(first); text.IndexOf(end, last + 1, StringComparison.Ordinal).ShouldBe(-1);
        return Encoding.UTF8.GetBytes(includeMarkers ? text[first..(last + end.Length)] : text[(first + begin.Length)..last]);
    }
}
