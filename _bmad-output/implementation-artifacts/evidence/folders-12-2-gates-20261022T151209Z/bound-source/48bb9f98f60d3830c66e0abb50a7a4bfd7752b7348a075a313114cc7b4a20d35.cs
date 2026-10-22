using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Actual durable technical epoch/repair/cohort metadata protocol. Unregistered/disabled without independently qualified writer receipts/current private credentials.
/// Metadata reads never substitute for the required one-transaction target append and writer enforcement.</summary>
public sealed class DirectoryMigrationBoundaryActor(ActorHost host, IDirectoryMigrationBoundaryAuthority? authority = null) : Actor(host), IDirectoryMigrationBoundaryActor
{
    private const string StateKey = "directory-migration-boundary-candidate-v1";
    private const int Bound = 10000;
    /// <summary>Gets the private actor registration name.</summary>
    public const string ActorTypeName = "DirectoryMigrationBoundaryActor";
    /// <summary>Gets one exact tenant owner for all predecessor epoch/fence/cohort/outcome metadata.</summary>
    public static string GetActorId(string tenant) => new AggregateIdentity(tenant, "eventstore", "directory-migration-boundary-v1").ActorId;
    /// <inheritdoc/>
    public Task<DirectoryBoundaryOutcome> InstallAsync(DirectoryEpochInstallation installation)
    {
        Validate(installation); Check(installation.TenantId);
        return ApplyAsync(installation.TenantId, installation.OperationId, installation.ExpectedRevision, Digest(installation), "Install", async state => {
            if (state.CurrentEpochId is not null || installation.ExpectedRevision != 0 || authority is null || !await authority.VerifyInstallationAsync(installation).ConfigureAwait(false)) { return null; }
            return (state with { CurrentEpochId = installation.EpochId, Installations = state.Installations.Append(installation).ToArray() }, DirectoryBoundaryOutcomeState.Installed);
        });
    }
    /// <inheritdoc/>
    public Task<DirectoryBoundaryOutcome> RepairAsync(DirectoryRepairBoundary boundary)
    {
        var owned = Capture(boundary); Check(owned.TenantId);
        return ApplyAsync(owned.TenantId, owned.OperationId, owned.ExpectedRevision, Digest(owned), "Repair", async state => {
            if (state.CurrentEpochId != owned.EpochId || state.ActiveRepairId is not null || authority is null || !await authority.VerifyRepairAsync(owned).ConfigureAwait(false)) { return null; }
            return (state with { ActiveRepairId = owned.OperationId, Repairs = state.Repairs.Append(owned).ToArray() }, DirectoryBoundaryOutcomeState.RepairFenced);
        });
    }
    /// <inheritdoc/>
    public Task<DirectoryBoundaryOutcome> RecordDrainAsync(DirectoryRepairDrainReceipt receipt)
    {
        Validate(receipt); Check(receipt.TenantId);
        return ApplyAsync(receipt.TenantId, receipt.OperationId, receipt.ExpectedRevision, Digest(receipt), "RecordDrain", async state => {
            var repair = state.Repairs.SingleOrDefault(r => r.OperationId == receipt.RepairId);
            if (state.ActiveRepairId != receipt.RepairId || repair is null || !repair.Cohort.Contains(receipt.Original)
                || state.Drains.Any(d => d.RepairId == receipt.RepairId && d.Original == receipt.Original)
                || authority is null || !await authority.VerifyDrainAsync(receipt).ConfigureAwait(false)) { return null; }
            return (state with { Drains = state.Drains.Append(receipt).ToArray() }, DirectoryBoundaryOutcomeState.DrainRecorded);
        });
    }
    /// <inheritdoc/>
    public Task<DirectoryBoundaryOutcome> ActivateAsync(DirectoryEpochActivation activation)
    {
        Validate(activation); Check(activation.TenantId);
        return ApplyAsync(activation.TenantId, activation.OperationId, activation.ExpectedRevision, Digest(activation), "Activate", async state => {
            var repair = state.Repairs.SingleOrDefault(r => r.OperationId == activation.RepairId);
            if (state.ActiveRepairId != activation.RepairId || repair is null || state.Installations.Any(i => i.EpochId == activation.Successor.EpochId)
                || repair.Cohort.Any(item => !state.Drains.Any(d => d.RepairId == repair.OperationId && d.Original == item)) || authority is null
                || !await authority.VerifyInstallationAsync(activation.Successor).ConfigureAwait(false) || !await authority.VerifyActivationAsync(activation).ConfigureAwait(false)) { return null; }
            return (state with { CurrentEpochId = activation.Successor.EpochId, ActiveRepairId = null,
                Installations = state.Installations.Append(activation.Successor).ToArray(), Activations = state.Activations.Append(activation).ToArray() }, DirectoryBoundaryOutcomeState.Activated);
        });
    }
    /// <inheritdoc/>
    public async Task<DirectoryEpochLedger?> ReadAsync(string tenantId)
    {
        Check(tenantId); string digest = Digest(new[] { tenantId, "Read" });
        if (!await AuthorizedAsync(tenantId, "Read", digest).ConfigureAwait(false)) { return null; }
        var state = await ReadStateAsync(tenantId).ConfigureAwait(false);
        return await AuthorizedAsync(tenantId, "Read", digest).ConfigureAwait(false) ? state : null;
    }
    /// <inheritdoc/>
    public async Task<DirectoryBoundaryOutcome?> LookupAsync(string tenantId, string operationId, string requestDigest)
    {
        Check(tenantId); Text(operationId); Hex(requestDigest);
        string credentialScope = Digest(new[] { tenantId, operationId, requestDigest });
        if (!await AuthorizedAsync(tenantId, "Lookup", credentialScope).ConfigureAwait(false)) { return null; }
        var state = await ReadStateAsync(tenantId).ConfigureAwait(false); var result = state?.Outcomes.SingleOrDefault(o => o.OperationId == operationId && o.RequestDigest == requestDigest);
        return await AuthorizedAsync(tenantId, "Lookup", credentialScope).ConfigureAwait(false) ? result : null;
    }
    private async Task<DirectoryBoundaryOutcome> ApplyAsync(string tenant, string id, long expected, string requestDigest, string method,
        Func<DirectoryEpochLedger, Task<(DirectoryEpochLedger Next, DirectoryBoundaryOutcomeState State)?>> transition)
    {
        var unavailable = new DirectoryBoundaryOutcome(id, requestDigest, DirectoryBoundaryOutcomeState.Unavailable, 0);
        if (!await AuthorizedAsync(tenant, method, requestDigest).ConfigureAwait(false)) { return unavailable; }
        var state = await ReadStateAsync(tenant, true).ConfigureAwait(false); if (state is null) { return unavailable; }
        var prior = state.Outcomes.SingleOrDefault(o => o.OperationId == id);
        if (prior is not null) { return prior.RequestDigest == requestDigest && await AuthorizedAsync(tenant, method, requestDigest).ConfigureAwait(false)
            ? prior : unavailable with { State = DirectoryBoundaryOutcomeState.Conflict }; }
        if (state.Outcomes.Count >= Bound) { return unavailable; }
        var change = state.Revision != expected ? (state, DirectoryBoundaryOutcomeState.Stale) : await transition(state).ConfigureAwait(false);
        if (change is null || !await AuthorizedAsync(tenant, method, requestDigest).ConfigureAwait(false)) { return unavailable; }
        long revision = checked(state.Revision + 1); var outcome = new DirectoryBoundaryOutcome(id, requestDigest, change.Value.Item2, revision);
        var next = Capture(change.Value.Item1 with { Revision = revision, Outcomes = state.Outcomes.Append(outcome).ToArray() });
        if (authority is null) { return unavailable; }
        var pending = RecoverableAnchoredState.Prepare(PendingScope, state.Revision, revision, state, next);
        if (!await RecoverableAnchoredState.CommitAsync(pending, authority, ReadPendingAsync, PersistPendingAsync).ConfigureAwait(false)) { return unavailable; }
        var confirmed = await ReadStateAsync(tenant).ConfigureAwait(false);
        return confirmed?.Outcomes.SingleOrDefault(o => o.OperationId == id) == outcome && await AuthorizedAsync(tenant, method, requestDigest).ConfigureAwait(false) ? outcome : unavailable;
    }
    private Task<bool> AuthorizedAsync(string tenant, string method, string digest) => authority?.AuthorizeOperationAsync(tenant, method, digest) ?? Task.FromResult(false);
    private async Task<DirectoryEpochLedger?> ReadStateAsync(string tenant, bool recoverAdmittedOriginal = false)
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false); var stored = await StateManager.TryGetStateAsync<DirectoryEpochLedger>(StateKey).ConfigureAwait(false);
        var state = stored.HasValue ? Capture(stored.Value) : new(tenant, 0, null, null, [], [], [], [], []);
        if (state.TenantId != tenant || authority is null) { return null; }
        try
        {
        return await RecoverableAnchoredState.ReconcileAsync(PendingScope, state, await ReadPendingAsync().ConfigureAwait(false), Capture,
            value => authority.ValidateStateAsync(tenant, value.Revision, Digest(value)), authority, PersistTargetAsync, recoverAdmittedOriginal).ConfigureAwait(false);
        }
        catch (InvalidOperationException) { return null; }
    }

    private string PendingScope => Host.Id.GetId() + "|" + StateKey;
    private const string PendingKey = StateKey + "-pending-transition-v1";
    private async Task<AnchoredStateTransition?> ReadPendingAsync()
    {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var pending = await StateManager.TryGetStateAsync<AnchoredStateTransition>(PendingKey).ConfigureAwait(false);
        return pending.HasValue ? pending.Value : null;
    }
    private async Task PersistPendingAsync(AnchoredStateTransition pending)
    {
        await StateManager.SetStateAsync(PendingKey, pending).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }
    private async Task<DirectoryEpochLedger> PersistTargetAsync(DirectoryEpochLedger next)
    {
        await StateManager.SetStateAsync(StateKey, next).ConfigureAwait(false);
        _ = await StateManager.TryRemoveStateAsync(PendingKey).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var confirmed = await StateManager.TryGetStateAsync<DirectoryEpochLedger>(StateKey).ConfigureAwait(false);
        return confirmed.HasValue ? confirmed.Value : throw new InvalidOperationException("Reconciled main state is missing.");
    }
    private void Check(string tenant) { Text(tenant); if (Host.Id.GetId() != GetActorId(tenant)) { throw new ArgumentException("Directory boundary tenant mismatch."); } }
    private static DirectoryEpochLedger Capture(DirectoryEpochLedger state)
    {
        Text(state.TenantId);
        if (state.Revision < 0 || state.Installations is null || state.Repairs is null || state.Drains is null || state.Activations is null || state.Outcomes is null
            || state.Installations.Count > Bound || state.Repairs.Count > Bound || state.Drains.Count > Bound || state.Activations.Count > Bound || state.Outcomes.Count > Bound)
        { throw new InvalidOperationException("Malformed bounded directory boundary state."); }
        var installations = CaptureList(state.Installations).Select(i => { Validate(i); if (i.TenantId != state.TenantId) { throw new InvalidOperationException("Foreign installation."); } return i; }).ToArray();
        var repairs = CaptureList(state.Repairs).Select(Capture).ToArray(); var drains = CaptureList(state.Drains); var activations = CaptureList(state.Activations); var outcomes = CaptureList(state.Outcomes);
        foreach (var r in repairs) { if (r.TenantId != state.TenantId || !installations.Any(i => i.EpochId == r.EpochId)) { throw new InvalidOperationException("Uninstalled repair."); } }
        foreach (var d in drains) { Validate(d); if (d.TenantId != state.TenantId || !repairs.Any(r => r.OperationId == d.RepairId && r.Cohort.Contains(d.Original))) { throw new InvalidOperationException("Unmanifested result."); } }
        foreach (var a in activations) { Validate(a); if (a.TenantId != state.TenantId || !repairs.Any(r => r.OperationId == a.RepairId)) { throw new InvalidOperationException("Unknown activation."); } }
        if (outcomes.LongLength != state.Revision || outcomes.Where((o, n) => o is null || o.CommittedRevision != n + 1L
                || o.State is not (DirectoryBoundaryOutcomeState.Installed or DirectoryBoundaryOutcomeState.RepairFenced or DirectoryBoundaryOutcomeState.DrainRecorded or DirectoryBoundaryOutcomeState.Activated or DirectoryBoundaryOutcomeState.Stale)).Any()
            || outcomes.Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count() != outcomes.Length
            || installations.Select(i => i.EpochId).Distinct(StringComparer.Ordinal).Count() != installations.Length
            || repairs.Select(r => r.OperationId).Distinct(StringComparer.Ordinal).Count() != repairs.Length
            || drains.Select(d => d.RepairId + "/" + d.Original.Owner.ActorId + "/" + d.Original.OperationId).Distinct(StringComparer.Ordinal).Count() != drains.Length
            || state.CurrentEpochId != installations.LastOrDefault()?.EpochId || state.ActiveRepairId is not null && !repairs.Any(r => r.OperationId == state.ActiveRepairId))
        { throw new InvalidOperationException("Malformed directory boundary history."); }
        foreach (var o in outcomes) { Text(o.OperationId); Hex(o.RequestDigest); }
        return state with { Installations = Array.AsReadOnly(installations), Repairs = Array.AsReadOnly(repairs), Drains = Array.AsReadOnly(drains), Activations = Array.AsReadOnly(activations), Outcomes = Array.AsReadOnly(outcomes) };
    }
    private static T[] CaptureList<T>(IReadOnlyList<T> source)
    {
        if (source.Count is < 0 or > Bound) { throw new InvalidOperationException("Malformed bounded boundary collection."); }
        var owned = new List<T>();
        foreach (var item in source) { if (owned.Count >= Bound || item is null) { throw new InvalidOperationException("Malformed bounded boundary collection."); } owned.Add(item); }
        return owned.ToArray();
    }
    private static DirectoryRepairBoundary Capture(DirectoryRepairBoundary source)
    {
        ArgumentNullException.ThrowIfNull(source); foreach (string s in new[] { source.TenantId, source.OperationId, source.EpochId, source.NamespaceCheckpoint, source.WriterRevocationReceipt }) { Text(s); }
        if (source.ExpectedRevision < 0 || source.Cohort is null || source.Cohort.Count > 1000) { throw new ArgumentException("Malformed finite repair cohort."); }
        var owned = new List<DirectoryRepairCohortItem>(); string? previous = null;
        foreach (var item in source.Cohort)
        {
            if (owned.Count >= 1000) { throw new ArgumentException("Malformed finite repair cohort."); } Validate(item);
            string key = item.Owner.ActorId + "/" + item.OperationId;
            if (item.Owner.TenantId != source.TenantId || previous is not null && StringComparer.Ordinal.Compare(previous, key) >= 0) { throw new ArgumentException("Unordered or foreign repair cohort."); }
            owned.Add(item); previous = key;
        }
        return source with { Cohort = Array.AsReadOnly(owned.ToArray()) };
    }
    private static void Validate(DirectoryRepairCohortItem item)
    {
        ArgumentNullException.ThrowIfNull(item); ArgumentNullException.ThrowIfNull(item.Owner);
        foreach (string s in new[] { item.OperationId, item.SourceConversationId, item.PermitId, item.EffectCapabilityId }) { Text(s); }
        if (!Enum.IsDefined(item.Kind) || item.Kind == DirectoryWriteKind.OriginalRecoveryResult || item.OwnerRevision <= 0 || item.OriginalState is not ("Pending" or "Committed" or "Authorized")) { throw new ArgumentException("Malformed original cohort item."); }
    }
    private static void Validate(DirectoryEpochInstallation i)
    { ArgumentNullException.ThrowIfNull(i); foreach (string s in new[] { i.TenantId, i.OperationId, i.EpochId, i.LegacyRevocationReceipt, i.WriterEnforcementReceipt }) { Text(s); } if (i.ExpectedRevision < 0) { throw new ArgumentException("Invalid epoch revision."); } }
    private static void Validate(DirectoryRepairDrainReceipt d)
    { ArgumentNullException.ThrowIfNull(d); foreach (string s in new[] { d.TenantId, d.OperationId, d.RepairId, d.SourceReceipt }) { Text(s); } Validate(d.Original); if (d.Original.Owner.TenantId != d.TenantId || d.ExpectedRevision < 0 || d.TerminalState is not ("Settled" or "TypedNegative" or "CancelledBeforeEffect")) { throw new ArgumentException("Invalid exact terminal drain."); } }
    private static void Validate(DirectoryEpochActivation a)
    { ArgumentNullException.ThrowIfNull(a); foreach (string s in new[] { a.TenantId, a.OperationId, a.RepairId, a.BridgeRevocationReceipt, a.PreservedGuardEvidenceReceipt }) { Text(s); } Validate(a.Successor); if (a.Successor.TenantId != a.TenantId || a.ExpectedRevision < 0 || a.Successor.ExpectedRevision != a.ExpectedRevision) { throw new ArgumentException("Invalid successor activation."); } }
    private static void Text(string s) { if (string.IsNullOrWhiteSpace(s) || s.Length > 2048 || new UTF8Encoding(false, true).GetByteCount(s) > 2048) { throw new ArgumentException("Invalid directory boundary identity."); } }
    private static void Hex(string s) { if (s is not { Length: 64 } || s.Any(c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F'))) { throw new ArgumentException("Invalid boundary digest."); } }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
