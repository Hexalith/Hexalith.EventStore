// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 5-7; separately authorized isolated environment required.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
internal sealed class AzureLiveEnvironment : IAsyncDisposable
{
    internal required AzureDaprFixture Fixture { get; init; }
    internal required AggregateIdentity Identity { get; init; }
    internal required PayloadProtectionScopeRegistry Roots { get; init; }
    internal required AzureLiveBackend Backend { get; init; }
    internal required PayloadProtectionInstanceProfile Profile { get; init; }
    internal PayloadProtectionLeaseCoordinator Leases { get; private set; } = null!;
    internal KeyReservationCoordinator Reservations { get; private set; } = null!;
    internal PayloadKeyCache Cache { get; private set; } = new();
    internal string Root => PayloadProtectionStateKeys.TenantDomainDigest(Identity.TenantId, Identity.Domain);
    internal string Scope => PayloadProtectionStateKeys.ScopeDigest(Identity);

    internal static async Task<AzureLiveEnvironment> CreateAsync(AzureDaprFixture fixture, string test, CancellationToken token)
    {
        await new PayloadProtectionCapabilityProbe(fixture.Store).ProbeAsync(token);
        var identity = new AggregateIdentity("azure-" + test, "qualification", "aggregate-01");
        var real = fixture.Authority.Backend();
        (await new AzureKeyVaultReadinessProbe(real).ProbeAsync(token)).Ready.ShouldBeTrue();
        ((await real.RefreshAsync(token)).Id == fixture.Authority.RotationVersion).ShouldBeTrue();
        var backend = new AzureLiveBackend(real, fixture.Authority.InitialVersion);
        var env = new AzureLiveEnvironment
        {
            Fixture = fixture,
            Identity = identity,
            Roots = new([new(identity.TenantId, identity.Domain)]),
            Backend = backend,
            Profile = new(fixture.Authority.SourceSha, PayloadProtectionStateKeys.Digest("isolated-source-conformance"), backend.BackendId, ["json", "json+pdenc-v2", "json-redacted"], ["V2", "ReadOnly"])
        };
        string now = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        (await new PayloadProtectionCatalogs(fixture.Store).InitializeAsync(env.Root, token)).ShouldBe(PayloadStateCommitResult.Committed);
        (await fixture.Store.CommitAsync([PayloadProtectionStateMutation.Put(new LifecycleV1(identity.TenantId, identity.Domain, identity.AggregateId, env.Scope, "Active", 1, "none", "01J00000000000000000000860", now)), PayloadProtectionStateMutation.Put(new DeploymentGenerationV1(identity.TenantId, identity.Domain, env.Root, 1, PayloadProtectionStateKeys.AuthorityDigest, env.Profile.SourceSha, env.Profile.PackageSetSha256, "Open", now)), PayloadProtectionStateMutation.Put(new FenceV1(identity.TenantId, identity.Domain, env.Root, "V2", 1, "01J00000000000000000000860", PayloadProtectionStateKeys.AuthorityDigest, 1, "Legacy", 0, now))], token)).ShouldBe(PayloadStateCommitResult.Committed);
        await env.RestartAsync(token);
        return env;
    }

    internal async Task RestartAsync(CancellationToken token)
    {
        await Cache.DisposeAsync();
        Cache = new();
        Leases = new(Fixture.Store, Roots, Profile);
        await Leases.RegisterAsync(Identity, token);
        Reservations = new(Fixture.Store, Roots, Leases, new(Roots, [new("qualification", new AzureLiveErasure())]), Backend, new(), _ => true, "authorized-qualification");
    }

    internal PayloadKeyResolver Resolver(PayloadProtectionBackend? backend = null) => new(Fixture.Store, Roots, new(Roots, [new("qualification", new AzureLiveErasure())]), Leases, backend ?? Backend, new(), Cache);
    internal PayloadProtectionContext Context(ulong sequence = 1) => new(Identity, "azure-qualification-event", PayloadProtectionPayloadKind.Event, sequence);
    internal async Task<WrappedDekV1> KeyAsync(PayloadProtectionCompletionContext completion, CancellationToken token) => (await Fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(Scope, completion.KeyReference, completion.DekVersion), token))!.Value;
    internal async Task AssertReadableAsync(byte[] bytes, CancellationToken token)
    {
        var resolver = Resolver();
        var read = await new PayloadProtectionCore().TryUnprotectEventAsync(bytes, Context(), (key, version, ct) => resolver.ResolveAsync(Identity, key, version, new CryptographicPayloadProtectionEntropy().CreateKeyReference(), ct), token);
        read.IsReadable.ShouldBeTrue();
        read.PayloadBytes!.AsSpan().SequenceEqual("{\"secret\":\"authorized-live-canary\"}"u8).ShouldBeTrue();
        CryptographicOperations.ZeroMemory(read.PayloadBytes!);
    }

    public async ValueTask DisposeAsync() => await Cache.DisposeAsync();
}
