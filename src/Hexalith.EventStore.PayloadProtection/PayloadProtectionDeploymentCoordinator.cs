// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 13; amendment sections 2-3.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Closes fleet generations, waits the full capability interval and preserves irreversible write watermarks.</summary>
internal sealed class PayloadProtectionDeploymentCoordinator(IPayloadProtectionStateStore store, PayloadProtectionScopeRegistry roots,
    TimeProvider? timeProvider = null, Func<TimeSpan, CancellationToken, ValueTask>? delay = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Closes an exact approved generation; successor admission starts its full wait only after strong confirmation.</summary>
    internal async ValueTask CloseAsync(AggregateIdentity identity, ulong expectedGeneration, CancellationToken token = default)
    {
        string root = roots.Require(identity);
        PayloadProtectionStateValue<DeploymentGenerationV1> current = await RequiredAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(root), token).ConfigureAwait(false);
        ValidateIdentity(current.Value, identity);
        if (current.Value.Generation != expectedGeneration) { throw Mismatch(); }
        if (current.Value.Status == "Closed") { return; }
        RequireCommit(await store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { Status = "Closed" }, current.ETag)], token).ConfigureAwait(false));
    }

    /// <summary>Opens a greater explicitly approved generation only after the confirmed full wait and exact indexed old-lease checks.</summary>
    internal async ValueTask OpenSuccessorAsync(AggregateIdentity identity, DeploymentGenerationV1 successor, CancellationToken token = default)
    {
        string root = roots.Require(identity);
        ValidateIdentity(successor, identity);
        if (successor.Status != "Open" || successor.ApprovedSpecDigest != PayloadProtectionStateKeys.AuthorityDigest) { throw Mismatch(); }
        PayloadProtectionStateValue<DeploymentGenerationV1> closed = await RequiredAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(root), token).ConfigureAwait(false);
        ValidateIdentity(closed.Value, identity);
        if (closed.Value == successor) { return; }
        if (closed.Value.Status != "Closed" || successor.Generation <= closed.Value.Generation) { throw Mismatch(); }
        await PayloadMonotonicDrain.WaitAsync(_clock, delay, token).ConfigureAwait(false);
        using var operationBudget = new PayloadOperationBudget(_clock, token);
        var catalogs = new PayloadProtectionCatalogs(store, _clock);
        MemberCatalogSnapshot members = await catalogs.ReadMembersAsync(root, token).ConfigureAwait(false);
        foreach (MemberEntryV1 member in members.Pages.SelectMany(p => p.Value.Entries).Where(e => e.Generation <= closed.Value.Generation && e.State != "Cleared"))
        {
            if (await PayloadOperationBudget.ReadAsync<CapabilityLeaseV1>(store, PayloadProtectionStateKeys.Capability(root, member.Generation, member.InstanceId), token).ConfigureAwait(false) is not null) { throw Mismatch(); }
        }
        RequireCommit(await store.CommitAsync([PayloadProtectionStateMutation.Put(successor, closed.ETag), PayloadProtectionStateMutation.Compare(members.Head)], token).ConfigureAwait(false));
    }

    /// <summary>Advances explicit operator authorization while keeping the highest-written observation unchanged.</summary>
    internal async ValueTask AdvanceFenceAsync(AggregateIdentity identity, FenceV1 approved, CancellationToken token = default)
    {
        string root = roots.Require(identity);
        PayloadProtectionStateValue<FenceV1> current = await RequiredAsync<FenceV1>(PayloadProtectionStateKeys.Fence(root), token).ConfigureAwait(false);
        if (current.Value == approved) { return; }
        PayloadProtectionStateValue<DeploymentGenerationV1> generation = await RequiredAsync<DeploymentGenerationV1>(PayloadProtectionStateKeys.Deployment(root), token).ConfigureAwait(false);
        ValidateIdentity(generation.Value, identity);
        if (generation.Value.Status != "Open" || generation.Value.ApprovedSpecDigest != PayloadProtectionStateKeys.AuthorityDigest
            || approved.TenantId != identity.TenantId || approved.Domain != identity.Domain || approved.TenantDomainDigest != root
            || current.Value.TenantId != identity.TenantId || current.Value.Domain != identity.Domain
            || approved.DeploymentGeneration != generation.Value.Generation || approved.ApprovedSpecDigest != PayloadProtectionStateKeys.AuthorityDigest
            || approved.ApprovalEpoch <= current.Value.ApprovalEpoch || approved.ApprovalId == current.Value.ApprovalId
            || approved.HighestWrittenFormat != current.Value.HighestWrittenFormat || approved.HighestWrittenEpoch != current.Value.HighestWrittenEpoch
            || (current.Value.HighestWrittenFormat == "V2" && approved.ApprovedWriteMode is "Disabled" or "CompatibilityLegacy")) { throw Mismatch(); }
        MemberCatalogSnapshot members = await new PayloadProtectionCatalogs(store, _clock).ReadMembersAsync(root, token).ConfigureAwait(false);
        MemberEntryV1[] serving = members.Pages.SelectMany(p => p.Value.Entries).Where(e => e.State == "Serving" && e.Generation == generation.Value.Generation).ToArray();
        if (serving.Length == 0) { throw Mismatch(); }
        foreach (MemberEntryV1 member in serving)
        {
            CapabilityLeaseV1 capability = (await RequiredAsync<CapabilityLeaseV1>(PayloadProtectionStateKeys.Capability(root, member.Generation, member.InstanceId), token).ConfigureAwait(false)).Value;
            if (capability.SourceSha != generation.Value.ApprovedSourceSha || capability.PackageSetSha256 != generation.Value.PackageSetSha256
                || capability.SpecDigest != generation.Value.ApprovedSpecDigest || !capability.ReadFormats.Contains("json+pdenc-v2", StringComparer.Ordinal)
                || !capability.WriteModes.Contains(approved.ApprovedWriteMode, StringComparer.Ordinal)
                || PayloadProtectionStateCodec.ParseTimestamp(capability.ExpiresAtUtc) <= _clock.GetUtcNow()) { throw Mismatch(); }
        }
        RequireCommit(await store.CommitAsync([PayloadProtectionStateMutation.Compare(generation), PayloadProtectionStateMutation.Compare(members.Head),
            .. members.Pages.Select(PayloadProtectionStateMutation.Compare), PayloadProtectionStateMutation.Put(approved, current.ETag)], token).ConfigureAwait(false));
    }
    private static void ValidateIdentity(DeploymentGenerationV1 value, AggregateIdentity identity)
    {
        if (value.TenantId != identity.TenantId || value.Domain != identity.Domain || value.TenantDomainDigest != PayloadProtectionStateKeys.TenantDomainDigest(identity.TenantId, identity.Domain)) { throw Mismatch(); }
    }
    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken token) where T : class => await PayloadOperationBudget.ReadAsync<T>(store, key, token).ConfigureAwait(false) ?? throw Mismatch();
    private static void RequireCommit(PayloadStateCommitResult result)
    {
        if (result != PayloadStateCommitResult.Committed) { throw new PayloadProtectionStateException(result == PayloadStateCommitResult.Denied ? UnreadableProtectedDataReason.ProviderDenied : result == PayloadStateCommitResult.Conflict ? UnreadableProtectedDataReason.ConsistencyMismatch : UnreadableProtectedDataReason.ProviderUnavailable); }
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
}
