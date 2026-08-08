namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// One outstanding committed-but-unpublished command tracked by
/// <see cref="UnpublishedPublicationIndex"/>.
/// Entries stay deliberately minimal: the drain record (<see cref="UnpublishedEventsRecord"/>) or
/// the <see cref="PipelineState"/> checkpoint remains the source of truth for the persisted
/// sequence range. The entry only says <em>which</em> command still owes a publication and where
/// its checkpoint lives.
/// </summary>
/// <param name="MessageId">The command message identifier, which is also the drain tracking id.</param>
/// <param name="CorrelationId">The correlation identifier used to locate the pipeline checkpoint.</param>
/// <param name="CommittedAt">When the committing batch staged this entry.</param>
public record UnpublishedPublicationEntry(
    string MessageId,
    string CorrelationId,
    DateTimeOffset CommittedAt) {
    /// <summary>
    /// Gets a value indicating whether the entry still carries the two identities recovery needs.
    /// A malformed entry (blank message id or blank correlation id) can never be re-armed and must
    /// be pruned rather than skipped, because skipping it permanently consumes index capacity.
    /// </summary>
    public bool IsWellFormed
        => !string.IsNullOrWhiteSpace(MessageId) && !string.IsNullOrWhiteSpace(CorrelationId);
}

/// <summary>
/// Why an <see cref="UnpublishedPublicationIndex.TryAdd"/> call did not append a new entry.
/// The two refusal reasons are deliberately distinguished: at-capacity is an operational condition
/// the caller surfaces as backpressure, while an invalid entry is a data defect that must never be
/// reported as "too many pending commands" with an outstanding count far below the threshold.
/// </summary>
public enum PublicationIndexAddOutcome {
    /// <summary>The entry is tracked (newly appended, or an existing entry refreshed in place).</summary>
    Added = 1,

    /// <summary>The index already holds the configured maximum number of outstanding entries.</summary>
    AtCapacity = 2,

    /// <summary>The entry lacks the message id or correlation id recovery needs.</summary>
    InvalidEntry = 3,
}

/// <summary>
/// Story 4.4: a single fixed-key actor-state index of the commands whose events are committed but
/// not yet published.
/// <para>
/// <c>IActorStateManager</c> (Dapr.Actors 1.18.5) exposes no key-enumeration API and FR28 forbids
/// <c>DaprClient.QueryStateAsync</c> over actor state, so an activation hook can only read names it
/// already knows. One fixed key is therefore the minimum sufficient mechanism, and because it is
/// staged into the batch that already commits the events it becomes durable at exactly the instant
/// they do.
/// </para>
/// </summary>
/// <param name="Entries">The outstanding entries, de-duplicated by message id.</param>
public record UnpublishedPublicationIndex(IReadOnlyList<UnpublishedPublicationEntry> Entries) {
    /// <summary>The single fixed actor-state key holding the whole index.</summary>
    public const string StateKey = "publication-index";

    /// <summary>Gets an index with no outstanding entries.</summary>
    public static UnpublishedPublicationIndex Empty { get; } = new([]);

    /// <summary>
    /// Gets the outstanding entries. Normalized on construction so a persisted payload that
    /// deserializes with a null collection -- or with a null ELEMENT, e.g. <c>[null]</c> -- cannot
    /// make every later activation throw and leave the index permanently unrepairable.
    /// </summary>
    public IReadOnlyList<UnpublishedPublicationEntry> Entries { get; init; } = Normalize(Entries);

    /// <summary>Determines whether an entry for the supplied message id is already tracked.</summary>
    /// <param name="messageId">The command message identifier.</param>
    /// <returns><c>true</c> when the index already tracks that message id.</returns>
    public bool Contains(string messageId)
        => !string.IsNullOrWhiteSpace(messageId)
            && Entries.Any(e => string.Equals(e.MessageId, messageId, StringComparison.Ordinal));

    /// <summary>
    /// Attempts to add (or refresh) an entry, reporting refusal to the caller rather than
    /// silently dropping it. Refusal is what makes the capacity bound fail closed: the caller must
    /// not commit events it cannot record here.
    /// </summary>
    /// <param name="entry">The entry to add.</param>
    /// <param name="maxEntries">The configured maximum number of outstanding entries.</param>
    /// <param name="updated">The resulting index; the original instance when the add is refused.</param>
    /// <returns>Why the add succeeded or was refused; the two refusal reasons are distinct.</returns>
    public PublicationIndexAddOutcome TryAdd(
        UnpublishedPublicationEntry entry,
        int maxEntries,
        out UnpublishedPublicationIndex updated) {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.IsWellFormed) {
            updated = this;
            return PublicationIndexAddOutcome.InvalidEntry;
        }

        // De-duplicate by MessageId: a repeat of the same command refreshes its entry and never
        // consumes additional capacity.
        int existingIndex = IndexOf(entry.MessageId);
        if (existingIndex >= 0) {
            var replaced = new List<UnpublishedPublicationEntry>(Entries);
            replaced[existingIndex] = entry;
            updated = this with { Entries = replaced };
            return PublicationIndexAddOutcome.Added;
        }

        if (Entries.Count >= maxEntries) {
            updated = this;
            return PublicationIndexAddOutcome.AtCapacity;
        }

        updated = this with { Entries = [.. Entries, entry] };
        return PublicationIndexAddOutcome.Added;
    }

    /// <summary>Removes the entry for a message id, if present.</summary>
    /// <param name="messageId">The command message identifier.</param>
    /// <param name="updated">The resulting index; the original instance when nothing was removed.</param>
    /// <returns><c>true</c> when an entry was removed.</returns>
    public bool TryRemove(string messageId, out UnpublishedPublicationIndex updated) {
        int existingIndex = IndexOf(messageId);
        if (existingIndex < 0) {
            updated = this;
            return false;
        }

        var remaining = new List<UnpublishedPublicationEntry>(Entries);
        remaining.RemoveAt(existingIndex);
        updated = this with { Entries = remaining };
        return true;
    }

    /// <summary>Removes every entry whose message id appears in the supplied set.</summary>
    /// <param name="messageIds">The message identifiers to prune.</param>
    /// <returns>The pruned index.</returns>
    public UnpublishedPublicationIndex Prune(IReadOnlyCollection<string> messageIds) {
        ArgumentNullException.ThrowIfNull(messageIds);
        if (messageIds.Count == 0) {
            return this;
        }

        var pruned = new HashSet<string>(messageIds, StringComparer.Ordinal);
        return this with {
            Entries = [.. Entries.Where(e => !pruned.Contains(e.MessageId ?? string.Empty))],
        };
    }

    private static IReadOnlyList<UnpublishedPublicationEntry> Normalize(
        IReadOnlyList<UnpublishedPublicationEntry>? entries) {
        if (entries is null || entries.Count == 0) {
            return [];
        }

        return entries.Any(e => e is null)
            ? [.. entries.Where(e => e is not null)]
            : entries;
    }

    private int IndexOf(string messageId) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            return -1;
        }

        for (int i = 0; i < Entries.Count; i++) {
            if (string.Equals(Entries[i].MessageId, messageId, StringComparison.Ordinal)) {
                return i;
            }
        }

        return -1;
    }
}
