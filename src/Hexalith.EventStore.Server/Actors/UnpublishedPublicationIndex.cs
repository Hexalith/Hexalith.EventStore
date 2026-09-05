namespace Hexalith.EventStore.Server.Actors;

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

    /// <summary>
    /// Gets the number of distinct, well-formed publication-recovery owners represented by the
    /// normalized index.
    /// </summary>
    public int OwnerCount => Entries.Count(entry => entry.IsWellFormed);

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

        if (OwnerCount >= maxEntries) {
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

        var remaining = Entries
            .Where(entry => !string.Equals(entry.MessageId, messageId, StringComparison.Ordinal))
            .ToList();
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

        var owners = new Dictionary<string, UnpublishedPublicationEntry>(StringComparer.Ordinal);
        foreach (UnpublishedPublicationEntry? entry in entries)
        {
            if (entry is null || !entry.IsWellFormed)
            {
                continue;
            }

            if (owners.TryGetValue(entry.MessageId, out UnpublishedPublicationEntry? owner))
            {
                if (!string.Equals(owner.CorrelationId, entry.CorrelationId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Publication recovery index contains conflicting owners for one message id.");
                }

                continue;
            }

            owners.Add(entry.MessageId, entry);
        }

        var normalized = new List<UnpublishedPublicationEntry>(entries.Count);
        var emittedOwners = new HashSet<string>(StringComparer.Ordinal);
        foreach (UnpublishedPublicationEntry? entry in entries)
        {
            if (entry is null)
            {
                continue;
            }

            if (entry.IsWellFormed)
            {
                if (emittedOwners.Add(entry.MessageId))
                {
                    normalized.Add(entry);
                }

                continue;
            }

            // A well-formed owner is authoritative over every malformed historical remnant that
            // shares its message id. Blank or otherwise ownerless entries remain durable for the
            // bounded activation repair path.
            if (string.IsNullOrWhiteSpace(entry.MessageId) || !owners.ContainsKey(entry.MessageId))
            {
                normalized.Add(entry);
            }
        }

        return normalized;
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
