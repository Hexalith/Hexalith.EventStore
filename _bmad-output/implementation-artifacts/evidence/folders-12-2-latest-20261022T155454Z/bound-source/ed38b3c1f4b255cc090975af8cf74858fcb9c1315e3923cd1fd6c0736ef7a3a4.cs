using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Ordered reconnect/backfill runtime glue using the immutable index and authenticated persisted per-publication source acknowledgements as its durable checkpoint.</summary>
/// <remarks>Each pass resumes only an independently authenticated durable consecutive original prefix for its exact complete cut and current delivery binding; unavailable proof starts at zero. No process memory, target rollover or wake-up can skip an unknown/quarantined publication.
/// Physical host installation, namespace coverage, receiver/current private credentials and backend qualification remain separate requirements.</remarks>
public sealed class SourcePublicationDispatcher(SourcePublicationFeed feed, TimeProvider clock, ISourcePublicationDelivery? delivery = null)
{
    /// <summary>Independently resumes or reconstructs the consecutive acknowledged prefix, retaining bounded verified progress while attempting at most the bounded number of new deliveries.</summary>
    public async Task<SourcePublicationDispatchResult> DispatchAsync(SourcePublicationScope scope, int maximumCount = 10000, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(scope);
        if (maximumCount is < 1 or > 10000) { throw new ArgumentOutOfRangeException(nameof(maximumCount)); }
        if (delivery is null) { return new(0, false, "publication-delivery-unavailable"); }
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        long prefix = 0; int newWork = 0;
        try
        {
            var initial = await deadline.ReadAsync(token => feed.ReadAsync(scope, 0, 100, token)).ConfigureAwait(false);
            if (initial.Page is not { } initialPage) { return new(0, false, initial.FailureReason ?? "publication-feed-unavailable"); }
            var cut = initialPage.Checkpoint;
            var retained = await deadline.ReadAsync(token => feed.ReadDispatchProgressAsync(cut, token)).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            if (retained is not null)
            {
                if (retained.Scope != scope || retained.AcknowledgedPrefix < 0 || retained.AcknowledgedPrefix > cut.LastOffset
                    || retained.SourceAuthorityRevision != cut.AuthorityRevision || retained.LastOffset != cut.LastOffset) { return new(0, false, "publication-progress-invalid"); }
                prefix = retained.AcknowledgedPrefix;
            }
            while (true)
            {
                var read = await deadline.ReadAsync(token => feed.ReadIndexedPageAsync(cut, prefix, token)).ConfigureAwait(false);
                deadline.ThrowIfCancellationRequested();
                if (read.Page is not { } page) { return new(prefix, false, read.FailureReason ?? "publication-feed-unavailable"); }
                // Own the bounded page before any receiver effect. The concrete feed enforces immutable references and current complete source cuts.
                if (page.Checkpoint is null || page.Checkpoint.Scope != scope || page.Checkpoint.LastOffset < prefix || page.Entries is null
                    || page.Entries.Count > 100 || page.NextOffset < prefix || page.NextOffset > page.Checkpoint.LastOffset)
                { return new(prefix, false, "publication-page-invalid"); }
                var owned = new List<SourcePublicationIndexEntry>();
                foreach (var entry in page.Entries)
                {
                    deadline.ThrowIfCancellationRequested();
                    if (owned.Count >= 100 || entry is null || entry.Publication is null || entry.Offset != prefix + owned.Count + 1L)
                    { return new(prefix, false, "publication-page-gap"); }
                    owned.Add(entry);
                }
                long next = owned.Count == 0 ? prefix : owned[^1].Offset;
                if (next != page.NextOffset || page.HasMore != (next < page.Checkpoint.LastOffset) || owned.Count == 0 && page.HasMore)
                { return new(prefix, false, "publication-page-gap"); }
                foreach (var entry in owned)
                {
                    var outcome = await deadline.ReadAsync(token => delivery.LookupAcknowledgementAsync(entry, token)).ConfigureAwait(false);
                    deadline.ThrowIfCancellationRequested();
                    if (outcome == SourcePublicationDeliveryStatus.Pending)
                    {
                        if (newWork >= maximumCount) { return new(prefix, false, "publication-pass-bound"); }
                        newWork++;
                        outcome = await deadline.ReadAsync(token => delivery.DeliverAsync(entry, token)).ConfigureAwait(false);
                    }
                    deadline.ThrowIfCancellationRequested();
                    if (outcome != SourcePublicationDeliveryStatus.Acknowledged)
                    { return new(prefix, false, outcome == SourcePublicationDeliveryStatus.Quarantined ? "publication-delivery-quarantined" : "publication-delivery-unavailable"); }
                    long predecessor = prefix; prefix = entry.Offset;
                    var progress = await deadline.ReadAsync(token => feed.AdvanceDispatchProgressAsync(new(page.Checkpoint, predecessor, [entry]), token)).ConfigureAwait(false);
                    deadline.ThrowIfCancellationRequested();
                    if (progress is not null && (progress.Scope != scope || progress.AcknowledgedPrefix != prefix || progress.SourceAuthorityRevision != cut.AuthorityRevision || progress.LastOffset != cut.LastOffset))
                    { return new(predecessor, false, "publication-progress-invalid"); }
                }
                if (owned.Count == 0) { deadline.ThrowIfCancellationRequested(); return new(prefix, true, null); }

            }
        }
        catch (OperationCanceledException)
        { cancellationToken.ThrowIfCancellationRequested(); return new(prefix, false, deadline.IsExpired ? "publication-dispatch-time-bound-exceeded" : "publication-delivery-unavailable"); }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        { cancellationToken.ThrowIfCancellationRequested(); return new(prefix, false, "publication-delivery-unavailable"); }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); throw; }
    }
    /// <summary>Runs ordered passes after reconnect/wake-up with caller-provided polling interval; disabled/default authority remains unavailable and cancellation stops the loop.</summary>
    public async Task RunAsync(SourcePublicationScope scope, TimeSpan pollInterval, int maximumCount = 10000, CancellationToken cancellationToken = default)
    {
        if (pollInterval <= TimeSpan.Zero || pollInterval > TimeSpan.FromDays(1)) { throw new ArgumentOutOfRangeException(nameof(pollInterval)); }
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DispatchAsync(scope, maximumCount, cancellationToken).ConfigureAwait(false);
            await Task.Delay(pollInterval, clock, cancellationToken).ConfigureAwait(false);
        }
    }
}
