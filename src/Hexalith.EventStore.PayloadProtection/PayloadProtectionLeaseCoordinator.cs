// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13 and 15; amendment sections 1-4.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>CAS-binds capability and operation leases to enrolled roots, serving members and current durable epochs.</summary>
internal sealed class PayloadProtectionLeaseCoordinator
{
    private readonly IPayloadProtectionStateStore _store;
    private readonly PayloadProtectionScopeRegistry _roots;
    private readonly PayloadProtectionInstanceProfile _profile;
    private readonly TimeProvider _clock;
    private readonly PayloadProtectionCatalogs _catalogs;
    private readonly Dictionary<string, long> _renewed = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Creates a fresh process-incarnation identity. Restarted coordinators never inherit another process's membership.</summary>
    internal PayloadProtectionLeaseCoordinator(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
        PayloadProtectionInstanceProfile profile, TimeProvider? timeProvider = null, IPayloadProtectionEntropy? entropy = null)
    {
        _store = store;
        _roots = roots;
        _profile = profile with { ReadFormats = profile.ReadFormats.ToArray(), WriteModes = profile.WriteModes.ToArray() };
        _clock = timeProvider ?? TimeProvider.System;
        _catalogs = new(store, _clock);
        InstanceId = (entropy ?? new CryptographicPayloadProtectionEntropy()).CreateKeyReference();
        if (!CanonicalUlid.IsValid(InstanceId)) { throw Mismatch(); }
    }

    /// <summary>Gets the fresh per-incarnation canonical member identity, confined to state coordination.</summary>
    internal string InstanceId { get; }

    /// <summary>Atomically appends permanent membership and creates its exact 60-second capability.</summary>
    internal async ValueTask RegisterAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        string root = _roots.Require(identity);
        long started = _clock.GetTimestamp();
        PayloadProtectionStateValue<DeploymentGenerationV1> generation = await DeploymentAsync(identity, cancellationToken).ConfigureAwait(false);
        MemberCatalogSnapshot members = await _catalogs.ReadMembersAsync(root, cancellationToken).ConfigureAwait(false);
        MemberEntryV1? existing = members.Pages.SelectMany(p => p.Value.Entries).SingleOrDefault(e => e.InstanceId == InstanceId);
        if (existing is not null) { throw Mismatch(); }
        CapabilityLeaseV1 capability = Capability(root, generation.Value.Generation);
        List<PayloadProtectionStateMutation> mutations = [.. _catalogs.AppendMember(members, generation.Value.Generation, InstanceId),
            PayloadProtectionStateMutation.Compare(generation), PayloadProtectionStateMutation.Put(capability)];
        PayloadStateCommitResult result = await _store.CommitAsync(mutations, cancellationToken).ConfigureAwait(false);
        // The same stable instance identity reconciles an unobserved atomic registration.
        if (result == PayloadStateCommitResult.Unknown)
        {
            members = await _catalogs.ReadMembersAsync(root, cancellationToken).ConfigureAwait(false);
            existing = members.Pages.SelectMany(p => p.Value.Entries).SingleOrDefault(e => e.InstanceId == InstanceId);
            PayloadProtectionStateValue<CapabilityLeaseV1>? found = await _store.ReadAsync<CapabilityLeaseV1>(PayloadProtectionStateCodec.KeyFor(capability), cancellationToken).ConfigureAwait(false);
            if (existing?.State != "Serving" || existing.Generation != generation.Value.Generation || found is null
                || !PayloadProtectionStateCodec.Encode(found.Value).AsSpan().SequenceEqual(PayloadProtectionStateCodec.Encode(capability))) { throw Unavailable(); }
        }
        else { RequireCommit(result); }
        if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(40)) { throw Unavailable(); }
        lock (_gate) { _renewed[root] = started; }
    }

    /// <summary>Renews only an unchanged open generation and serving member; no closed or draining authority can revive.</summary>
    internal async ValueTask RenewAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        string root = _roots.Require(identity);
        long started = _clock.GetTimestamp();
        PayloadProtectionStateValue<DeploymentGenerationV1> generation = await DeploymentAsync(identity, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<MemberPageV1> member = await ServingMemberAsync(root, generation.Value.Generation, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<CapabilityLeaseV1> previous = await RequiredAsync<CapabilityLeaseV1>(PayloadProtectionStateKeys.Capability(root, generation.Value.Generation, InstanceId), cancellationToken).ConfigureAwait(false);
        ValidateCapability(previous.Value, generation.Value);
        CapabilityLeaseV1 renewed = Capability(root, generation.Value.Generation);
        PayloadStateCommitResult result = await _store.CommitAsync([PayloadProtectionStateMutation.Compare(generation),
            PayloadProtectionStateMutation.Compare(member), PayloadProtectionStateMutation.Put(renewed, previous.ETag)], cancellationToken).ConfigureAwait(false);
        RequireCommit(result); // Uncertainty withholds traffic until a fresh confirmed renewal.
        if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(40)) { throw Unavailable(); }
        lock (_gate) { _renewed[root] = started; }
    }

    /// <summary>Returns immutable authority observations for an enclosing intent/reservation transaction.</summary>
    internal async ValueTask<IReadOnlyList<PayloadProtectionStateMutation>> AdmissionAsync(AggregateIdentity identity,
        LifecycleV1 lifecycle, bool writing, CancellationToken cancellationToken = default)
    {
        string root = _roots.Require(identity);
        AssertTraffic(root);
        if (lifecycle.ScopeDigest != PayloadProtectionStateKeys.ScopeDigest(identity) || lifecycle.TenantId != identity.TenantId
            || lifecycle.Domain != identity.Domain || lifecycle.AggregateId != identity.AggregateId) { throw Mismatch(); }
        if (lifecycle.State is not ("Active" or "Pending") || (writing && lifecycle.State != "Active"))
        {
            throw new PayloadProtectionStateException(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted);
        }
        PayloadProtectionStateValue<DeploymentGenerationV1> generation = await DeploymentAsync(identity, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<MemberPageV1> member = await ServingMemberAsync(root, generation.Value.Generation, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<CapabilityLeaseV1> capability = await RequiredAsync<CapabilityLeaseV1>(PayloadProtectionStateKeys.Capability(root, generation.Value.Generation, InstanceId), cancellationToken).ConfigureAwait(false);
        ValidateCapability(capability.Value, generation.Value);
        AssertTraffic(root);
        // Do not renew a lease as a side effect of comparison. The generation and serving page fence the transaction.
        return [PayloadProtectionStateMutation.Compare(generation), PayloadProtectionStateMutation.Compare(member)];
    }

    /// <summary>Admits a read only by CAS with the allowed lifecycle and serving member before cache access.</summary>
    internal async ValueTask<PayloadProtectionLease> AcquireReadAsync(AggregateIdentity identity, string keyRef, uint version,
        string operationId, PayloadProtectionStateValue<LifecycleV1> lifecycle, CancellationToken cancellationToken = default)
    {
        long started = _clock.GetTimestamp();
        IReadOnlyList<PayloadProtectionStateMutation> authority = await AdmissionAsync(identity, lifecycle.Value, false, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = _clock.GetUtcNow();
        var lease = new ReadLeaseV1(operationId, PayloadProtectionStateKeys.ScopeDigest(identity), keyRef, version, InstanceId,
            lifecycle.Value.Epoch, PayloadProtectionStateCodec.Timestamp(now), PayloadProtectionStateCodec.Timestamp(now.AddSeconds(45)));
        RequireCommit(await _store.CommitAsync([.. authority, PayloadProtectionStateMutation.Compare(lifecycle), PayloadProtectionStateMutation.Put(lease)], cancellationToken).ConfigureAwait(false));
        var admitted = new PayloadProtectionLease(_clock, started, TimeSpan.FromSeconds(45), lifecycle.Value.Epoch,
            () => ReleaseReadAsync(lease));
        try { admitted.AssertCurrent(cancellationToken); return admitted; }
        catch { await admitted.DisposeAsync().ConfigureAwait(false); throw; }
    }

    /// <summary>Admits an exact reservation-completion lease under current allowed authority before actor persistence.</summary>
    internal async ValueTask AcquireCompletionAsync(PayloadProtectionCompletionContext completion,
        PayloadProtectionStateValue<LifecycleV1> lifecycle, CancellationToken cancellationToken = default)
    {
        if (completion.LifecycleEpoch != lifecycle.Value.Epoch) { throw Mismatch(); }
        IReadOnlyList<PayloadProtectionStateMutation> authority = await AdmissionAsync(completion.Identity, lifecycle.Value, true, cancellationToken).ConfigureAwait(false);
        string scope = PayloadProtectionStateKeys.ScopeDigest(completion.Identity);
        PayloadProtectionStateValue<WrappedDekV1> key = await RequiredAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(scope, completion.KeyReference, completion.DekVersion), cancellationToken).ConfigureAwait(false);
        if (key.Value.State != "Reserved" || key.Value.CreatingOperationId != completion.OperationId || key.Value.LifecycleEpoch != completion.LifecycleEpoch) { throw Mismatch(); }
        DateTimeOffset now = _clock.GetUtcNow();
        var lease = new CompletionLeaseV1(completion.OperationId, scope, completion.KeyReference, completion.DekVersion, InstanceId,
            lifecycle.Value.Epoch, PayloadProtectionStateCodec.Timestamp(now), PayloadProtectionStateCodec.Timestamp(now.AddSeconds(60)));
        RequireCommit(await _store.CommitAsync([.. authority, PayloadProtectionStateMutation.Compare(lifecycle),
            PayloadProtectionStateMutation.Compare(key), PayloadProtectionStateMutation.Put(lease)], cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Begins graceful retirement before the full 60-second wait and explicit local clearing.</summary>
    internal async ValueTask RetireAsync(AggregateIdentity identity, Func<CancellationToken, ValueTask> clearLocal,
        CancellationToken cancellationToken = default)
    {
        string root = _roots.Require(identity);
        MemberCatalogSnapshot members = await _catalogs.ReadMembersAsync(root, cancellationToken).ConfigureAwait(false);
        MemberEntryV1 member = members.Pages.SelectMany(p => p.Value.Entries).Single(e => e.InstanceId == InstanceId);
        if (member.State == "Cleared") { return; }
        if (member.State == "Serving")
        {
            RequireCommit(await _store.CommitAsync(_catalogs.TransitionMember(members, InstanceId, "Draining"), cancellationToken).ConfigureAwait(false));
        }
        lock (_gate) { _renewed.Remove(root); }
        // Restart/uncertainty never reuses an old wall-clock timestamp to shorten the confirmed wait.
        long confirmed = _clock.GetTimestamp();
        await Task.Delay(TimeSpan.FromSeconds(60), _clock, cancellationToken).ConfigureAwait(false);
        if (_clock.GetElapsedTime(confirmed) < TimeSpan.FromSeconds(60)) { throw Unavailable(); }
        await clearLocal(cancellationToken).ConfigureAwait(false);
        members = await _catalogs.ReadMembersAsync(root, cancellationToken).ConfigureAwait(false);
        RequireCommit(await _store.CommitAsync(_catalogs.TransitionMember(members, InstanceId, "Cleared"), cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask ReleaseReadAsync(ReadLeaseV1 lease)
    {
        PayloadProtectionStateValue<ReadLeaseV1>? current = await _store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateCodec.KeyFor(lease)).ConfigureAwait(false);
        if (current is null) { return; }
        if (current.Value != lease) { throw Mismatch(); }
        // A failed release cannot prove erasure; the unconditional full drain still covers the maximum lease lifetime.
        _ = await _store.CommitAsync([new(PayloadProtectionStateCodec.KeyFor(lease), null, current.ETag)]).ConfigureAwait(false);
    }
    private async ValueTask<PayloadProtectionStateValue<DeploymentGenerationV1>> DeploymentAsync(AggregateIdentity identity, CancellationToken cancellationToken)
    {
        string root = _roots.Require(identity);
        PayloadProtectionStateValue<DeploymentGenerationV1> value = await RequiredAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(root), cancellationToken).ConfigureAwait(false);
        if (value.Value.TenantId != identity.TenantId || value.Value.Domain != identity.Domain || value.Value.TenantDomainDigest != root
            || value.Value.Status != "Open" || value.Value.ApprovedSpecDigest != PayloadProtectionStateKeys.AuthorityDigest
            || value.Value.ApprovedSourceSha != _profile.SourceSha || value.Value.PackageSetSha256 != _profile.PackageSetSha256) { throw Mismatch(); }
        return value;
    }
    private async ValueTask<PayloadProtectionStateValue<MemberPageV1>> ServingMemberAsync(string root, ulong generation, CancellationToken cancellationToken)
    {
        MemberCatalogSnapshot members = await _catalogs.ReadMembersAsync(root, cancellationToken).ConfigureAwait(false);
        PayloadProtectionStateValue<MemberPageV1> page = members.Pages.SingleOrDefault(p => p.Value.Entries.Any(e => e.InstanceId == InstanceId)) ?? throw Mismatch();
        MemberEntryV1 member = page.Value.Entries.Single(e => e.InstanceId == InstanceId);
        if (member.State != "Serving" || member.Generation != generation) { throw Mismatch(); }
        return page;
    }
    private CapabilityLeaseV1 Capability(string root, ulong generation)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        return new(root, generation, InstanceId, _profile.SourceSha, _profile.PackageSetSha256, PayloadProtectionStateKeys.AuthorityDigest,
            _profile.BackendId, _profile.ReadFormats.ToArray(), _profile.WriteModes.ToArray(), PayloadProtectionStateCodec.Timestamp(now), PayloadProtectionStateCodec.Timestamp(now.AddSeconds(60)));
    }
    private void ValidateCapability(CapabilityLeaseV1 value, DeploymentGenerationV1 generation)
    {
        if (value.TenantDomainDigest != generation.TenantDomainDigest || value.Generation != generation.Generation || value.InstanceId != InstanceId
            || value.SourceSha != _profile.SourceSha || value.PackageSetSha256 != _profile.PackageSetSha256 || value.SpecDigest != PayloadProtectionStateKeys.AuthorityDigest
            || value.BackendId != _profile.BackendId || !value.ReadFormats.SequenceEqual(_profile.ReadFormats) || !value.WriteModes.SequenceEqual(_profile.WriteModes)
            || PayloadProtectionStateCodec.ParseTimestamp(value.ExpiresAtUtc) <= _clock.GetUtcNow()) { throw Mismatch(); }
    }
    private void AssertTraffic(string root)
    {
        lock (_gate)
        {
            if (!_renewed.TryGetValue(root, out long timestamp) || _clock.GetElapsedTime(timestamp) >= TimeSpan.FromSeconds(40)) { throw Unavailable(); }
        }
    }
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => await _store.ReadAsync<T>(key, cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
    private static void RequireCommit(PayloadStateCommitResult result)
    {
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied
            ? UnreadableProtectedDataReason.ProviderDenied : result == PayloadStateCommitResult.Conflict ? UnreadableProtectedDataReason.ConsistencyMismatch : UnreadableProtectedDataReason.ProviderUnavailable); }
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
    private static PayloadProtectionStateException Unavailable() => new(UnreadableProtectedDataReason.ProviderUnavailable);
}
