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
            SourcePublicationCut? initial = CaptureCut(await deadline.ReadAsync(token => namespaces.ReadAsync(scope, token)).ConfigureAwait(false), scope, deadline);
            if (initial is null) { deadline.ThrowIfCancellationRequested(); return new(null, "publication-namespace-unavailable"); }
            SourcePublicationIndexState? prior = CaptureState(await deadline.ReadAsync(token => store.ReadAsync(scope, token)).ConfigureAwait(false), scope, deadline);
            if (prior?.PoisonCode is not null) { deadline.ThrowIfCancellationRequested(); return new(null, prior.PoisonCode); }
            var projected = new List<SourcePublicationDescriptor>();
            string? conflict = null;
            foreach (SourcePublicationHead target in initial.Sources)
            {
                deadline.ThrowIfCancellationRequested();
                SourcePublicationHead? previous = prior?.Sources.FirstOrDefault(h => h.Identity == target.Identity);
                if (previous is not null && previous.Head > target.Head) { conflict = "publication-source-regressed"; break; }
                // A registered but not-yet-created source contributes the empty committed prefix to this finite cut.
                if (target.Head == 0) { continue; }
                AuthoritativeStreamReadResult read = await deadline.ReadAsync(token => streams.ReadAsync(target.Identity, token)).ConfigureAwait(false);
                if (!read.IsAuthoritative || read.Stream is not { } source || source.Identity != target.Identity || source.Head < target.Head
                    || source.Events is null || source.Events.Count != source.Head || source.ObservedAt == default
                    || source.ObservedAt > timeProvider.GetUtcNow() || string.IsNullOrWhiteSpace(source.ObservationId))
                { deadline.ThrowIfCancellationRequested(); return new(null, "publication-source-hole"); }
                var prefix = new List<StreamReadEvent>();
                long payloadBytes = 0;
                foreach (StreamReadEvent item in source.Events)
                {
                    deadline.ThrowIfCancellationRequested();
                    if (prefix.Count == target.Head)
                    {
                        if (source.Head == target.Head) { return new(null, "publication-source-hole"); }
                        break;
                    }
                    if (item is null || item.Payload is null || item.Payload.Length > 16 * 1024 * 1024 - payloadBytes)
                    { return new(null, "publication-source-hole"); }
                    byte[] payload = item.Payload.ToArray();
                    payloadBytes += payload.Length;
                    var captured = item with { Payload = payload, ProtectionMetadata = CaptureProtection(item.ProtectionMetadata, deadline) };
                    if (captured.SequenceNumber != prefix.Count + 1 || string.IsNullOrWhiteSpace(captured.EventTypeName)
                        || string.IsNullOrWhiteSpace(captured.MessageId) || captured.MetadataVersion <= 0
                        || string.IsNullOrWhiteSpace(captured.SerializationFormat)) { return new(null, "publication-source-hole"); }
                    prefix.Add(captured);
                }
                if (prefix.Count != target.Head) { return new(null, "publication-source-hole"); }
                IReadOnlyList<SourcePublicationDescriptor> candidates;
                try { candidates = projector.Project(source with { Head = target.Head, Events = prefix.AsReadOnly() }, cancellationToken); }
                catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
                { conflict = "publication-source-incompatible"; break; }
                if (candidates is null || candidates.Count < 0 || candidates.Count > MaxEntries) { conflict = "publication-source-incompatible"; break; }
                var perSource = new List<SourcePublicationDescriptor>();
                foreach (SourcePublicationDescriptor candidate in candidates)
                {
                    deadline.ThrowIfCancellationRequested();
                    if (perSource.Count >= MaxEntries || !ValidDescriptor(candidate) || candidate.Identity != target.Identity
                        || candidate.SourceRevision > target.Head || prefix[(int)candidate.SourceRevision - 1].MessageId != candidate.SourceMessageId
                        || perSource.Any(p => p.SourceRevision == candidate.SourceRevision))
                    { conflict = "publication-source-incompatible"; break; }
                    perSource.Add(candidate);
                }
                if (conflict is not null) { break; }
                foreach (SourcePublicationIndexEntry existing in prior?.Entries.Where(e => e.Publication.Identity == target.Identity) ?? [])
                {
                    deadline.ThrowIfCancellationRequested();
                    if (!perSource.Contains(existing.Publication)) { conflict = "publication-reference-conflict"; break; }
                }
                if (conflict is not null) { break; }
                if (previous is not null && perSource.Any(p => p.SourceRevision <= previous.Head
                    && !(prior!.Entries.Any(e => e.Publication == p))))
                { conflict = "publication-reference-conflict"; break; }
                projected.AddRange(perSource.OrderBy(p => p.SourceRevision));
                if (projected.Count > MaxEntries) { return new(null, "publication-operational-bound"); }
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
            // A changed namespace authority/cut cannot authenticate this reconciliation.
            SourcePublicationCut? final = CaptureCut(await deadline.ReadAsync(token => namespaces.ReadAsync(scope, token)).ConfigureAwait(false), scope, deadline);
            if (final is null || !SameCut(initial, final)) { return new(null, "publication-namespace-changed"); }
            long revision = prior?.Revision ?? 0;
            var updated = new SourcePublicationIndexState(scope, checked(revision + 1), initial.AuthorityRevision,
                conflict is not null && prior is not null ? prior.Sources : initial.Sources,
                conflict is not null ? prior?.Entries ?? Array.Empty<SourcePublicationIndexEntry>() : entries.AsReadOnly(), conflict);
            deadline.ThrowIfCancellationRequested();
            if (!await deadline.ReadAsync(token => store.TryWriteAsync(scope, revision, updated, token)).ConfigureAwait(false)) { continue; }
            // Read back exact persisted outcome; ambiguous/lost acknowledgement never fabricates a checkpoint.
            SourcePublicationIndexState? persisted = CaptureState(await deadline.ReadAsync(token => store.ReadAsync(scope, token)).ConfigureAwait(false), scope, deadline);
            if (persisted is null || persisted.Revision != updated.Revision || persisted.PoisonCode != updated.PoisonCode
                || persisted.AuthorityRevision != updated.AuthorityRevision || !persisted.Sources.SequenceEqual(updated.Sources)
                || !persisted.Entries.SequenceEqual(updated.Entries))
            { return new(null, "publication-index-outcome-unknown"); }
            deadline.ThrowIfCancellationRequested();
            SourcePublicationCut? releaseCut = CaptureCut(await deadline.ReadAsync(token => namespaces.ReadAsync(scope, token)).ConfigureAwait(false), scope, deadline);
            if (releaseCut is null || !SameCut(initial, releaseCut)) { return new(null, "publication-namespace-changed"); }
            if (persisted.PoisonCode is not null) { return new(null, persisted.PoisonCode); }
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

    private SourcePublicationCut? CaptureCut(SourcePublicationCut? cut, SourcePublicationScope scope, AuthoritativeStreamReadDeadline deadline)
    {
        deadline.ThrowIfCancellationRequested();
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (cut is null || cut.Scope != scope || !cut.IsComplete || (string.IsNullOrWhiteSpace(cut.AuthorityRevision) || cut.AuthorityRevision.Length > 256)
            || cut.ObservedAt > now || cut.ValidUntil <= now || cut.ValidUntil <= cut.ObservedAt || cut.Sources is null
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
        return state with { Sources = heads.AsReadOnly(), Entries = entries.AsReadOnly() };
    }
}
