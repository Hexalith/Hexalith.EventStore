// Approved Dapr Kubernetes cryptography supplement: e31a6b1d147f9e71bcbad97edc39d556e384fa587fdbdd391ce7e290ecff3034; sections 4-6.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>One enrolled root under the Dapr Kubernetes authority over the deterministic in-memory CAS fixture; never qualification evidence.</summary>
internal sealed class KubernetesCryptoTestEnvironment
{
    private KubernetesCryptoTestEnvironment()
    {
        Store = new(Clock, PayloadProtectionAuthorityProfile.DaprKubernetesCrypto);
        Erasure = new(Roots, [new("parties", new LifecycleTestErasureProvider())]);
    }

    /// <summary>Gets the authenticated fixture scope.</summary>
    internal static AggregateIdentity Identity { get; } = new("tenant-a", "parties", "party-01");
    /// <summary>Gets the monotonic test clock.</summary>
    internal LifecycleTestClock Clock { get; } = new();
    /// <summary>Gets the atomic fault fixture decoding under the Dapr Kubernetes authority.</summary>
    internal MemoryPayloadProtectionStateStore Store { get; }
    /// <summary>Gets the explicitly enrolled root.</summary>
    internal PayloadProtectionScopeRegistry Roots { get; } = new([new("tenant-a", "parties")]);
    /// <summary>Gets the owned erasure registry.</summary>
    internal ErasureStateRegistry Erasure { get; }
    /// <summary>Gets the registered process authority.</summary>
    internal PayloadProtectionLeaseCoordinator Leases { get; private set; } = null!;
    /// <summary>Gets the exact root address.</summary>
    internal static string Root => PayloadProtectionStateKeys.TenantDomainDigest("tenant-a", "parties");
    /// <summary>Gets the exact scope address.</summary>
    internal static string Scope => PayloadProtectionStateKeys.ScopeDigest(Identity);

    /// <summary>Creates catalogs, lifecycle, deployment and fence bound to the third profile, then registers one V2 reader.</summary>
    internal static async ValueTask<KubernetesCryptoTestEnvironment> CreateAsync()
    {
        var env = new KubernetesCryptoTestEnvironment();
        PayloadProtectionAuthorityProfile authority = PayloadProtectionAuthorityProfile.DaprKubernetesCrypto;
        string now = PayloadProtectionStateCodec.Timestamp(env.Clock.GetUtcNow());
        var profile = new PayloadProtectionInstanceProfile(new('b', 40), new('c', 64), WrappedDekV2.ClosedBackendId, ["json", "json+pdenc-v2", "wrapped-dek-v2"], ["V2"]);
        await new PayloadProtectionCatalogs(env.Store, env.Clock).InitializeAsync(KubernetesCryptoTestEnvironment.Root);
        (await env.Store.CommitAsync([PayloadProtectionStateMutation.Put(new DeploymentGenerationV1("tenant-a", "parties", KubernetesCryptoTestEnvironment.Root, 1,
                authority.Digest, profile.SourceSha, profile.PackageSetSha256, "Open", now), authorityProfile: authority),
            PayloadProtectionStateMutation.Put(new LifecycleV1("tenant-a", "parties", "party-01", KubernetesCryptoTestEnvironment.Scope, "Active", 1, "none", "01J00000000000000000000820", now)),
            PayloadProtectionStateMutation.Put(new FenceV1("tenant-a", "parties", KubernetesCryptoTestEnvironment.Root, "V2", 7, "01J00000000000000000000819",
                authority.Digest, 1, "Legacy", 0, now), authorityProfile: authority)])).ShouldBe(PayloadStateCommitResult.Committed);
        env.Leases = new(env.Store, env.Roots, profile, env.Clock, authorityProfile: authority);
        await env.Leases.RegisterAsync(Identity);
        return env;
    }

    /// <summary>Creates a reservation coordinator for one backend phase.</summary>
    internal KeyReservationCoordinator Reservations(PayloadProtectionBackend backend)
        => new(Store, Roots, Leases, Erasure, backend, new(Clock), _ => true, "fixture-actor", timeProvider: Clock);

    /// <summary>Protects and confirms persistence through the same issuing coordinator.</summary>
    internal async ValueTask<(CoreProtectionResult Result, PayloadProtectionCompletionContext Completion)> WriteAsync(PayloadProtectionBackend backend, ulong sequence = 1)
    {
        KeyReservationCoordinator reservations = Reservations(backend);
        var write = await reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context(sequence));
        await reservations.AcquireCompletionAsync(write.Completion!);
        await reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        return (write.Result, write.Completion!);
    }

    /// <summary>Reads the exact persisted key record through the state boundary.</summary>
    internal async ValueTask<PayloadProtectionStateValue<WrappedDekV1>> KeyAsync(PayloadProtectionCompletionContext completion)
        => (await Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(Scope, completion.KeyReference, completion.DekVersion)))!;

    /// <summary>Asserts the immutable payload decrypts through the unchanged resolver with the given backend phase.</summary>
    internal async ValueTask AssertReadableAsync(PayloadProtectionBackend backend, byte[] payload)
    {
        // Clearing a scope blocks it like an invalidation, so every read uses its own fresh process cache.
        await using var cache = new PayloadKeyCache();
        var resolver = new PayloadKeyResolver(Store, Roots, Erasure, Leases, backend, new(Clock), cache, Clock);
        CoreUnprotectionResult result = await new PayloadProtectionCore().TryUnprotectEventAsync(payload, TestFixture.Context(),
            (key, version, ct) => resolver.ResolveAsync(Identity, key, version, new CryptographicPayloadProtectionEntropy().CreateKeyReference(), ct));
        try { result.UnreadableReason.ShouldBeNull(); result.PayloadBytes.ShouldBe(CompatibilityTestData.PlainJson()); }
        finally { if (result.PayloadBytes is not null) { CryptographicOperations.ZeroMemory(result.PayloadBytes); } }
        await cache.ClearScopeAsync(Scope);
    }
}
