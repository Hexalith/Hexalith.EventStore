using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Folds complete authenticated source prefixes under an unchanged finite namespace cut.</summary>
/// <param name="namespaces">The independently authenticated namespace source.</param>
/// <param name="streams">The authenticated exact source-prefix reader.</param>
/// <param name="timeProvider">The current authority and operation clock.</param>
public sealed class SourceNamespaceSnapshotReader(
    ISourcePublicationNamespaceSource namespaces,
    IAuthoritativeEventStreamReader streams,
    TimeProvider timeProvider)
{
    private const int MaxSources = 1000;
    private const int MaxEvents = 10000;
    private const long MaxPayloadBytes = 16 * 1024 * 1024;

    /// <summary>Returns a complete folded snapshot, or no values when any authority or prefix cannot be verified.</summary>
    /// <typeparam name="T">The folded value type.</typeparam>
    /// <param name="scope">The exact installed namespace.</param>
    /// <param name="fold">A read-only fold over an exact source prefix; null omits that source's value.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>A covered snapshot, or null when evidence is unavailable.</returns>
    public async Task<SourceNamespaceSnapshot<T>?> ReadFoldedAsync<T>(
        SourcePublicationScope scope,
        Func<AuthoritativeEventStream, CancellationToken, T?> fold,
        CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(fold);
        using var deadline = new AuthoritativeStreamReadDeadline(
            TimeSpan.FromSeconds(30), timeProvider, cancellationToken, timeProvider.GetTimestamp());
        try
        {
            SourcePublicationCut? first = CaptureCut(
                await deadline.ReadAsync(token => namespaces.ReadAsync(scope, token)).ConfigureAwait(false), scope, deadline);
            if (first is null) { return null; }

            var values = new List<T>();
            long eventCount = 0;
            long payloadBytes = 0;
            foreach (SourcePublicationHead head in first.Sources)
            {
                deadline.ThrowIfCancellationRequested();
                // An installed source without a committed event has the empty prefix.
                if (head.Head == 0) { continue; }
                AuthoritativeStreamReadResult read = await deadline.ReadAsync(
                    token => streams.ReadAsync(head.Identity, token)).ConfigureAwait(false);
                if (!read.IsAuthoritative || read.Stream is not { } source || source.Identity != head.Identity
                    || source.Head != head.Head || source.ObservedAt == default
                    || source.ObservedAt > timeProvider.GetUtcNow() || string.IsNullOrWhiteSpace(source.ObservationId)
                    || source.Events is null || source.Events.Count != head.Head)
                { return null; }

                var events = new List<StreamReadEvent>();
                foreach (StreamReadEvent item in source.Events)
                {
                    deadline.ThrowIfCancellationRequested();
                    if (eventCount >= MaxEvents || item is null || item.Payload is null
                        || item.Payload.Length > MaxPayloadBytes - payloadBytes
                        || item.SequenceNumber != events.Count + 1L || string.IsNullOrWhiteSpace(item.EventTypeName)
                        || string.IsNullOrWhiteSpace(item.SerializationFormat) || string.IsNullOrWhiteSpace(item.MessageId)
                        || item.MetadataVersion <= 0)
                    { return null; }
                    payloadBytes += item.Payload.Length;
                    eventCount++;
                    events.Add(item with { Payload = item.Payload.ToArray(),
                        ProtectionMetadata = CaptureProtection(item.ProtectionMetadata, deadline) });
                }
                AuthoritativeEventStream captured = source with { Events = events.AsReadOnly() };
                T? value = await deadline.ReadAsync(token => Task.FromResult(fold(captured, token))).ConfigureAwait(false);
                if (value is not null) { values.Add(value); }
            }

            SourcePublicationCut? last = CaptureCut(
                await deadline.ReadAsync(token => namespaces.ReadAsync(scope, token)).ConfigureAwait(false), scope, deadline);
            if (last is null || !SameCut(first, last) || first.ValidUntil <= timeProvider.GetUtcNow()) { return null; }
            deadline.ThrowIfCancellationRequested();
            return new(first, values.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        catch (Exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private SourcePublicationCut? CaptureCut(SourcePublicationCut? cut, SourcePublicationScope scope,
        AuthoritativeStreamReadDeadline deadline)
    {
        deadline.ThrowIfCancellationRequested();
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (cut is null || cut.Scope != scope || !cut.IsComplete
            || string.IsNullOrWhiteSpace(cut.AuthorityRevision) || cut.AuthorityRevision.Length > 256
            || cut.ObservedAt == default || cut.ObservedAt > now || cut.ValidUntil <= now
            || cut.ValidUntil <= cut.ObservedAt || cut.Sources is null
            || cut.Sources.Count < 0 || cut.Sources.Count > MaxSources)
        { return null; }

        var owned = new List<SourcePublicationHead>();
        long total = 0;
        foreach (SourcePublicationHead head in cut.Sources)
        {
            deadline.ThrowIfCancellationRequested();
            if (head is null || head.Identity is null || head.Identity.TenantId != scope.Tenant
                || head.Identity.Domain != scope.Domain || head.Head < 0 || head.Head > MaxEvents - total
                || owned.Any(other => other.Identity == head.Identity))
            { return null; }
            total += head.Head;
            owned.Add(head);
        }
        return cut with { Sources = Array.AsReadOnly(owned.OrderBy(head => head.Identity.ActorId, StringComparer.Ordinal).ToArray()) };
    }

    private static bool SameCut(SourcePublicationCut first, SourcePublicationCut last)
        => first.Scope == last.Scope && first.AuthorityRevision == last.AuthorityRevision
            && first.Sources.SequenceEqual(last.Sources);

    private static EventStorePayloadProtectionMetadata? CaptureProtection(
        EventStorePayloadProtectionMetadata? metadata, AuthoritativeStreamReadDeadline deadline)
    {
        if (metadata is null) { return null; }
        IReadOnlyDictionary<string, string>? flags = null;
        if (metadata.CompatibilityFlags is { } supplied)
        {
            if (supplied.Count < 0 || supplied.Count > EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount)
            { throw new InvalidOperationException("Malformed source protection flags."); }
            var owned = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in supplied)
            {
                deadline.ThrowIfCancellationRequested();
                if (owned.Count >= EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount
                    || pair.Key is null || pair.Value is null || !owned.TryAdd(pair.Key, pair.Value))
                { throw new InvalidOperationException("Malformed source protection flags."); }
            }
            flags = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(owned);
        }
        var captured = metadata with { CompatibilityFlags = flags };
        if (captured.State is not (PayloadProtectionState.Unprotected or PayloadProtectionState.Protected)
            || !EventStorePayloadProtectionMetadataCarrier.TryValidate(captured, out _))
        { throw new InvalidOperationException("Unreadable source protection metadata."); }
        return captured;
    }
}
