// Approved Dapr Kubernetes cryptography supplement: 3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd; sections 4-8.
using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Creates one isolated root bound to the Dapr Kubernetes authority through real Dapr transactions.</summary>
internal sealed class KubernetesCryptoLiveEnvironment
{
    private KubernetesCryptoLiveEnvironment(DaprPayloadProtectionStateStore store, string tenant)
    {
        Store = store;
        Identity = new(tenant, "qualification", "aggregate-01");
        Roots = new([new(tenant, "qualification")]);
        Erasure = new(Roots, [new("qualification", new LifecycleQualificationErasure())]);
    }

    /// <summary>Gets the sentinel embedded in every protected payload.</summary>
    internal static string Canary => "pp-k8s-sensitive-canary-5q81";
    /// <summary>Gets the exact state primitive.</summary>
    internal DaprPayloadProtectionStateStore Store { get; }
    /// <summary>Gets the isolated identity.</summary>
    internal AggregateIdentity Identity { get; }
    /// <summary>Gets the explicitly enrolled root.</summary>
    internal PayloadProtectionScopeRegistry Roots { get; }
    /// <summary>Gets the explicit erasure authority.</summary>
    internal ErasureStateRegistry Erasure { get; }
    /// <summary>Gets registered process authority.</summary>
    internal PayloadProtectionLeaseCoordinator Leases { get; private set; } = null!;
    /// <summary>Gets the exact root address.</summary>
    internal string Root => PayloadProtectionStateKeys.TenantDomainDigest(Identity.TenantId, Identity.Domain);
    /// <summary>Gets the exact scope address.</summary>
    internal string Scope => PayloadProtectionStateKeys.ScopeDigest(Identity);
    /// <summary>Gets the immutable V2-reader capability profile.</summary>
    internal static PayloadProtectionInstanceProfile Profile { get; } = new(new('b', 40), new('c', 64), WrappedDekV2.ClosedBackendId,
        ["json", "json+pdenc-v2", "wrapped-dek-v2"], ["V2"]);

    /// <summary>Qualifies the store, then creates catalogs, lifecycle, deployment and fence under the third authority and registers one reader.</summary>
    internal static async ValueTask<KubernetesCryptoLiveEnvironment> CreateAsync(DaprPayloadProtectionStateStore store, string tenant, CancellationToken token)
    {
        await new PayloadProtectionCapabilityProbe(store).ProbeAsync(token);
        var env = new KubernetesCryptoLiveEnvironment(store, tenant);
        PayloadProtectionAuthorityProfile authority = PayloadProtectionAuthorityProfile.DaprKubernetesCrypto;
        string now = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        (await new PayloadProtectionCatalogs(store).InitializeAsync(env.Root, token)).ShouldBe(PayloadStateCommitResult.Committed);
        (await store.CommitAsync([PayloadProtectionStateMutation.Put(new LifecycleV1(tenant, "qualification", env.Identity.AggregateId, env.Scope, "Active", 1, "none", "01J00000000000000000000901", now)),
            PayloadProtectionStateMutation.Put(new DeploymentGenerationV1(tenant, "qualification", env.Root, 1, authority.Digest, Profile.SourceSha, Profile.PackageSetSha256, "Open", now), authorityProfile: authority),
            PayloadProtectionStateMutation.Put(new FenceV1(tenant, "qualification", env.Root, "V2", 1, "01J00000000000000000000902", authority.Digest, 1, "Legacy", 0, now), authorityProfile: authority)], token))
            .ShouldBe(PayloadStateCommitResult.Committed);
        env.Leases = await env.RegisterAsync(token);
        return env;
    }

    /// <summary>Registers a fresh process incarnation for this root.</summary>
    internal async ValueTask<PayloadProtectionLeaseCoordinator> RegisterAsync(CancellationToken token, DaprPayloadProtectionStateStore? store = null)
    {
        var leases = new PayloadProtectionLeaseCoordinator(store ?? Store, Roots, Profile, authorityProfile: PayloadProtectionAuthorityProfile.DaprKubernetesCrypto);
        await leases.RegisterAsync(Identity, token);
        return leases;
    }

    /// <summary>Creates a reservation coordinator for one backend phase.</summary>
    internal KeyReservationCoordinator Reservations(PayloadProtectionBackend backend)
        => new(Store, Roots, Leases, Erasure, backend, new(), _ => true, "qualification-actor");

    /// <summary>Protects, admits and confirms persistence through the same issuing coordinator.</summary>
    internal async ValueTask<(byte[] Payload, PayloadProtectionCompletionContext Completion)> WriteAsync(PayloadProtectionBackend backend, CancellationToken token, ulong sequence = 1)
    {
        KeyReservationCoordinator reservations = Reservations(backend);
        var write = await reservations.ProtectEventAsync(Plain(), ["/secret"], Context(sequence), token);
        await reservations.AcquireCompletionAsync(write.Completion!, token);
        await reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted, token);
        return (write.Result.PayloadBytes, write.Completion!);
    }

    /// <summary>Reads the exact persisted key record through the Dapr state boundary.</summary>
    internal async ValueTask<PayloadProtectionStateValue<WrappedDekV1>> KeyAsync(PayloadProtectionCompletionContext completion, CancellationToken token,
        DaprPayloadProtectionStateStore? store = null)
        => (await (store ?? Store).ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(Scope, completion.KeyReference, completion.DekVersion), token))!;

    /// <summary>Verifies persisted scope-index and KEK-inventory membership of one key.</summary>
    internal async ValueTask VerifyIndexesAsync(WrappedDekV1 key, CancellationToken token)
    {
        var indexes = new PayloadKeyIndexes(Store);
        PayloadKeyIndexes.VerifyMembership(key, await indexes.ReadScopeAsync(Scope, token),
            await indexes.ReadKekAsync(PayloadProtectionStateKeys.Digest(key.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId), token));
    }

    /// <summary>Reads a KEK-inventory head entry count for one exact address.</summary>
    internal async ValueTask<ulong> InventoryAsync(string address, CancellationToken token)
        => (await new PayloadKeyIndexes(Store).ReadKekAsync(PayloadProtectionStateKeys.Digest(WrappedDekV2.ClosedBackendId), PayloadProtectionStateKeys.Digest(address), token)).Head?.Value.EntryCount ?? 0;

    /// <summary>Decrypts the immutable payload through the unchanged resolver and a fresh cache.</summary>
    internal async ValueTask<CoreUnprotectionResult> ReadAsync(PayloadProtectionBackend backend, byte[] payload, CancellationToken token,
        ulong sequence = 1, PayloadProtectionLeaseCoordinator? process = null, DaprPayloadProtectionStateStore? store = null)
    {
        await using var cache = new PayloadKeyCache();
        var resolver = new PayloadKeyResolver(store ?? Store, Roots, Erasure, process ?? Leases, backend, new(), cache);
        CoreUnprotectionResult result = await new PayloadProtectionCore().TryUnprotectEventAsync(payload, Context(sequence),
            (key, version, ct) => resolver.ResolveAsync(Identity, key, version, new CryptographicPayloadProtectionEntropy().CreateKeyReference(), ct), token);
        await cache.ClearScopeAsync(Scope, token);
        return result;
    }

    /// <summary>Asserts the payload decrypts to the canary plaintext and clears the returned copy.</summary>
    internal async ValueTask AssertReadableAsync(PayloadProtectionBackend backend, byte[] payload, CancellationToken token, ulong sequence = 1,
        PayloadProtectionLeaseCoordinator? process = null, DaprPayloadProtectionStateStore? store = null)
    {
        CoreUnprotectionResult result = await ReadAsync(backend, payload, token, sequence, process, store);
        try { result.UnreadableReason.ShouldBeNull(); result.PayloadBytes.ShouldBe(Plain()); }
        finally { if (result.PayloadBytes is not null) { CryptographicOperations.ZeroMemory(result.PayloadBytes); } }
    }

    /// <summary>Gets the canary plaintext.</summary>
    internal static byte[] Plain() => Encoding.UTF8.GetBytes("{\"secret\":\"" + Canary + "\"}");

    private PayloadProtectionContext Context(ulong sequence) => new(Identity, "qualification-event", PayloadProtectionPayloadKind.Event, sequence);
}
