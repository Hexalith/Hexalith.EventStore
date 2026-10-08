using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Ordered reconnect/backfill runtime glue using the immutable index and authenticated persisted per-publication source acknowledgements as its durable checkpoint.</summary>
/// <remarks>Each pass starts at zero and proves a consecutive prefix. No process memory, target rollover or wake-up can skip an unknown/quarantined publication.
/// Physical host installation, namespace coverage, receiver/current private credentials and backend qualification remain separate requirements.</remarks>
public sealed class SourcePublicationDispatcher(SourcePublicationFeed feed, TimeProvider clock, ISourcePublicationDelivery? delivery = null)
{
    /// <summary>Reconstructs up to the caller's bounded pass size; completion releases only after every original in the observed cut is acknowledged.</summary>
    public async Task<SourcePublicationDispatchResult> DispatchAsync(SourcePublicationScope scope, int maximumCount = 10000, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(scope);
        if (maximumCount is < 1 or > 10000) { throw new ArgumentOutOfRangeException(nameof(maximumCount)); }
        if (delivery is null) { return new(0, false, "publication-delivery-unavailable"); }
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, cancellationToken, clock.GetTimestamp());
        long prefix = 0;
        try
        {
            while (true)
            {
                var read = await deadline.ReadAsync(token => feed.ReadAsync(scope, prefix, Math.Min(100, maximumCount - (int)prefix), token)).ConfigureAwait(false);
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
                    var outcome = await deadline.ReadAsync(token => delivery.DeliverAsync(entry, token)).ConfigureAwait(false);
                    deadline.ThrowIfCancellationRequested();
                    if (outcome != SourcePublicationDeliveryStatus.Acknowledged)
                    { return new(prefix, false, outcome == SourcePublicationDeliveryStatus.Quarantined ? "publication-delivery-quarantined" : "publication-delivery-unavailable"); }
                    prefix = entry.Offset;
                }
                if (!page.HasMore) { deadline.ThrowIfCancellationRequested(); return new(prefix, true, null); }
                if (prefix >= maximumCount) { return new(prefix, false, "publication-pass-bound"); }
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
