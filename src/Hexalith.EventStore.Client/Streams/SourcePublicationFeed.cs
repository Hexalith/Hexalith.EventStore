using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Reconciles every authenticated finite-cut prefix before releasing durable ordered publication references.</summary>
/// <param name="namespaces">The independently authenticated namespace coverage source; no available default exists.</param>
/// <param name="streams">The authenticated exact source-prefix reader.</param>
/// <param name="projector">The closed safe publication projection.</param>
/// <param name="store">The technical conditional durable index owner.</param>
/// <param name="timeProvider">The current authorization and operational clock.</param>
/// <remarks>Operational bounds fail closed; they are not qualified deployment capacity. Full namespace installation, provider binding and delivery pump are separate work.</remarks>
public sealed class SourcePublicationFeed(ISourcePublicationNamespaceSource namespaces, IAuthoritativeEventStreamReader streams,
    ISourcePublicationProjector projector, ISourcePublicationIndexStore store, TimeProvider timeProvider)
{
    private const int MaxSources = 1000;
    private const int MaxEntries = 10000;

    /// <summary>Backfills/reconciles an exact finite cut and pages its immutable index. Any hole, conflict or stale authority releases no checkpoint.</summary>
    public async Task<SourcePublicationReadResult> ReadAsync(SourcePublicationScope scope, long afterOffset = 0, int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(scope);
            if (afterOffset < 0 || pageSize is < 1 or > 100) { cancellationToken.ThrowIfCancellationRequested(); return new(null, "publication-invalid-request"); }
            using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), timeProvider, cancellationToken, timeProvider.GetTimestamp());
            try
            {
                SourcePublicationReadResult result;
                try { result = await ReconcileAsync(scope, afterOffset, pageSize, deadline, cancellationToken).ConfigureAwait(false); }
                catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
                { result = new(null, "publication-unavailable"); }
                deadline.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new(null, deadline.IsExpired ? "publication-time-bound-exceeded" : "publication-unavailable");
            }

        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private async Task<SourcePublicationReadResult> ReconcileAsync(SourcePublicationScope scope, long afterOffset, int pageSize,
        AuthoritativeStreamReadDeadline deadline, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            SourcePublicationCut? initial = await ReadCutAsync(scope, deadline).ConfigureAwait(false);
            if (initial is null) { deadline.ThrowIfCancellationRequested(); return new(null, "publication-namespace-unavailable"); }
            SourcePublicationIndexState? prior = await ReadStateAsync(scope, deadline).ConfigureAwait(false);
            if (prior?.PoisonCode is not null) { deadline.ThrowIfCancellationRequested(); return new(null, prior.PoisonCode); }
            var retained = await deadline.ReadAsync(async token =>
            {
                var progress = await store.ReadReconciliationProgressAsync(initial, token).ConfigureAwait(false);
                return progress is null ? null : SourcePublicationReconciliationProgress.Capture(progress);
            }).ConfigureAwait(false);
            SourcePublicationCut currentInitial = initial;
            if (retained is not null && (prior?.ReconciliationProgress is null || !SameProgress(retained, prior.ReconciliationProgress))) { return new(null, "publication-reconciliation-proof-unavailable"); }
            if (retained is not null && !retained.MatchesCut(initial))
            {
                if (!retained.CanContinueUnder(initial)) { return new(null, "publication-reconciliation-proof-unavailable"); }
                if (retained.VerifiedSources < retained.Sources.Count)
                {
                    var original = await deadline.ReadAsync(_ => Task.FromResult(CaptureCut(retained.OriginalCut, scope, deadline))).ConfigureAwait(false);
                    if (original is null) { return new(null, "publication-reconciliation-proof-unavailable"); }
                    initial = original;
                }
                else { retained = null; } // Completed original work must not hide new publications from the next reconciliation.
            }
            if (retained is null && prior?.ReconciliationProgress?.MatchesCut(initial) == true)
            { return new(null, "publication-reconciliation-proof-unavailable"); }
            var projected = retained?.Publications.ToList() ?? [];
            int verified = retained?.VerifiedSources ?? 0;
            int worked = 0;
            string? conflict = null;
            int sourceIndex = 0;
            foreach (SourcePublicationHead target in initial.Sources)
            {
                deadline.ThrowIfCancellationRequested();
                SourcePublicationHead? previous = prior?.Sources.FirstOrDefault(h => h.Identity == target.Identity);
                if (previous is not null && previous.Head > target.Head) { conflict = "publication-source-regressed"; break; }
                if (sourceIndex++ < verified) { continue; }
                var result = target.Head == 0 ? (Publications: (IReadOnlyList<SourcePublicationDescriptor>?)Array.Empty<SourcePublicationDescriptor>(), ObservationId: "empty-certified-cut", Head: 0L, Failure: (string?)null)
                    : await ReadSourceAsync(target, deadline).ConfigureAwait(false);
                if (result.Publications is null)
                {
                    if (result.Failure == "publication-source-incompatible") { conflict = result.Failure; break; }
                    return new(null, result.Failure ?? "publication-source-hole");
                }
                foreach (SourcePublicationIndexEntry existing in prior?.Entries.Where(e => e.Publication.Identity == target.Identity) ?? [])
                {
                    deadline.ThrowIfCancellationRequested();
                    if (!result.Publications.Contains(existing.Publication)) { conflict = "publication-reference-conflict"; break; }
                }
                if (conflict is not null) { break; }
                if (previous is not null && result.Publications.Any(p => p.SourceRevision <= previous.Head && !prior!.Entries.Any(e => e.Publication == p)))
                { conflict = "publication-reference-conflict"; break; }
                if (result.Publications.Any(candidate => projected.Any(existing => existing.PublicationId == candidate.PublicationId)))
                { conflict = "publication-reference-conflict"; break; }
                projected.AddRange(result.Publications);
                if (projected.Count > MaxEntries) { return new(null, "publication-operational-bound"); }
                var advanced = await deadline.ReadAsync(async token =>
                {
                    var proof = await store.AdvanceReconciliationProgressAsync(new(initial, sourceIndex - 1, target, result.ObservationId, result.Head, result.Publications) { CurrentCut = currentInitial }, token).ConfigureAwait(false);
                    return proof is null ? null : SourcePublicationReconciliationProgress.Capture(proof);
                }).ConfigureAwait(false);
                if (advanced is not null)
                {
                    if (!advanced.MatchesCut(initial) || advanced.VerifiedSources != verified + 1 || !advanced.Publications.SequenceEqual(projected))
                    { return new(null, "publication-reconciliation-proof-unavailable"); }
                    retained = advanced; verified = advanced.VerifiedSources;
                    prior = await ReadStateAsync(scope, deadline).ConfigureAwait(false);
                    if (prior?.ReconciliationProgress is null || !SameProgress(retained, prior.ReconciliationProgress))
                    { return new(null, "publication-reconciliation-outcome-unknown"); }
                    if (++worked >= 20 && verified < initial.Sources.Count) { return new(null, "publication-reconciliation-pending"); }
                }
                else if (retained is not null) { return new(null, "publication-reconciliation-unavailable"); }
            }
            if (prior?.Sources.Any(h => !initial.Sources.Any(target => target.Identity == h.Identity)) == true)
            { conflict = "publication-source-disappeared"; }
            var entries = prior?.Entries.ToList() ?? [];
            if (conflict is null)
            {
                foreach (SourcePublicationDescriptor candidate in projected)
                {
                    deadline.ThrowIfCancellationRequested();
                    SourcePublicationIndexEntry? existing = entries.FirstOrDefault(e => e.Publication.PublicationId == candidate.PublicationId);
                    if (existing is not null)
                    {
                        if (existing.Publication != candidate) { conflict = "publication-reference-conflict"; break; }
                        continue;
                    }
                    if (entries.Count >= MaxEntries) { return new(null, "publication-operational-bound"); }
                    entries.Add(new(entries.Count + 1L, candidate));
                }
            }
            // Original finite-cut completion is separate from the freshly complete current inventory.
            SourcePublicationCut? final = await ReadCutAsync(scope, deadline).ConfigureAwait(false);
            if (final is null || !SameCut(initial, final) && (retained is null || !await ConfirmReconciliationAsync(final, prior!, deadline).ConfigureAwait(false)))
            { return new(null, "publication-namespace-changed"); }
            long revision = prior?.Revision ?? 0;
            var updated = new SourcePublicationIndexState(scope, checked(revision + 1), initial.AuthorityRevision,
                conflict is not null && prior is not null ? prior.Sources : initial.Sources,
                conflict is not null ? prior?.Entries ?? Array.Empty<SourcePublicationIndexEntry>() : entries.AsReadOnly(), conflict) { DispatchProgress = prior?.DispatchProgress, ReconciliationProgress = retained };
            bool unchanged = prior is not null && prior.AuthorityRevision == updated.AuthorityRevision && prior.PoisonCode == updated.PoisonCode
                && prior.Sources.SequenceEqual(updated.Sources) && prior.Entries.SequenceEqual(updated.Entries);
            if (unchanged) { updated = prior!; }
            deadline.ThrowIfCancellationRequested();
            if (!unchanged && !await deadline.ReadAsync(token => store.TryWriteAsync(scope, revision, updated, token)).ConfigureAwait(false)) { continue; }
            // Read back exact persisted outcome; ambiguous/lost acknowledgement never fabricates a checkpoint.
            SourcePublicationIndexState? persisted = await ReadStateAsync(scope, deadline).ConfigureAwait(false);
            if (persisted is null || persisted.Revision != updated.Revision || persisted.PoisonCode != updated.PoisonCode
                || persisted.AuthorityRevision != updated.AuthorityRevision || !persisted.Sources.SequenceEqual(updated.Sources)
                || !persisted.Entries.SequenceEqual(updated.Entries) || !SameProgress(persisted.ReconciliationProgress, updated.ReconciliationProgress))
            { return new(null, "publication-index-outcome-unknown"); }
            deadline.ThrowIfCancellationRequested();
            SourcePublicationCut? releaseCut = await ReadCutAsync(scope, deadline).ConfigureAwait(false);
            if (releaseCut is null || !SameCut(initial, releaseCut) && retained is null) { return new(null, "publication-namespace-changed"); }
            if (persisted.PoisonCode is not null) { return new(null, persisted.PoisonCode); }
            if (!await ConfirmReconciliationAsync(releaseCut, persisted, deadline).ConfigureAwait(false))
            { return new(null, "publication-reconciliation-proof-unavailable"); }
            if (afterOffset > persisted.Entries.Count) { return new(null, "publication-cursor-outside-cut"); }
            var page = persisted.Entries.Where(e => e.Offset > afterOffset).Take(pageSize).ToArray();
            long next = page.Length == 0 ? afterOffset : page[^1].Offset;
            var checkpoint = new SourcePublicationCheckpoint(scope, persisted.Revision, persisted.AuthorityRevision,
                persisted.Sources, persisted.Entries.Count);
            deadline.ThrowIfCancellationRequested();
            return new(new(checkpoint, Array.AsReadOnly(page), next, next < persisted.Entries.Count), null);
        }
        deadline.ThrowIfCancellationRequested();
        return new(null, "publication-index-contended");
    }

    /// <summary>Reads a bounded page of the reconciled immutable index under fresh exact complete-cut and durable-state checks, without refolding every source again.</summary>
    internal async Task<SourcePublicationReadResult> ReadIndexedPageAsync(SourcePublicationCheckpoint cut, long afterOffset, CancellationToken token)
    {
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), timeProvider, token, timeProvider.GetTimestamp());
        var initial = await ReadCutAsync(cut.Scope, deadline).ConfigureAwait(false);
        var state = await ReadStateAsync(cut.Scope, deadline).ConfigureAwait(false);
        if (initial is null || state is null || state.PoisonCode is not null || state.Revision < cut.IndexRevision || state.AuthorityRevision != cut.AuthorityRevision
            || initial.AuthorityRevision != cut.AuthorityRevision || !state.Sources.SequenceEqual(cut.Sources)
            || !initial.Sources.SequenceEqual(cut.Sources) && (state.ReconciliationProgress is not { } continued || !continued.CanContinueUnder(initial))
            || state.ReconciliationProgress is { } partial && partial.VerifiedSources != partial.Sources.Count
            || state.Entries.Count != cut.LastOffset || afterOffset < 0 || afterOffset > state.Entries.Count) { return new(null, "publication-resume-cut-changed"); }
        var entries = state.Entries.Where(e => e.Offset > afterOffset).Take(100).ToArray();
        var durable = await ReadStateAsync(cut.Scope, deadline).ConfigureAwait(false);
        var final = await ReadCutAsync(cut.Scope, deadline).ConfigureAwait(false);
        deadline.ThrowIfCancellationRequested();
        if (durable is null || durable.Revision != state.Revision || durable.AuthorityRevision != state.AuthorityRevision || durable.PoisonCode != state.PoisonCode
            || durable.DispatchProgress != state.DispatchProgress || !durable.Sources.SequenceEqual(state.Sources) || !durable.Entries.SequenceEqual(state.Entries)
            || final is null || !SameCut(initial, final) && (durable.ReconciliationProgress is not { } preserved || !preserved.CanContinueUnder(final)))
        { return new(null, "publication-resume-cut-changed"); }
        if (!await ConfirmReconciliationAsync(final, durable, deadline).ConfigureAwait(false)) { return new(null, "publication-reconciliation-proof-unavailable"); }
        long next = entries.Length == 0 ? afterOffset : entries[^1].Offset;
        var checkpoint = new SourcePublicationCheckpoint(cut.Scope, durable.Revision, durable.AuthorityRevision, durable.Sources, durable.Entries.Count);
        return new(new(checkpoint, Array.AsReadOnly(entries), next, next < durable.Entries.Count), null);
    }
    /// <summary>Requests independent retained original progress; the owner must revalidate the complete cut/current delivery binding.</summary>
    internal Task<SourcePublicationDispatchProgress?> ReadDispatchProgressAsync(SourcePublicationCheckpoint cut, CancellationToken token)
        => store.ReadDispatchProgressAsync(cut, token);
    /// <summary>The owner independently authenticates actual source acknowledgements; receiver statuses are never durable-prefix proof.</summary>
    internal Task<SourcePublicationDispatchProgress?> AdvanceDispatchProgressAsync(SourcePublicationDispatchAdvance advance, CancellationToken token)
        => store.AdvanceDispatchProgressAsync(advance, token);

    private async Task<bool> ConfirmReconciliationAsync(SourcePublicationCut cut, SourcePublicationIndexState state, AuthoritativeStreamReadDeadline deadline)
    {
        if (state.ReconciliationProgress is not { } original) { return true; }
        var fresh = await deadline.ReadAsync(async token =>
        {
            var proof = await store.ReadReconciliationProgressAsync(cut, token).ConfigureAwait(false);
            return proof is null ? null : SourcePublicationReconciliationProgress.Capture(proof);
        }).ConfigureAwait(false);
        deadline.ThrowIfCancellationRequested();
        return fresh is not null && fresh.VerifiedSources == fresh.Sources.Count
            && (fresh.MatchesCut(cut) || fresh.CanContinueUnder(cut)) && SameProgress(fresh, original);
    }
    private Task<SourcePublicationCut?> ReadCutAsync(SourcePublicationScope scope, AuthoritativeStreamReadDeadline deadline)
        => deadline.ReadAsync(async token => CaptureCut(await namespaces.ReadAsync(scope, token).ConfigureAwait(false), scope, deadline));
    private Task<SourcePublicationIndexState?> ReadStateAsync(SourcePublicationScope scope, AuthoritativeStreamReadDeadline deadline)
        => deadline.ReadAsync(async token => CaptureState(await store.ReadAsync(scope, token).ConfigureAwait(false), scope, deadline));
    private async Task<(IReadOnlyList<SourcePublicationDescriptor>? Publications, string ObservationId, long Head, string? Failure)> ReadSourceAsync(
        SourcePublicationHead target, AuthoritativeStreamReadDeadline deadline)
    {
        return await deadline.ReadAsync(async token =>
        {
            AuthoritativeStreamReadResult read = await streams.ReadAsync(target.Identity, token).ConfigureAwait(false);
            if (!read.IsAuthoritative || read.Stream is not { } source || source.Identity != target.Identity || source.Head < target.Head || source.Head > MaxEntries
                || source.Events is null || source.Events.Count != source.Head || source.ObservedAt == default || source.ObservedAt > timeProvider.GetUtcNow()
                || string.IsNullOrWhiteSpace(source.ObservationId) || source.ObservationId.Length > 256)
            { return ((IReadOnlyList<SourcePublicationDescriptor>?)null, "", 0L, "publication-source-hole"); }
            var prefix = new List<StreamReadEvent>(); long bytes = 0;
            foreach (var item in source.Events)
            {
                deadline.ThrowIfCancellationRequested();
                if (prefix.Count == target.Head) { if (source.Head == target.Head) { return (null, "", 0L, "publication-source-hole"); } break; }
                if (item is null || item.Payload is null || item.Payload.Length > 16 * 1024 * 1024 - bytes) { return (null, "", 0L, "publication-source-hole"); }
                byte[] payload = item.Payload.ToArray(); bytes += payload.Length;
                var captured = item with { Payload = payload, ProtectionMetadata = CaptureProtection(item.ProtectionMetadata, deadline) };
                if (captured.SequenceNumber != prefix.Count + 1 || string.IsNullOrWhiteSpace(captured.EventTypeName) || string.IsNullOrWhiteSpace(captured.MessageId)
                    || captured.MetadataVersion <= 0 || string.IsNullOrWhiteSpace(captured.SerializationFormat)) { return (null, "", 0L, "publication-source-hole"); }
                prefix.Add(captured);
            }
            if (prefix.Count != target.Head) { return (null, "", 0L, "publication-source-hole"); }
            IReadOnlyList<SourcePublicationDescriptor> candidates;
            try { candidates = projector.Project(source with { Head = target.Head, Events = prefix.AsReadOnly() }, token); }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
            { return (null, "", 0L, "publication-source-incompatible"); }
            if (candidates is null || candidates.Count is < 0 or > MaxEntries) { return (null, "", 0L, "publication-source-incompatible"); }
            var owned = new List<SourcePublicationDescriptor>();
            foreach (var candidate in candidates)
            {
                deadline.ThrowIfCancellationRequested();
                if (owned.Count >= MaxEntries || !ValidDescriptor(candidate) || candidate.Identity != target.Identity || candidate.SourceRevision > target.Head
                    || prefix[(int)candidate.SourceRevision - 1].MessageId != candidate.SourceMessageId || owned.Any(p => p.SourceRevision == candidate.SourceRevision))
                { return (null, "", 0L, "publication-source-incompatible"); }
                owned.Add(candidate);
            }
            deadline.ThrowIfCancellationRequested();
            return ((IReadOnlyList<SourcePublicationDescriptor>?)Array.AsReadOnly(owned.OrderBy(p => p.SourceRevision).ToArray()), source.ObservationId, source.Head, (string?)null);
        }).ConfigureAwait(false);
    }
    private static bool SameProgress(SourcePublicationReconciliationProgress? left, SourcePublicationReconciliationProgress? right)
        => left is null ? right is null : right is not null && left.Scope == right.Scope && left.Version == right.Version
            && left.SourceAuthorityRevision == right.SourceAuthorityRevision && left.VerifiedSources == right.VerifiedSources && left.ReceiptId == right.ReceiptId
            && left.Sources.SequenceEqual(right.Sources) && left.Publications.SequenceEqual(right.Publications)
            && SameOriginalCut(left.OriginalCut, right.OriginalCut);
    private static bool SameOriginalCut(SourcePublicationCut? left, SourcePublicationCut? right)
        => left is null ? right is null : right is not null && SameCut(left, right) && left.ObservedAt == right.ObservedAt
            && left.ValidUntil == right.ValidUntil && left.IsComplete == right.IsComplete;

    private SourcePublicationCut? CaptureCut(SourcePublicationCut? cut, SourcePublicationScope scope, AuthoritativeStreamReadDeadline deadline)
    {
        deadline.ThrowIfCancellationRequested();
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (cut is null || cut.Scope != scope || !cut.IsComplete || (string.IsNullOrWhiteSpace(cut.AuthorityRevision) || cut.AuthorityRevision.Length > 256)
            || cut.ObservedAt == default || cut.ObservedAt > now || cut.ValidUntil <= now || cut.ValidUntil <= cut.ObservedAt || cut.Sources is null
            || cut.Sources.Count < 0 || cut.Sources.Count > MaxSources) { return null; }
        var owned = new List<SourcePublicationHead>();
        foreach (SourcePublicationHead head in cut.Sources)
        {
            deadline.ThrowIfCancellationRequested();
            if (owned.Count >= MaxSources || head is null || head.Identity is null || head.Head < 0 || head.Head > MaxEntries
                || head.Identity.TenantId != scope.Tenant || head.Identity.Domain != scope.Domain || owned.Any(h => h.Identity == head.Identity)) { return null; }
            owned.Add(head);
        }
        deadline.ThrowIfCancellationRequested();
        return cut with { Sources = Array.AsReadOnly(owned.OrderBy(h => h.Identity.ActorId, StringComparer.Ordinal).ToArray()) };
    }

    private static EventStorePayloadProtectionMetadata? CaptureProtection(EventStorePayloadProtectionMetadata? metadata,
        AuthoritativeStreamReadDeadline deadline)
    {
        if (metadata is null) { return null; }
        if (metadata.Scheme?.Length > EventStorePayloadProtectionMetadata.MaxSchemeLength
            || metadata.KeyAlias?.Length > EventStorePayloadProtectionMetadata.MaxKeyAliasLength
            || metadata.ContentHint?.Length > EventStorePayloadProtectionMetadata.MaxContentHintLength)
        { throw new InvalidOperationException("Malformed publication protection metadata."); }
        IReadOnlyDictionary<string, string>? flags = null;
        if (metadata.CompatibilityFlags is { } supplied)
        {
            if (supplied.Count < 0 || supplied.Count > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount)
            { throw new InvalidOperationException("Malformed publication protection flags."); }
            var owned = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in supplied)
            {
                deadline.ThrowIfCancellationRequested();
                if (owned.Count >= EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount || pair.Key is null || pair.Value is null
                    || pair.Key.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagKeyLength
                    || pair.Value.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagValueLength || !owned.TryAdd(pair.Key, pair.Value))
                { throw new InvalidOperationException("Malformed publication protection flags."); }
            }
            flags = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(owned);
        }
        var captured = metadata with { CompatibilityFlags = flags };
        if (captured.State is not (PayloadProtectionState.Unprotected or PayloadProtectionState.Protected)
            || !EventStorePayloadProtectionMetadataCarrier.TryValidate(captured, out _))
        { throw new InvalidOperationException("Unreadable publication protection metadata."); }
        deadline.ThrowIfCancellationRequested();
        return captured;
    }

    private static bool SameCut(SourcePublicationCut first, SourcePublicationCut second)
        => first.Scope == second.Scope && first.AuthorityRevision == second.AuthorityRevision && first.Sources.SequenceEqual(second.Sources);

    private static bool ValidDescriptor(SourcePublicationDescriptor? descriptor)
        => descriptor is not null && descriptor.Identity is not null && descriptor.SourceRevision is > 0 and <= MaxEntries
            && !string.IsNullOrWhiteSpace(descriptor.PublicationId) && descriptor.PublicationId.Length <= 256
            && !string.IsNullOrWhiteSpace(descriptor.SourceMessageId) && descriptor.SourceMessageId.Length <= 256
            && descriptor.StableFieldsDigest is { Length: 64 } digest && digest.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static SourcePublicationIndexState? CaptureState(SourcePublicationIndexState? state, SourcePublicationScope scope,
        AuthoritativeStreamReadDeadline deadline)
    {
        deadline.ThrowIfCancellationRequested();
        if (state is null) { return null; }
        if (state.Scope != scope || state.Revision <= 0 || (string.IsNullOrWhiteSpace(state.AuthorityRevision) || state.AuthorityRevision.Length > 256)
            || state.Sources is null || state.Sources.Count < 0 || state.Sources.Count > MaxSources || state.Entries is null || state.Entries.Count < 0 || state.Entries.Count > MaxEntries
            || state.PoisonCode is not (null or "publication-source-regressed" or "publication-source-incompatible" or "publication-reference-conflict" or "publication-source-disappeared"))
        { throw new InvalidOperationException("Malformed publication index."); }
        var heads = new List<SourcePublicationHead>();
        foreach (SourcePublicationHead head in state.Sources)
        {
            deadline.ThrowIfCancellationRequested();
            if (heads.Count >= MaxSources || head is null || head.Identity is null || head.Head < 0 || head.Head > MaxEntries
                || head.Identity.TenantId != scope.Tenant || head.Identity.Domain != scope.Domain || heads.Any(h => h.Identity == head.Identity))
            { throw new InvalidOperationException("Malformed publication source vector."); }
            heads.Add(head);
        }
        var entries = new List<SourcePublicationIndexEntry>();
        foreach (SourcePublicationIndexEntry entry in state.Entries)
        {
            deadline.ThrowIfCancellationRequested();
            if (entries.Count >= MaxEntries || entry is null || entry.Offset != entries.Count + 1L || !ValidDescriptor(entry.Publication)
                || !heads.Any(h => h.Identity == entry.Publication.Identity && h.Head >= entry.Publication.SourceRevision)
                || entries.Any(e => e.Publication.PublicationId == entry.Publication.PublicationId
                    || e.Publication.Identity == entry.Publication.Identity && e.Publication.SourceRevision == entry.Publication.SourceRevision))
            { throw new InvalidOperationException("Malformed publication reference index."); }
            entries.Add(entry);
        }
        deadline.ThrowIfCancellationRequested();
        var reconciliation = state.ReconciliationProgress is { } proof ? SourcePublicationReconciliationProgress.Capture(proof) : null;
        if (reconciliation is not null && reconciliation.Scope != scope) { throw new InvalidOperationException("Reconciliation scope mismatch."); }
        return state with { Sources = heads.AsReadOnly(), Entries = entries.AsReadOnly(), ReconciliationProgress = reconciliation };
    }
}
