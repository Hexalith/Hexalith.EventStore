using System.Collections.ObjectModel;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Reads every exact committed prefix under a current complete finite namespace cut.</summary>
/// <param name="namespaces">Independent installed coverage and current authority.</param>
/// <param name="streams">Authenticated source reader.</param>
/// <param name="clock">Monotonic budget and authority clock.</param>
/// <remarks>The existing feed's thirty-second, thousand-source and ten-thousand-event operational bounds apply.
/// These denial bounds do not qualify production capacity or noncooperative provider reclamation.</remarks>
public sealed class SourceNamespaceSnapshotReader(ISourcePublicationNamespaceSource namespaces,
    IAuthoritativeEventStreamReader streams, TimeProvider clock)
{
    /// <summary>Returns all detached source bytes or null; partial coverage never becomes an empty catalogue.</summary>
    /// <param name="scope">Exact installed source namespace.</param>
    /// <param name="cancellationToken">Original caller cancellation.</param>
    /// <returns>The complete current snapshot, or no evidence.</returns>
    public async Task<SourceNamespaceSnapshot?> ReadAsync(SourcePublicationScope scope, CancellationToken cancellationToken = default)
    {
        var folded = await ReadFoldedCoreAsync(scope, (source, _) => source, retainSourceBytes: true, cancellationToken).ConfigureAwait(false);
        return folded is null ? null : new(folded.Cut, folded.Values);
    }

    /// <summary>Folds one bounded detached prefix at a time, releases its bytes, then reconfirms the complete namespace before returning any summaries.</summary>
    /// <typeparam name="T">Small materialized summary; the callback must not retain or lazily enumerate source bytes.</typeparam>
    /// <param name="scope">Exact installed namespace.</param><param name="fold">Pure source fold; a null result denies the entire read.</param>
    /// <param name="cancellationToken">Caller cancellation.</param><returns>Complete current materialized summaries or no evidence.</returns>
    public Task<SourceNamespaceFold<T>?> ReadFoldedAsync<T>(SourcePublicationScope scope,
        Func<AuthoritativeEventStream, CancellationToken, T?> fold, CancellationToken cancellationToken = default) where T : class
        => ReadFoldedCoreAsync(scope, fold, retainSourceBytes: false, cancellationToken);

    private async Task<SourceNamespaceFold<T>?> ReadFoldedCoreAsync<T>(SourcePublicationScope scope,
        Func<AuthoritativeEventStream, CancellationToken, T?> fold, bool retainSourceBytes, CancellationToken cancellationToken) where T : class
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(scope);
            using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock,
                cancellationToken, clock.GetTimestamp());
            try
            {
                var suppliedInitial = await deadline.ReadAsync(token => namespaces.ReadAsync(scope, token)).ConfigureAwait(false);
                var initial = await deadline.ReadAsync(_ => Task.FromResult(CaptureCut(suppliedInitial, scope, deadline))).ConfigureAwait(false);
                if (initial is null) { return null; }
                var sources = new List<T>();
                long retainedBytes = 0;
                foreach (var head in initial.Sources)
                {
                    deadline.ThrowIfCancellationRequested();
                    // The authenticated namespace owner certifies the actual committed head.
                    // An uncreated registration has no source bytes to read; gateway NotFound is not a hole.
                    if (head.Head == 0) { continue; }
                    var read = await deadline.ReadAsync(token => streams.ReadAsync(head.Identity, token)).ConfigureAwait(false);
                    // Capture uses only a local byte counter; abandoned worker progress never changes this operation's retained state.
                    long alreadyRetained = retainedBytes;
                    var captured = await deadline.ReadAsync(_ => Task.FromResult(CaptureSource(read, head, deadline,
                        retainSourceBytes ? 16 * 1024 * 1024 - alreadyRetained : 16 * 1024 * 1024))).ConfigureAwait(false);
                    if (captured is null) { return null; }
                    if (retainSourceBytes) { retainedBytes += captured.Value.PayloadBytes; }
                    var value = await deadline.ReadAsync(token => Task.FromResult(fold(captured.Value.Source, token))).ConfigureAwait(false);
                    deadline.ThrowIfCancellationRequested();
                    if (value is null) { return null; }
                    sources.Add(value);
                }
                var suppliedFinal = await deadline.ReadAsync(token => namespaces.ReadAsync(scope, token)).ConfigureAwait(false);
                var final = await deadline.ReadAsync(_ => Task.FromResult(CaptureCut(suppliedFinal, scope, deadline))).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                return final is not null && final.AuthorityRevision == initial.AuthorityRevision
                    && final.Sources.SequenceEqual(initial.Sources)
                    ? new(final, sources.AsReadOnly()) : null;
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException
                or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
            { cancellationToken.ThrowIfCancellationRequested(); return null; }
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }

    private (AuthoritativeEventStream Source, long PayloadBytes)? CaptureSource(AuthoritativeStreamReadResult read,
        SourcePublicationHead head, AuthoritativeStreamReadDeadline deadline, long maximumBytes)
    {
        deadline.ThrowIfCancellationRequested();
        if (!read.IsAuthoritative || read.Stream is not { } source || source.Identity != head.Identity
            || source.Head < head.Head || source.Events is null || source.Events.Count < head.Head || source.Head > 10000 || source.Events.Count > 10000
            || source.ObservedAt == default || source.ObservedAt > clock.GetUtcNow()
            || string.IsNullOrWhiteSpace(source.ObservationId)) { return null; }
        var events = new List<StreamReadEvent>(); long payloadBytes = 0;
        foreach (var item in source.Events)
        {
            deadline.ThrowIfCancellationRequested();
            if (events.Count == head.Head) { break; }
            if (item is null || item.SequenceNumber != events.Count + 1L
                || item.Payload is null || item.Payload.Length > maximumBytes - payloadBytes
                || string.IsNullOrWhiteSpace(item.EventTypeName) || string.IsNullOrWhiteSpace(item.MessageId)
                || item.MetadataVersion <= 0 || string.IsNullOrWhiteSpace(item.SerializationFormat)) { return null; }
            var payload = item.Payload.ToArray(); payloadBytes += payload.Length;
            events.Add(item with { Payload = payload, ProtectionMetadata = CaptureProtection(item.ProtectionMetadata, deadline) });
        }
        deadline.ThrowIfCancellationRequested();
        return events.Count == head.Head ? (source with { Head = head.Head, Events = events.AsReadOnly() }, payloadBytes) : null;
    }

    private SourcePublicationCut? CaptureCut(SourcePublicationCut? cut, SourcePublicationScope scope, AuthoritativeStreamReadDeadline deadline)
    {
        deadline.ThrowIfCancellationRequested();
        var now = clock.GetUtcNow();
        if (cut is null || cut.Scope != scope || !cut.IsComplete || cut.ObservedAt == default || cut.ObservedAt > now
            || cut.ValidUntil <= now || cut.ValidUntil <= cut.ObservedAt
            || string.IsNullOrWhiteSpace(cut.AuthorityRevision) || cut.AuthorityRevision.Length > 256
            || cut.Sources is null || cut.Sources.Count is < 0 or > 1000) { return null; }
        var heads = new List<SourcePublicationHead>();
        foreach (var head in cut.Sources)
        {
            deadline.ThrowIfCancellationRequested();
            if (heads.Count >= 1000 || head is null || head.Identity is null || head.Head is < 0 or > 10000
                || head.Identity.TenantId != scope.Tenant || head.Identity.Domain != scope.Domain
                || heads.Any(h => h.Identity == head.Identity)) { return null; }
            heads.Add(head);
        }
        deadline.ThrowIfCancellationRequested();
        return cut with { Sources = Array.AsReadOnly(heads.OrderBy(h => h.Identity.ActorId, StringComparer.Ordinal).ToArray()) };
    }

    private static EventStorePayloadProtectionMetadata? CaptureProtection(EventStorePayloadProtectionMetadata? metadata,
        AuthoritativeStreamReadDeadline deadline)
    {
        if (metadata is null) { return null; }
        if (metadata.Scheme?.Length > EventStorePayloadProtectionMetadata.MaxSchemeLength
            || metadata.KeyAlias?.Length > EventStorePayloadProtectionMetadata.MaxKeyAliasLength
            || metadata.ContentHint?.Length > EventStorePayloadProtectionMetadata.MaxContentHintLength)
        { throw new InvalidOperationException("Malformed namespace source protection metadata."); }
        IReadOnlyDictionary<string, string>? flags = null;
        if (metadata.CompatibilityFlags is { } supplied)
        {
            if (supplied.Count is < 0 or > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount)
            { throw new InvalidOperationException("Malformed namespace source protection flags."); }
            var owned = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in supplied)
            {
                deadline.ThrowIfCancellationRequested();
                if (owned.Count >= EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount || pair.Key is null || pair.Value is null
                    || pair.Key.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagKeyLength
                    || pair.Value.Length > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagValueLength || !owned.TryAdd(pair.Key, pair.Value))
                { throw new InvalidOperationException("Malformed namespace source protection flags."); }
            }
            flags = new ReadOnlyDictionary<string, string>(owned);
        }
        var captured = metadata with { CompatibilityFlags = flags };
        if (captured.State is not (PayloadProtectionState.Unprotected or PayloadProtectionState.Protected)
            || !EventStorePayloadProtectionMetadataCarrier.TryValidate(captured, out _))
        { throw new InvalidOperationException("Unreadable namespace source protection metadata."); }
        return captured;
    }
}
