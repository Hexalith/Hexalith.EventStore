using Hexalith.EventStore.Contracts.Security;
using Dapr.Actors.Runtime;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Actual DAPR-backed conditional publication index owner; never registered as an available default.</summary>
/// <param name="host">The runtime-scoped private technical actor.</param>
/// <param name="operations">Independent current private method/request authority; absent defaults deny.</param>
public sealed class SourcePublicationIndexActor(ActorHost host, ISourcePublicationOperationAuthority? operations = null) : Actor(host), ISourcePublicationIndexActor
{
    private const string StateKey = "source-publications-v1";
    /// <summary>Gets the exact actor registration name for a qualified private host binding.</summary>
    public const string ActorTypeName = "SourcePublicationIndexActor";

    /// <inheritdoc/>
    public async Task<SourcePublicationIndexState?> ReadAsync(SourcePublicationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Host.Id.GetId() != scope.ActorId) { throw new ArgumentException("Publication index scope mismatch.", nameof(scope)); }
        if (operations is null || !await operations.ReadIndexAsync(scope).ConfigureAwait(false)) { return null; }
        var result = await ReadStateAsync(scope).ConfigureAwait(false);
        return await operations.ReadIndexAsync(scope).ConfigureAwait(false) ? result : null;
    }
    private async Task<SourcePublicationIndexState?> ReadStateAsync(SourcePublicationScope scope, bool recoverAdmittedOriginal = false)
    {
        // A preceding failed/unknown SaveState may leave staged cached data. Only a fresh durable read may be certified.
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var state = await StateManager.TryGetStateAsync<SourcePublicationIndexState>(StateKey).ConfigureAwait(false);
        var owned = state.HasValue ? Capture(state.Value, scope) : null;
        if (operations is null) { throw new InvalidOperationException("Independent publication authority is absent."); }
        return await RecoverableAnchoredState.ReconcileAsync(PendingScope, owned, await ReadPendingAsync().ConfigureAwait(false),
            value => value is null ? null : Capture(value, scope),
            value => operations.ValidateIndexStateAsync(scope, value?.Revision ?? 0, Digest(value)), operations,
            value => value is null ? throw new InvalidOperationException("Null prospective index.") : PersistTargetAsync(value), recoverAdmittedOriginal).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TryWriteAsync(SourcePublicationIndexWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(write.State);
        if (write.ExpectedRevision < 0 || write.State.Revision != checked(write.ExpectedRevision + 1))
        { throw new ArgumentException("Invalid publication compare revision.", nameof(write)); }
        if (Host.Id.GetId() != write.State.Scope.ActorId) { throw new ArgumentException("Publication index scope mismatch.", nameof(write)); }
        SourcePublicationIndexState owned = Capture(write.State, write.State.Scope);
        var request = write with { State = owned };
        if (operations is null || !await operations.WriteIndexAsync(request).ConfigureAwait(false)) { return false; }
        var retainedOriginal = await ReadPendingAsync().ConfigureAwait(false);
        SourcePublicationIndexState? current = await ReadStateAsync(write.State.Scope, true).ConfigureAwait(false);
        if (retainedOriginal is not null && current is not null && current.Revision == owned.Revision
            && retainedOriginal.TargetDigest == Digest(owned) && Digest(current) == Digest(owned))
        { return await operations.WriteIndexAsync(request).ConfigureAwait(false); }
        if ((current?.Revision ?? 0) != write.ExpectedRevision || current?.PoisonCode is not null) { return false; }
        if (owned.ReconciliationProgress is { } reconciliation
            && !await operations.VerifyReconciliationProgressAsync(reconciliation).ConfigureAwait(false)) { return false; }
        if (current?.ReconciliationProgress is not null && owned.ReconciliationProgress is null && owned.PoisonCode is null) { return false; }
        if (owned.DispatchProgress != current?.DispatchProgress && owned.DispatchProgress is { } progress
            && (!MatchesProgress(owned, Checkpoint(owned), progress) || !await operations.VerifyDispatchProgressAsync(Checkpoint(owned), progress).ConfigureAwait(false))) { return false; }
        if (current is not null && (owned.Entries.Count < current.Entries.Count || !current.Entries.SequenceEqual(owned.Entries.Take(current.Entries.Count))
            || current.Sources.Any(prior => !owned.Sources.Any(next => next.Identity == prior.Identity && next.Head >= prior.Head)))) { return false; }
        if (!await operations.WriteIndexAsync(request).ConfigureAwait(false)) { return false; }
        var pending = RecoverableAnchoredState.Prepare(PendingScope, write.ExpectedRevision, owned.Revision, current, owned);
        if (!await RecoverableAnchoredState.CommitAsync(pending, operations, ReadPendingAsync, PersistPendingAsync).ConfigureAwait(false)) { return false; }
        var persisted = await ReadStateAsync(owned.Scope).ConfigureAwait(false);
        return persisted is not null && Digest(persisted) == Digest(owned) && await operations.WriteIndexAsync(request).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SourcePublicationDispatchProgress?> ReadDispatchProgressAsync(SourcePublicationCheckpoint cut)
    {
        cut = CaptureCheckpoint(cut);
        if (Host.Id.GetId() != cut.Scope.ActorId || operations is null || !await operations.ReadIndexAsync(cut.Scope).ConfigureAwait(false)) { return null; }
        var state = await ReadStateAsync(cut.Scope).ConfigureAwait(false); var original = state?.DispatchProgress;
        if (state is null || original is null || !MatchesProgress(state, cut, original)
            || !await operations.VerifyDispatchProgressAsync(cut, original).ConfigureAwait(false)) { return null; }
        var final = await ReadStateAsync(cut.Scope).ConfigureAwait(false);
        return final is not null && Digest(final) == Digest(state) && await operations.VerifyDispatchProgressAsync(cut, original).ConfigureAwait(false)
            && await operations.ReadIndexAsync(cut.Scope).ConfigureAwait(false) ? original : null;
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationDispatchProgress?> AdvanceDispatchProgressAsync(SourcePublicationDispatchAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance); var cut = CaptureCheckpoint(advance.Cut);
        if (advance.ExpectedPrefix < 0 || advance.AcknowledgedEntries is null || advance.AcknowledgedEntries.Count is < 1 or > 100
            || Host.Id.GetId() != cut.Scope.ActorId || operations is null || !await operations.ReadIndexAsync(cut.Scope).ConfigureAwait(false)) { return null; }
        var entries = new List<SourcePublicationIndexEntry>();
        foreach (var entry in advance.AcknowledgedEntries)
        { if (entries.Count >= 100 || entry is null || entry.Offset != advance.ExpectedPrefix + entries.Count + 1L) { return null; } entries.Add(entry); }
        if (entries.Count == 0) { return null; }
        // A prefix proposal begins with read permission only; original recovery is reached through TryWriteAsync after separate exact mutation/progress admission.
        var state = await ReadStateAsync(cut.Scope).ConfigureAwait(false);
        if (state is null || !MatchesCut(state, cut) || entries[^1].Offset > state.Entries.Count
            || !entries.SequenceEqual(state.Entries.Skip(checked((int)advance.ExpectedPrefix)).Take(entries.Count))) { return null; }
        long predecessor = state.DispatchProgress is { } original && MatchesProgress(state, cut, original)
            && await operations.VerifyDispatchProgressAsync(cut, original).ConfigureAwait(false) ? original.AcknowledgedPrefix : 0;
        if (predecessor != advance.ExpectedPrefix) { return null; }
        var ownedAdvance = advance with { Cut = cut, AcknowledgedEntries = entries.AsReadOnly() };
        var progress = await operations.AuthorizeDispatchAdvanceAsync(ownedAdvance, state).ConfigureAwait(false);
        if (progress is null || progress.Version != checked((state.DispatchProgress?.Version ?? 0) + 1) || progress.AcknowledgedPrefix != entries[^1].Offset
            || !MatchesProgress(state, cut, progress) || !await operations.VerifyDispatchProgressAsync(cut, progress).ConfigureAwait(false)) { return null; }
        var next = state with { Revision = checked(state.Revision + 1), DispatchProgress = progress };
        if (!await TryWriteAsync(new(state.Revision, next)).ConfigureAwait(false)) { return null; }
        return await ReadDispatchProgressAsync(cut).ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationReconciliationProgress?> ReadReconciliationProgressAsync(SourcePublicationCut cut)
    {
        cut = CaptureReconciliationCut(cut);
        if (Host.Id.GetId() != cut.Scope.ActorId || operations is null || !await operations.ReadIndexAsync(cut.Scope).ConfigureAwait(false)) { return null; }
        var state = await ReadStateAsync(cut.Scope).ConfigureAwait(false); var progress = state?.ReconciliationProgress;
        if (state is null || state.PoisonCode is not null || progress is null
            || !await CurrentReconciliationAsync(progress, cut).ConfigureAwait(false)) { return null; }
        var final = await ReadStateAsync(cut.Scope).ConfigureAwait(false);
        return final is not null && Digest(final) == Digest(state) && await CurrentReconciliationAsync(progress, cut).ConfigureAwait(false)
            && await operations.ReadIndexAsync(cut.Scope).ConfigureAwait(false) ? progress : null;
    }
    /// <inheritdoc/>
    public async Task<SourcePublicationReconciliationProgress?> AdvanceReconciliationProgressAsync(SourcePublicationReconciliationAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance); var cut = CaptureReconciliationCut(advance.Cut);
        var currentCut = advance.CurrentCut is { } suppliedCurrent ? CaptureReconciliationCut(suppliedCurrent) : cut;
        if (currentCut.Scope != cut.Scope || Host.Id.GetId() != cut.Scope.ActorId || operations is null || advance.ExpectedVerifiedSources < 0 || advance.ExpectedVerifiedSources >= cut.Sources.Count
            || advance.Source != cut.Sources[advance.ExpectedVerifiedSources] || advance.ObservedHead < advance.Source.Head
            || advance.ObservedHead > 10000 || string.IsNullOrWhiteSpace(advance.ObservationId) || advance.ObservationId.Length > 256
            || !await operations.ReadIndexAsync(cut.Scope).ConfigureAwait(false)) { return null; }
        // Read permission never recovers an unanchored original. Separate exact progress/mutation admission precedes TryWrite recovery.
        var state = await ReadStateAsync(cut.Scope).ConfigureAwait(false);
        if (state?.PoisonCode is not null) { return null; }
        var original = state?.ReconciliationProgress;
        bool reusable = original is not null && original.MatchesCut(cut) && await CurrentReconciliationAsync(original, currentCut).ConfigureAwait(false);
        if (!SameReconciliationCut(cut, currentCut) && !reusable) { return null; }
        int predecessor = reusable ? original!.VerifiedSources : 0;
        if (predecessor != advance.ExpectedVerifiedSources) { return null; }
        var expected = SourcePublicationReconciliationProgress.Capture(new(cut.Scope, checked((original?.Version ?? 0) + 1), cut.AuthorityRevision,
            cut.Sources, predecessor + 1, (reusable ? original!.Publications : []).Concat(advance.Publications).ToArray(), "proposed") { OriginalCut = reusable ? original!.OriginalCut ?? cut : cut });
        var ownedAdvance = advance with { Cut = cut, CurrentCut = currentCut, Publications = expected.Publications.Where(p => p.Identity == advance.Source.Identity).ToArray() };
        var approved = await operations.AuthorizeReconciliationAdvanceAsync(ownedAdvance, state).ConfigureAwait(false);
        if (approved is null) { return null; }
        approved = SourcePublicationReconciliationProgress.Capture(approved);
        if (approved.Version != expected.Version || approved.VerifiedSources != expected.VerifiedSources || !approved.MatchesCut(cut)
            || !approved.Publications.SequenceEqual(expected.Publications) || !SameOriginalCut(approved.OriginalCut, expected.OriginalCut)
            || !await CurrentReconciliationAsync(approved, currentCut).ConfigureAwait(false)) { return null; }
        var next = state is null ? new SourcePublicationIndexState(cut.Scope, 1, cut.AuthorityRevision, [], []) { ReconciliationProgress = approved }
            : state with { Revision = checked(state.Revision + 1), ReconciliationProgress = approved };
        if (!await TryWriteAsync(new(state?.Revision ?? 0, next)).ConfigureAwait(false)) { return null; }
        return await ReadReconciliationProgressAsync(currentCut).ConfigureAwait(false);
    }
    private async Task<bool> CurrentReconciliationAsync(SourcePublicationReconciliationProgress progress, SourcePublicationCut current)
        => operations is not null && await operations.VerifyReconciliationProgressAsync(progress).ConfigureAwait(false)
            && (progress.MatchesCut(current) || progress.CanContinueUnder(current)
                && await operations.VerifyFiniteCutContinuationAsync(progress, current).ConfigureAwait(false));
    private static bool SameReconciliationCut(SourcePublicationCut first, SourcePublicationCut second)
        => first.Scope == second.Scope && first.AuthorityRevision == second.AuthorityRevision && first.Sources.SequenceEqual(second.Sources);
    private static bool SameOriginalCut(SourcePublicationCut? first, SourcePublicationCut? second)
        => first is null ? second is null : second is not null && SameReconciliationCut(first, second)
            && first.IsComplete == second.IsComplete && first.ObservedAt == second.ObservedAt && first.ValidUntil == second.ValidUntil;
    private static SourcePublicationCut CaptureReconciliationCut(SourcePublicationCut cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        if (!cut.IsComplete || cut.ObservedAt == default || cut.ValidUntil <= cut.ObservedAt) { throw new ArgumentException("Invalid reconciliation cut.", nameof(cut)); }
        var captured = CaptureCheckpoint(new(cut.Scope, 1, cut.AuthorityRevision, cut.Sources, 0));
        return cut with { Sources = Array.AsReadOnly(captured.Sources.OrderBy(h => h.Identity.ActorId, StringComparer.Ordinal).ToArray()) };
    }
    private static SourcePublicationCheckpoint Checkpoint(SourcePublicationIndexState state)
        => new(state.Scope, state.Revision, state.AuthorityRevision, state.Sources, state.Entries.Count);
    private static bool MatchesCut(SourcePublicationIndexState state, SourcePublicationCheckpoint cut)
        => state.Scope == cut.Scope && state.PoisonCode is null && state.Revision >= cut.IndexRevision && state.AuthorityRevision == cut.AuthorityRevision
            && state.Entries.Count == cut.LastOffset && state.Sources.SequenceEqual(cut.Sources);
    private static bool MatchesProgress(SourcePublicationIndexState state, SourcePublicationCheckpoint cut, SourcePublicationDispatchProgress progress)
        => MatchesCut(state, cut) && ValidProgress(state, progress) && progress.SourceAuthorityRevision == cut.AuthorityRevision
            && progress.LastOffset == cut.LastOffset && progress.SourcesDigest == Digest(cut.Sources);
    private static bool ValidProgress(SourcePublicationIndexState state, SourcePublicationDispatchProgress progress)
        => progress.Scope == state.Scope && progress.Version > 0 && progress.AcknowledgedPrefix > 0 && progress.AcknowledgedPrefix <= progress.LastOffset
            && progress.LastOffset <= state.Entries.Count && new[] { progress.SourceAuthorityRevision, progress.DeliveryTarget, progress.DeliveryAuthorityRevision, progress.ReceiptId }.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 256)
            && new[] { progress.PrefixDigest, progress.SourcesDigest }.All(value => value is { Length: 64 } && value.All(char.IsAsciiHexDigit))
            && progress.PrefixDigest == Digest(state.Entries.Take(checked((int)progress.AcknowledgedPrefix)).ToArray());
    private static SourcePublicationCheckpoint CaptureCheckpoint(SourcePublicationCheckpoint cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        if (cut.Scope is null || cut.IndexRevision <= 0 || cut.LastOffset is < 0 or > 10000 || string.IsNullOrWhiteSpace(cut.AuthorityRevision)
            || cut.AuthorityRevision.Length > 256 || cut.Sources is null || cut.Sources.Count > 1000) { throw new ArgumentException("Malformed exact publication cut.", nameof(cut)); }
        var heads = new List<SourcePublicationHead>();
        foreach (var head in cut.Sources)
        { if (heads.Count >= 1000 || head is null || head.Identity is null || head.Identity.TenantId != cut.Scope.Tenant || head.Identity.Domain != cut.Scope.Domain || head.Head is < 0 or > 10000 || heads.Any(h => h.Identity == head.Identity)) { throw new ArgumentException("Malformed publication cut vector.", nameof(cut)); } heads.Add(head); }
        return cut with { Sources = heads.AsReadOnly() };
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
    private async Task<SourcePublicationIndexState?> PersistTargetAsync(SourcePublicationIndexState next)
    {
        await StateManager.SetStateAsync(StateKey, next).ConfigureAwait(false);
        _ = await StateManager.TryRemoveStateAsync(PendingKey).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        var confirmed = await StateManager.TryGetStateAsync<SourcePublicationIndexState>(StateKey).ConfigureAwait(false);
        return confirmed.HasValue ? confirmed.Value : throw new InvalidOperationException("Reconciled main state is missing.");
    }
    private static SourcePublicationIndexState Capture(SourcePublicationIndexState state, SourcePublicationScope scope)
    {
        if (state.Scope != scope || state.Revision <= 0 || string.IsNullOrWhiteSpace(state.AuthorityRevision)
            || state.AuthorityRevision.Length > 256 || state.Sources is null || state.Entries is null
            || state.Sources.Count is < 0 or > 1000 || state.Entries.Count is < 0 or > 10000
            || state.PoisonCode is not (null or "publication-source-regressed" or "publication-source-incompatible"
                or "publication-reference-conflict" or "publication-source-disappeared"))
        { throw new InvalidOperationException("Malformed publication index."); }
        var heads = new List<SourcePublicationHead>();
        foreach (SourcePublicationHead head in state.Sources)
        {
            if (heads.Count >= 1000 || head is null || head.Identity is null || head.Head is < 0 or > 10000
                || head.Identity.TenantId != scope.Tenant || head.Identity.Domain != scope.Domain
                || heads.Any(h => h.Identity == head.Identity))
            { throw new InvalidOperationException("Malformed publication source vector."); }
            heads.Add(head);
        }
        var entries = new List<SourcePublicationIndexEntry>();
        foreach (SourcePublicationIndexEntry entry in state.Entries)
        {
            if (entries.Count >= 10000 || entry is null || entry.Offset != entries.Count + 1L || entry.Publication is not { } publication
                || publication.Identity is null || publication.SourceRevision is < 1 or > 10000
                || string.IsNullOrWhiteSpace(publication.PublicationId) || publication.PublicationId.Length > 256
                || string.IsNullOrWhiteSpace(publication.SourceMessageId) || publication.SourceMessageId.Length > 256
                || publication.StableFieldsDigest is not { Length: 64 } digest
                || !digest.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')
                || !heads.Any(h => h.Identity == publication.Identity && h.Head >= publication.SourceRevision)
                || entries.Any(e => e.Publication.PublicationId == publication.PublicationId
                    || e.Publication.Identity == publication.Identity && e.Publication.SourceRevision == publication.SourceRevision))
            { throw new InvalidOperationException("Malformed publication reference index."); }
            entries.Add(entry);
        }
        if (state.DispatchProgress is { } progress && !ValidProgress(state with { Sources = heads.AsReadOnly(), Entries = entries.AsReadOnly() }, progress)) { throw new InvalidOperationException("Malformed original dispatch progress."); }
        var reconciliation = state.ReconciliationProgress is { } retained ? SourcePublicationReconciliationProgress.Capture(retained) : null;
        if (reconciliation is not null && reconciliation.Scope != scope) { throw new InvalidOperationException("Reconciliation scope mismatch."); }
        return state with { Sources = heads.AsReadOnly(), Entries = entries.AsReadOnly(), ReconciliationProgress = reconciliation };
    }
    private static string Digest<T>(T state) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
}
