using System.Security.Cryptography;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>The actor-owned tenant/domain roster used only after independent installation coverage approval.</summary>
/// <param name="TenantId">The canonical tenant identifier.</param>
/// <param name="Domain">The canonical domain identifier.</param>
public sealed record SharedProjectionSourceScope(string TenantId, string Domain)
{
    /// <summary>Gets the fixed private actor address for this tenant and domain.</summary>
    public string ActorId => new AggregateIdentity(TenantId, Domain, "shared-projection-source-roster").ActorId;
}

/// <summary>A separately approved clean or legacy inventory installation.</summary>
/// <param name="Scope">The tenant and domain receiving coverage.</param>
/// <param name="CoverageReceipt">Independent proof of complete source coverage.</param>
/// <param name="WriterEnforcementReceipt">Independent proof that all writers enforce the source fence.</param>
/// <param name="IsCleanInstall">Whether the source store was certified empty at installation.</param>
/// <param name="InitialOwners">The complete certified legacy owner census, or an empty clean-install list.</param>
public sealed record SharedProjectionSourceInstallation(
    SharedProjectionSourceScope Scope, string CoverageReceipt, string WriterEnforcementReceipt,
    bool IsCleanInstall, IReadOnlyList<AggregateIdentity> InitialOwners);

/// <summary>Independent installation and internal operation authority; absent authority denies every operation.</summary>
public interface IInstallationCoverageAuthority
{
    /// <summary>Approves independently certified complete legacy coverage or a truly virgin installation.</summary>
    Task<bool> AuthorizeInstallationAsync(SharedProjectionSourceInstallation installation);

    /// <summary>Approves an EventStore aggregate write against a currently enforced installation.</summary>
    Task<bool> AuthorizeWriteAsync(SharedProjectionSourceScope scope, AggregateIdentity identity, string intentId);

    /// <summary>Approves a shared rebuild capture against the currently enforced installation.</summary>
    Task<bool> AuthorizeCaptureAsync(SharedProjectionSourceScope scope, string operationId);
}

/// <summary>The private actor contract for partitioned owner registration and a held source cut.</summary>
public interface ISharedProjectionSourceRosterActor : IActor
{
    /// <summary>Installs independently certified coverage before any captured rebuild.</summary>
    Task<bool> InstallAsync(SharedProjectionSourceInstallation installation);

    /// <summary>Registers an owner and durable in-flight intent before event persistence.</summary>
    Task<bool> BeginWriteAsync(AggregateIdentity identity, string intentId, bool isErasure);

    /// <summary>Settles an intent only after the source actor resolves event persistence.</summary>
    Task<bool> CompleteWriteAsync(AggregateIdentity identity, string intentId);

    /// <summary>Lists bounded unresolved writes for actor-head reconciliation before capture.</summary>
    Task<IReadOnlyList<SharedProjectionSourcePendingWrite>?> ReadPendingWritesAsync(
        SharedProjectionSourceScope scope, string operationId);

    /// <summary>Freezes writes once every prior intent is settled.</summary>
    Task<long?> BeginCaptureAsync(SharedProjectionSourceScope scope, string operationId);

    /// <summary>Reads one bounded owner partition while the same capture remains held.</summary>
    Task<SharedProjectionSourceRosterPage?> ReadPageAsync(SharedProjectionSourceScope scope, string operationId, int partition);

    /// <summary>Persists one source-actor-grounded partition of the frozen owner cut.</summary>
    Task<bool> WriteCapturedPageAsync(SharedProjectionSourceScope scope, string operationId, int partition,
        IReadOnlyList<SharedProjectionCapturedSourceOwner> owners);

    /// <summary>Seals all captured owner pages before sending SDK Begin.</summary>
    Task<bool> SealCaptureAsync(SharedProjectionSourceScope scope, string operationId, string fingerprint,
        string projectionType, string catalogFingerprint);

    /// <summary>Aborts only an unsealed cut and removes all private captured pages.</summary>
    Task<bool> AbortUnsealedCaptureAsync(SharedProjectionSourceScope scope, string operationId);

    /// <summary>Aborts a sealed cut only after the SDK proves no admitted Begin can remain.</summary>
    Task<bool> AbortRejectedCaptureAsync(SharedProjectionSourceScope scope, string operationId);

    /// <summary>Reads the durable recovery stage for one source cut.</summary>
    Task<SharedProjectionSourceCaptureProgress?> ReadCaptureProgressAsync(SharedProjectionSourceScope scope,
        string operationId);

    /// <summary>Reads one sealed immutable owner/head partition.</summary>
    Task<SharedProjectionCapturedSourcePage?> ReadCapturedPageAsync(SharedProjectionSourceScope scope,
        string operationId, int partition);

    /// <summary>Releases ordinary appends after SDK Begin while retaining the erasure hold.</summary>
    Task<bool> ReleaseAppendFenceAsync(SharedProjectionSourceScope scope, string operationId);

    /// <summary>Releases the erasure hold after every owner manifest is durably accumulated.</summary>
    Task<bool> MarkOwnersAccumulatedAsync(SharedProjectionSourceScope scope, string operationId);

    /// <summary>Closes the capture after durable SDK Commit.</summary>
    Task<bool> CompleteCaptureAsync(SharedProjectionSourceScope scope, string operationId);
}

/// <summary>A bounded partition of the frozen authoritative owner roster.</summary>
/// <param name="Revision">The roster revision held by the capture.</param>
/// <param name="Owners">The owners in this partition.</param>
public sealed record SharedProjectionSourceRosterPage(long Revision, IReadOnlyList<AggregateIdentity> Owners);

/// <summary>One actor-owned source head and completed-erasure marker at the held cut.</summary>
/// <param name="Owner">The canonical aggregate owner.</param>
/// <param name="Sequence">The captured aggregate event sequence.</param>
/// <param name="Position">The captured global event position.</param>
/// <param name="IsErased">Whether completed actor erasure removed the source history.</param>
/// <param name="HistoryFingerprint">The exact raw history identity captured under the append fence.</param>
public sealed record SharedProjectionCapturedSourceOwner(AggregateIdentity Owner, long Sequence, long Position,
    bool IsErased, string? HistoryFingerprint = null);

/// <summary>One bounded durable partition of the sealed source cut.</summary>
/// <param name="CaptureRevision">The roster revision frozen for this capture.</param>
/// <param name="Owners">The captured owner heads in this partition.</param>
public sealed record SharedProjectionCapturedSourcePage(long CaptureRevision,
    IReadOnlyList<SharedProjectionCapturedSourceOwner> Owners);

/// <summary>Persistent restart information for the capture lifecycle.</summary>
/// <param name="CaptureRevision">The roster revision frozen for this capture.</param>
/// <param name="Sealed">Whether the owner pages and fingerprint were sealed.</param>
/// <param name="AppendFenceHeld">Whether ordinary appends remain frozen.</param>
/// <param name="OwnersAccumulated">Whether every captured owner manifest was accepted.</param>
/// <param name="Fingerprint">The sealed inventory fingerprint.</param>
/// <param name="OwnerCount">The frozen owner count.</param>
/// <param name="Completed">Whether the rebuild committed and the capture closed.</param>
public sealed record SharedProjectionSourceCaptureProgress(long CaptureRevision, bool Sealed,
    bool AppendFenceHeld, bool OwnersAccumulated, string? Fingerprint, int OwnerCount, bool Completed,
    int PageCount = 64);

/// <summary>Durable tenant/domain roster, fence, and capture recovery state.</summary>
internal sealed record SharedProjectionSourceRosterState(
    long Revision, string CoverageReceipt, string WriterEnforcementReceipt,
    bool IsCleanInstall, string? CaptureOperationId, IReadOnlyList<SharedProjectionWriteIntent> PendingIntents,
    int OwnerCount, string InitialOwnersDigest, long CaptureRevision = 0, bool AppendFenceHeld = false,
    bool CaptureSealed = false, string? CaptureFingerprint = null, bool OwnersAccumulated = false,
    string? LastCompletedOperationId = null, int CaptureOwnerCount = 0,
    int PageCount = 64, int CapturePageCount = 64,
    string? CaptureProjectionType = null, string? CaptureCatalogFingerprint = null,
    string? LastRejectedOperationId = null);

/// <summary>One unresolved source persistence attempt held ahead of capture.</summary>
internal sealed record SharedProjectionWriteIntent(string IntentId, AggregateIdentity Identity);

/// <summary>One unresolved owner write whose actor head must be read before capture can proceed.</summary>
/// <param name="IntentId">The durable write intent identifier.</param>
/// <param name="Identity">The aggregate owner whose durable head must be observed.</param>
public sealed record SharedProjectionSourcePendingWrite(string IntentId, AggregateIdentity Identity);

/// <summary>Serializes source writes and capture within one tenant/domain Dapr actor.</summary>
public sealed class SharedProjectionSourceRosterActor(ActorHost host,
    IInstallationCoverageAuthority? authority = null,
    ISharedProjectionRebuildGateway? rebuildGateway = null) : Actor(host), ISharedProjectionSourceRosterActor
{
    private const string RootKey = "shared-source-roster-v1";
    private const int PartitionCount = 64;
    private const int MaxOwnersPerPartition = 1000;
    private const int MaxOwners = 10_000;
    private const int MaxPendingIntents = 1000;

    /// <summary>Gets the private registered actor type name.</summary>
    public const string ActorTypeName = "SharedProjectionSourceRosterActor";

    /// <inheritdoc/>
    public async Task<bool> InstallAsync(SharedProjectionSourceInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        CheckScope(installation.Scope);
        if (authority is null || !await authority.AuthorizeInstallationAsync(installation).ConfigureAwait(false)) return false;
        if (string.IsNullOrWhiteSpace(installation.CoverageReceipt)
            || string.IsNullOrWhiteSpace(installation.WriterEnforcementReceipt)
            || installation.InitialOwners is null || installation.InitialOwners.Count > MaxOwners
            || installation.IsCleanInstall && installation.InitialOwners.Count != 0)
            return false;

        AggregateIdentity[] owners = [.. installation.InitialOwners];
        if (owners.Any(owner => owner is null || owner.TenantId != installation.Scope.TenantId
            || owner.Domain != installation.Scope.Domain) || owners.Distinct().Count() != owners.Length)
            return false;
        string initialDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\n', owners.OrderBy(owner => owner.ActorId, StringComparer.Ordinal)
                .Select(owner => owner.ActorId)))));
        SharedProjectionSourceRosterState? current = await ReadRootAsync().ConfigureAwait(false);
        if (current is not null)
            return current.Revision == 1 && current.CoverageReceipt == installation.CoverageReceipt
                && current.WriterEnforcementReceipt == installation.WriterEnforcementReceipt
                && current.IsCleanInstall == installation.IsCleanInstall
                && current.InitialOwnersDigest == initialDigest
                && await authority.AuthorizeInstallationAsync(installation).ConfigureAwait(false);

        var pages = new List<List<AggregateIdentity>>(PartitionCount);
        for (int partition = 0; partition < PartitionCount; partition++) pages.Add([]);
        foreach (AggregateIdentity owner in owners.OrderBy(item => item.ActorId, StringComparer.Ordinal))
        {
            List<AggregateIdentity> page = pages[Partition(owner)];
            if (page.Count == MaxOwnersPerPartition)
            {
                page = pages.Skip(PartitionCount).FirstOrDefault(item => item.Count < MaxOwnersPerPartition) ?? [];
                if (!pages.Contains(page)) pages.Add(page);
            }

            page.Add(owner);
        }
        for (int partition = 0; partition < pages.Count; partition++)
        {
            await StateManager.SetStateAsync(PageKey(partition), pages[partition].ToArray()).ConfigureAwait(false);
        }
        await StateManager.SetStateAsync(RootKey, new SharedProjectionSourceRosterState(
            1, installation.CoverageReceipt, installation.WriterEnforcementReceipt,
            installation.IsCleanInstall, null, [], owners.Length, initialDigest,
            PageCount: pages.Count)).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return await ReadRootAsync().ConfigureAwait(false) is not null
            && await authority.AuthorizeInstallationAsync(installation).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> BeginWriteAsync(AggregateIdentity identity, string intentId, bool isErasure)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        CheckScope(scope);
        if (string.IsNullOrWhiteSpace(intentId) || intentId.Length > 256 || authority is null
            || !await authority.AuthorizeWriteAsync(scope, identity, intentId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root is null || root.CaptureOperationId is not null
            && (root.AppendFenceHeld || isErasure && !root.OwnersAccumulated)) return false;
        SharedProjectionWriteIntent? pending = root.PendingIntents.FirstOrDefault(item => item.IntentId == intentId);
        if (pending is not null)
            return pending.Identity == identity
                && await ContainsOwnerAsync(identity, root.PageCount).ConfigureAwait(false)
                && await authority.AuthorizeWriteAsync(scope, identity, intentId).ConfigureAwait(false);
        if (root.PendingIntents.Count >= MaxPendingIntents) return false;
        int partition = Partition(identity);
        AggregateIdentity[] owners = await ReadPageContentsAsync(partition).ConfigureAwait(false);
        bool isNewOwner = !await ContainsOwnerAsync(identity, root.PageCount).ConfigureAwait(false);
        if (isNewOwner)
        {
            if (root.OwnerCount >= MaxOwners) return false;
            if (owners.Length >= MaxOwnersPerPartition)
            {
                partition = -1;
                for (int spill = PartitionCount; spill < root.PageCount; spill++)
                {
                    AggregateIdentity[] candidate = await ReadPageContentsAsync(spill).ConfigureAwait(false);
                    if (candidate.Length < MaxOwnersPerPartition)
                    {
                        partition = spill;
                        owners = candidate;
                        break;
                    }
                }

                if (partition < 0)
                {
                    partition = Math.Max(PartitionCount, root.PageCount);
                    owners = [];
                }
            }

            await StateManager.SetStateAsync(PageKey(partition), owners.Append(identity)
                .OrderBy(owner => owner.ActorId, StringComparer.Ordinal).ToArray()).ConfigureAwait(false);
        }
        await StateManager.SetStateAsync(RootKey, root with {
            Revision = checked(root.Revision + 1), PendingIntents = [.. root.PendingIntents,
                new SharedProjectionWriteIntent(intentId, identity)],
            OwnerCount = root.OwnerCount + (isNewOwner ? 1 : 0),
            PageCount = Math.Max(root.PageCount, partition + 1)
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        SharedProjectionSourceRosterState? saved = await ReadRootAsync().ConfigureAwait(false);
        return saved?.PendingIntents.Any(item => item.IntentId == intentId && item.Identity == identity) == true
            && await ContainsOwnerAsync(identity, saved.PageCount).ConfigureAwait(false)
            && await authority.AuthorizeWriteAsync(scope, identity, intentId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteWriteAsync(AggregateIdentity identity, string intentId)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        CheckScope(scope);
        if (string.IsNullOrWhiteSpace(intentId) || authority is null
            || !await authority.AuthorizeWriteAsync(scope, identity, intentId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root is null) return false;
        SharedProjectionWriteIntent? pending = root.PendingIntents.FirstOrDefault(item => item.IntentId == intentId);
        if (pending is null) return true;
        if (pending.Identity != identity) return false;
        await StateManager.SetStateAsync(RootKey, root with {
            Revision = checked(root.Revision + 1),
            PendingIntents = [.. root.PendingIntents.Where(item => item.IntentId != intentId)]
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return (await ReadRootAsync().ConfigureAwait(false))?.PendingIntents.All(item => item.IntentId != intentId) == true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SharedProjectionSourcePendingWrite>?> ReadPendingWritesAsync(
        SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (string.IsNullOrWhiteSpace(operationId) || authority is null
            || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return null;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root is null || root.CaptureOperationId is not null && root.CaptureOperationId != operationId) return null;
        return [.. root.PendingIntents.Select(static item =>
            new SharedProjectionSourcePendingWrite(item.IntentId, item.Identity))];
    }

    /// <inheritdoc/>
    public async Task<long?> BeginCaptureAsync(SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 256 || authority is null
            || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return null;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root is null || root.CaptureOperationId is not null && root.CaptureOperationId != operationId) return null;
        if (root.CaptureOperationId == operationId)
            return root.PendingIntents.Count == 0 ? root.CaptureRevision : null;
        if (root.LastCompletedOperationId == operationId || root.LastRejectedOperationId == operationId) return null;
        await StateManager.SetStateAsync(RootKey, root with {
            Revision = checked(root.Revision + 1), CaptureOperationId = operationId,
            CaptureRevision = checked(root.Revision + 1), AppendFenceHeld = true,
            CaptureSealed = false, CaptureFingerprint = null, OwnersAccumulated = false,
            CaptureOwnerCount = root.OwnerCount, CapturePageCount = Math.Max(PartitionCount, root.PageCount)
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        SharedProjectionSourceRosterState? saved = await ReadRootAsync().ConfigureAwait(false);
        return saved?.CaptureOperationId == operationId && saved.PendingIntents.Count == 0
            && await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)
                ? saved.CaptureRevision : null;
    }

    /// <inheritdoc/>
    public async Task<SharedProjectionSourceRosterPage?> ReadPageAsync(
        SharedProjectionSourceScope scope, string operationId, int partition)
    {
        CheckScope(scope);
        if (partition < 0 || authority is null
            || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return null;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId != operationId || !root.AppendFenceHeld
            || root.PendingIntents.Count != 0 || partition >= root.CapturePageCount) return null;
        AggregateIdentity[] owners = await ReadPageContentsAsync(partition).ConfigureAwait(false);
        return (await ReadRootAsync().ConfigureAwait(false))?.CaptureOperationId == operationId
            ? new(root.CaptureRevision, owners) : null;
    }

    /// <inheritdoc/>
    public async Task<bool> WriteCapturedPageAsync(SharedProjectionSourceScope scope, string operationId,
        int partition, IReadOnlyList<SharedProjectionCapturedSourceOwner> owners)
    {
        CheckScope(scope);
        if (partition < 0 || owners is null || owners.Count > MaxOwnersPerPartition
            || authority is null || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false))
            return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId != operationId || !root.AppendFenceHeld || root.CaptureSealed
            || root.PendingIntents.Count != 0 || partition >= root.CapturePageCount) return false;
        AggregateIdentity[] roster = await ReadPageContentsAsync(partition).ConfigureAwait(false);
        if (!roster.SequenceEqual(owners.Select(item => item.Owner))
            || owners.Any(item => item.Sequence < 0 || item.Position < 0
                || item.IsErased && item.Sequence != 0
                || !item.IsErased && (item.Sequence == 0) != (item.Position == 0)))
            return false;
        SharedProjectionCapturedSourcePage page = new(root.CaptureRevision, [.. owners]);
        await StateManager.SetStateAsync(CapturePageKey(partition), page).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        SharedProjectionCapturedSourcePage? saved = await ReadCapturePageStateAsync(partition).ConfigureAwait(false);
        return saved?.CaptureRevision == root.CaptureRevision
            && saved.Owners.SequenceEqual(owners)
            && (await ReadRootAsync().ConfigureAwait(false))?.CaptureOperationId == operationId;
    }

    /// <inheritdoc/>
    public async Task<bool> SealCaptureAsync(SharedProjectionSourceScope scope, string operationId, string fingerprint,
        string projectionType, string catalogFingerprint)
    {
        CheckScope(scope);
        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(projectionType)
            || string.IsNullOrWhiteSpace(catalogFingerprint) || authority is null
            || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId != operationId || !root.AppendFenceHeld) return false;
        if (root.CaptureSealed) return root.CaptureFingerprint == fingerprint
            && root.CaptureProjectionType == projectionType
            && root.CaptureCatalogFingerprint == catalogFingerprint;
        int count = 0;
        var seen = new HashSet<AggregateIdentity>();
        for (int partition = 0; partition < root.CapturePageCount; partition++)
        {
            SharedProjectionCapturedSourcePage? page = await ReadCapturePageStateAsync(partition).ConfigureAwait(false);
            if (page?.CaptureRevision != root.CaptureRevision
                || page.Owners.Any(item => !seen.Add(item.Owner))) return false;
            count = checked(count + page.Owners.Count);
        }
        if (count != root.CaptureOwnerCount) return false;
        await StateManager.SetStateAsync(RootKey, root with {
            Revision = checked(root.Revision + 1), CaptureSealed = true, CaptureFingerprint = fingerprint,
            CaptureProjectionType = projectionType, CaptureCatalogFingerprint = catalogFingerprint
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        SharedProjectionSourceRosterState? saved = await ReadRootAsync().ConfigureAwait(false);
        return saved?.CaptureOperationId == operationId && saved.CaptureSealed
            && saved.CaptureFingerprint == fingerprint;
    }

    /// <inheritdoc/>
    public async Task<bool> AbortUnsealedCaptureAsync(SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (string.IsNullOrWhiteSpace(operationId) || authority is null
            || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId is null) return true;
        if (root.CaptureOperationId != operationId || root.CaptureSealed || !root.AppendFenceHeld) return false;
        return await AbortUnsealedOrRejectedAsync(root).ConfigureAwait(false);
    }

    private async Task<bool> AbortUnsealedOrRejectedAsync(SharedProjectionSourceRosterState root,
        bool sealedRejected = false)
    {
        for (int partition = 0; partition < root.CapturePageCount; partition++)
        {
            string key = CapturePageKey(partition);
            if (await StateManager.ContainsStateAsync(key).ConfigureAwait(false))
                await StateManager.RemoveStateAsync(key).ConfigureAwait(false);
        }

        await StateManager.SetStateAsync(RootKey, root with
        {
            Revision = checked(root.Revision + 1), CaptureOperationId = null,
            AppendFenceHeld = false, CaptureRevision = 0, CaptureOwnerCount = 0,
            CapturePageCount = Math.Max(PartitionCount, root.PageCount),
            CaptureSealed = false, CaptureFingerprint = null, OwnersAccumulated = false,
            CaptureProjectionType = null, CaptureCatalogFingerprint = null,
            LastRejectedOperationId = sealedRejected ? root.CaptureOperationId : root.LastRejectedOperationId
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return (await ReadRootAsync().ConfigureAwait(false))?.CaptureOperationId is null;
    }

    /// <inheritdoc/>
    public async Task<bool> AbortRejectedCaptureAsync(SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (authority is null || rebuildGateway is null
            || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId != operationId || !root.CaptureSealed || !root.AppendFenceHeld)
            return false;
        DomainSharedProjectionRebuildResponse? receipt = await ReadDurablePhaseAsync(root, scope, operationId)
            .ConfigureAwait(false);
        if (receipt?.Status != ProjectionDispatchStatus.Retryable
            || receipt.ReasonCode != ProjectionDispatchReasonCodes.DeliveryGap)
            return false;
        DomainSharedProjectionRebuildIdentity identity = CaptureIdentity(root, scope, operationId);
        DomainSharedProjectionRebuildResponse aborted = await rebuildGateway.SendAsync(new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity))
            .ConfigureAwait(false);
        if (aborted.Phase != DomainSharedProjectionRebuildPhase.Aborted
            || aborted.Status is not (ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted))
            return false;
        return await AbortUnsealedOrRejectedAsync(root, sealedRejected: true).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SharedProjectionSourceCaptureProgress?> ReadCaptureProgressAsync(
        SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (authority is null || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return null;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root is null || root.CaptureOperationId != operationId && root.LastCompletedOperationId != operationId)
            return null;
        return new(root.CaptureRevision, root.CaptureSealed, root.AppendFenceHeld,
            root.OwnersAccumulated, root.CaptureFingerprint, root.CaptureOwnerCount,
            root.CaptureOperationId is null && root.LastCompletedOperationId == operationId,
            root.CapturePageCount);
    }

    /// <inheritdoc/>
    public async Task<SharedProjectionCapturedSourcePage?> ReadCapturedPageAsync(
        SharedProjectionSourceScope scope, string operationId, int partition)
    {
        CheckScope(scope);
        if (partition < 0 || authority is null
            || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return null;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId != operationId || !root.CaptureSealed && !root.AppendFenceHeld
            || partition >= root.CapturePageCount) return null;
        SharedProjectionCapturedSourcePage? page = await ReadCapturePageStateAsync(partition).ConfigureAwait(false);
        return page?.CaptureRevision == root.CaptureRevision
            && (await ReadRootAsync().ConfigureAwait(false))?.CaptureOperationId == operationId ? page : null;
    }

    /// <inheritdoc/>
    public async Task<bool> ReleaseAppendFenceAsync(SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (authority is null || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId != operationId || !root.CaptureSealed) return false;
        DomainSharedProjectionRebuildResponse? receipt = await ReadDurablePhaseAsync(root, scope, operationId)
            .ConfigureAwait(false);
        if (receipt?.Status != ProjectionDispatchStatus.Completed
            || receipt.Phase == DomainSharedProjectionRebuildPhase.Aborted) return false;
        if (!root.AppendFenceHeld) return true;
        await StateManager.SetStateAsync(RootKey, root with {
            Revision = checked(root.Revision + 1), AppendFenceHeld = false
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return (await ReadRootAsync().ConfigureAwait(false)) is { CaptureOperationId: var operation,
            AppendFenceHeld: false } && operation == operationId;
    }

    /// <inheritdoc/>
    public async Task<bool> MarkOwnersAccumulatedAsync(SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (authority is null || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId != operationId || !root.CaptureSealed || root.AppendFenceHeld) return false;
        DomainSharedProjectionRebuildResponse? receipt = await ReadDurablePhaseAsync(root, scope, operationId)
            .ConfigureAwait(false);
        if (receipt?.Status != ProjectionDispatchStatus.Completed
            || receipt.Phase == DomainSharedProjectionRebuildPhase.Aborted
            || receipt.AcceptedAggregateCount != root.CaptureOwnerCount) return false;
        if (root.OwnersAccumulated) return true;
        await StateManager.SetStateAsync(RootKey, root with {
            Revision = checked(root.Revision + 1), OwnersAccumulated = true
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return (await ReadRootAsync().ConfigureAwait(false)) is { CaptureOperationId: var operation,
            OwnersAccumulated: true } && operation == operationId;
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteCaptureAsync(SharedProjectionSourceScope scope, string operationId)
    {
        CheckScope(scope);
        if (authority is null || !await authority.AuthorizeCaptureAsync(scope, operationId).ConfigureAwait(false)) return false;
        SharedProjectionSourceRosterState? root = await ReadRootAsync().ConfigureAwait(false);
        if (root?.CaptureOperationId is null) return root?.LastCompletedOperationId == operationId;
        if (root.CaptureOperationId != operationId || !root.OwnersAccumulated) return false;
        DomainSharedProjectionRebuildResponse? receipt = await ReadDurablePhaseAsync(root, scope, operationId)
            .ConfigureAwait(false);
        if (receipt?.Status != ProjectionDispatchStatus.Completed
            || receipt.Phase != DomainSharedProjectionRebuildPhase.Committed
            || receipt.AcceptedAggregateCount != root.CaptureOwnerCount) return false;
        for (int partition = 0; partition < root.CapturePageCount; partition++)
            await StateManager.RemoveStateAsync(CapturePageKey(partition)).ConfigureAwait(false);
        await StateManager.SetStateAsync(RootKey, root with {
            Revision = checked(root.Revision + 1), CaptureOperationId = null,
            CaptureSealed = false, CaptureFingerprint = null, AppendFenceHeld = false,
            OwnersAccumulated = false, LastCompletedOperationId = operationId,
            CaptureOwnerCount = 0, CaptureProjectionType = null, CaptureCatalogFingerprint = null
        }).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        return (await ReadRootAsync().ConfigureAwait(false)) is { CaptureOperationId: null,
            LastCompletedOperationId: var completed } && completed == operationId;
    }

    private static DomainSharedProjectionRebuildIdentity CaptureIdentity(
        SharedProjectionSourceRosterState root, SharedProjectionSourceScope scope, string operationId)
        => new(scope.TenantId, scope.Domain, root.CaptureProjectionType!, operationId,
            root.CaptureCatalogFingerprint!);

    private async Task<DomainSharedProjectionRebuildResponse?> ReadDurablePhaseAsync(
        SharedProjectionSourceRosterState root, SharedProjectionSourceScope scope, string operationId)
    {
        if (rebuildGateway is null || string.IsNullOrWhiteSpace(root.CaptureProjectionType)
            || string.IsNullOrWhiteSpace(root.CaptureCatalogFingerprint)
            || string.IsNullOrWhiteSpace(root.CaptureFingerprint)) return null;
        return await rebuildGateway.SendAsync(new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Inspect,
            CaptureIdentity(root, scope, operationId), CaptureInventoryFingerprint: root.CaptureFingerprint))
            .ConfigureAwait(false);
    }

    private void CheckScope(SharedProjectionSourceScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Host.Id.GetId() != scope.ActorId) throw new ArgumentException("Source scope mismatch.", nameof(scope));
    }

    private async Task<SharedProjectionSourceRosterState?> ReadRootAsync()
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<SharedProjectionSourceRosterState>(RootKey).ConfigureAwait(false);
        return state.HasValue ? state.Value : null;
    }

    private async Task<AggregateIdentity[]> ReadPageContentsAsync(int partition)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<AggregateIdentity[]>(PageKey(partition)).ConfigureAwait(false);
        AggregateIdentity[] owners = state.HasValue ? state.Value : [];
        if (owners.Length > MaxOwnersPerPartition || owners.Any(owner => owner is null))
            throw new InvalidOperationException("Corrupt shared source roster partition.");
        return owners;
    }

    private async Task<bool> ContainsOwnerAsync(AggregateIdentity identity, int pageCount)
    {
        if ((await ReadPageContentsAsync(Partition(identity)).ConfigureAwait(false)).Contains(identity)) return true;
        for (int page = PartitionCount; page < pageCount; page++)
        {
            if ((await ReadPageContentsAsync(page).ConfigureAwait(false)).Contains(identity)) return true;
        }

        return false;
    }

    private async Task<SharedProjectionCapturedSourcePage?> ReadCapturePageStateAsync(int partition)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<SharedProjectionCapturedSourcePage>(CapturePageKey(partition))
            .ConfigureAwait(false);
        return state.HasValue ? state.Value : null;
    }

    private static int Partition(AggregateIdentity identity)
        => SHA256.HashData(Encoding.UTF8.GetBytes(identity.ActorId))[0] % PartitionCount;

    private static string PageKey(int partition) => "shared-source-roster-page-" + partition.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);

    private static string CapturePageKey(int partition) => "shared-source-capture-page-" + partition.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
}
