using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Creates isolated root authority through real Dapr transactions for lifecycle qualification.</summary>
internal sealed class LifecycleQualificationEnvironment : IAsyncDisposable
{
    /// <summary>Gets the exact state primitive.</summary>
    internal DaprPayloadProtectionStateStore Store { get; private init; } = null!;
    /// <summary>Gets the isolated identity.</summary>
    internal AggregateIdentity Identity { get; private init; } = null!;
    /// <summary>Gets the explicitly enrolled root.</summary>
    internal PayloadProtectionScopeRegistry Roots { get; private init; } = null!;
    /// <summary>Gets the ephemeral wrapping fixture.</summary>
    internal LifecycleQualificationBackend Backend { get; private init; } = null!;
    /// <summary>Gets registered process authority.</summary>
    internal PayloadProtectionLeaseCoordinator Leases { get; private set; } = null!;
    /// <summary>Gets the reservation engine.</summary>
    internal KeyReservationCoordinator Reservations { get; private set; } = null!;
    /// <summary>Gets the owned process cache.</summary>
    internal PayloadKeyCache Cache { get; } = new();
    /// <summary>Gets the exact root address.</summary>
    internal string Root => PayloadProtectionStateKeys.TenantDomainDigest(Identity.TenantId, Identity.Domain);
    /// <summary>Gets the exact scope address.</summary>
    internal string Scope => PayloadProtectionStateKeys.ScopeDigest(Identity);
    /// <summary>Gets the test deployment's immutable capability profile.</summary>
    internal PayloadProtectionInstanceProfile Profile => new(new('b', 40), new('c', 64), Backend.BackendId, ["json", "json+pdenc-v2"], ["V2"]);
    /// <summary>Gets a fresh lifecycle owner.</summary>
    internal KeyLifecycleCoordinator Lifecycle => new(Store, Roots, Leases, Cache, Backend.BackendId, _ => true, "qualification-actor");
    /// <summary>Creates all required authority explicitly; no scan or implicit enrollment occurs.</summary>
    internal static async ValueTask<LifecycleQualificationEnvironment> CreateAsync(DaprPayloadProtectionStateStore store, string tenant, CancellationToken token, LifecycleQualificationBackend? backend = null, PayloadProtectionScopeRegistry? roots = null)
    {
        await new PayloadProtectionCapabilityProbe(store).ProbeAsync(token);
        var env = new LifecycleQualificationEnvironment { Store = store, Backend = backend ?? new(tenant), Identity = new(tenant, "qualification", "aggregate-01"), Roots = roots ?? new([new(tenant, "qualification")]) };
        string now = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        (await new PayloadProtectionCatalogs(store).InitializeAsync(env.Root, token)).ShouldBe(PayloadStateCommitResult.Committed);
        const string initialOperation = "01J00000000000000000000501";
        (await store.CommitAsync([PayloadProtectionStateMutation.Put(new LifecycleV1(tenant, "qualification", env.Identity.AggregateId, env.Scope, "Active", 1, "none", initialOperation, now)),
            PayloadProtectionStateMutation.Put(new DeploymentGenerationV1(tenant, "qualification", env.Root, 1, PayloadProtectionStateKeys.AuthorityDigest, env.Profile.SourceSha, env.Profile.PackageSetSha256, "Open", now)),
            PayloadProtectionStateMutation.Put(new FenceV1(tenant, "qualification", env.Root, "V2", 1, initialOperation, PayloadProtectionStateKeys.AuthorityDigest, 1, "Legacy", 0, now))], token)).ShouldBe(PayloadStateCommitResult.Committed);
        env.Leases = new(store, env.Roots, env.Profile);
        await env.Leases.RegisterAsync(env.Identity, token);
        env.Reservations = new(store, env.Roots, env.Leases, new(env.Roots, [new("qualification", new LifecycleQualificationErasure())]), env.Backend, new(), _ => true, "qualification-actor");
        return env;
    }
    /// <summary>Clears the owned process cache and fixture KEKs.</summary>
    public async ValueTask DisposeAsync() { await Cache.DisposeAsync(); Backend.Dispose(); }
}
