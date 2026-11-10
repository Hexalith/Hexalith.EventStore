// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.2-10.4, 14 and 15.
// Owner decision D1 (2026-11-10, spec-payload-protection-capacity-fixes): implementation rules under the unchanged authority above;
// record shapes, member order, key grammar, per-page sort order and uniqueness, the 256-entry cap and conditional page creation are unchanged.
//   1. Placement: an entry lives only on the bucket page that PayloadIndexPlacement derives from its codec identity and the head's
//      pageCount (linear hashing). Page 0 is created when pageCount is 0; otherwise adding entry n + 1 with n + 1 > 64 pageCount
//      splits bucket s = pageCount - 2^floor(log2 pageCount) into the conditionally created page pageCount.
//   2. Generations: every index mutation writes the head under its ETag with the next generation, so comparing the head binds the
//      whole index. Touched pages take that generation; untouched pages keep the generation of the head that last wrote them, which
//      is never above the head's. A page newer than the head being read, or a moved head ETag, restarts the read at most 3 times.
//      A page newer than an unchanged head, a misplaced or duplicate entry, a missing page or a count disagreement is corruption.
//   3. Membership: per-key work, including each rewrap, verifies only its own scope and inventory bucket pages. VerifyInventoryAsync
//      remains the whole-inventory check for rotation start and old-version retirement.
using System.Collections.ObjectModel;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Reads complete or point views of scoped and environment-wide indexes and builds head-plus-touched-page changes for atomic enlistment.</summary>
internal sealed class PayloadKeyIndexes(IPayloadProtectionStateStore store, TimeProvider? timeProvider = null)
{
    /// <summary>Gets the maximum restarts after a concurrent index commit before a read reports provider unavailability.</summary>
    internal const int Restarts = 3;
    /// <summary>Gets the most selected pages a point read preflights at once against the enclosing phase's read budget.</summary>
    internal const int PointReadChunk = 64;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Reads the scope head, every declared page and the head again, checking generations, placement and the entry count.</summary>
    internal async ValueTask<ScopeKeyIndexSnapshot> ReadScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        (PayloadProtectionStateValue<ScopeIndexHeadV1>? head, IReadOnlyDictionary<uint, PayloadProtectionStateValue<ScopeIndexPageV1>> pages)
            = await ReadIndexAsync(ScopeLayout(scope), null, cancellationToken).ConfigureAwait(false);
        return new(scope, head, pages.Values.ToList().AsReadOnly());
    }

    /// <summary>Reads only exact environment-wide KEK inventory keys; display fingerprints never address records.</summary>
    internal async ValueTask<KekKeyIndexSnapshot> ReadKekAsync(string backendDigest, string providerDigest, CancellationToken cancellationToken = default)
    {
        (PayloadProtectionStateValue<KekInventoryHeadV1>? head, IReadOnlyDictionary<uint, PayloadProtectionStateValue<KekInventoryPageV1>> pages)
            = await ReadIndexAsync(KekLayout(backendDigest, providerDigest), null, cancellationToken).ConfigureAwait(false);
        return new(backendDigest, providerDigest, head, pages.Values.ToList().AsReadOnly());
    }

    /// <summary>Reads the scope head, one entry's bucket page and the head again; inserting also reads the page a growth step splits.</summary>
    internal async ValueTask<ScopeIndexView> ReadScopeEntryAsync(string scope, string keyRef, uint dekVersion, bool inserting = false,
        CancellationToken cancellationToken = default)
    {
        string identity = PayloadProtectionStateCodec.ScopeEntryIdentity(keyRef, dekVersion);
        (PayloadProtectionStateValue<ScopeIndexHeadV1>? head, IReadOnlyDictionary<uint, PayloadProtectionStateValue<ScopeIndexPageV1>> pages)
            = await ReadIndexAsync(ScopeLayout(scope), PointPages([identity], inserting), cancellationToken).ConfigureAwait(false);
        return new(scope, head, pages);
    }

    /// <summary>Reads the inventory head, one entry's bucket page and the head again; inserting also reads the page a growth step splits.</summary>
    internal ValueTask<KekInventoryView> ReadInventoryEntryAsync(string backendDigest, string providerDigest, KekInventoryEntryV1 entry,
        bool inserting = false, CancellationToken cancellationToken = default)
        => ReadInventoryEntriesAsync(backendDigest, providerDigest, [entry], inserting, cancellationToken);

    /// <summary>Reads the inventory head, the distinct bucket pages of the given entries and the head again.</summary>
    internal async ValueTask<KekInventoryView> ReadInventoryEntriesAsync(string backendDigest, string providerDigest,
        IReadOnlyCollection<KekInventoryEntryV1> entries, bool inserting = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0) { throw Mismatch(); }
        string[] identities = [.. entries.Select(e => PayloadProtectionStateCodec.KekEntryIdentity(e.ScopeDigest, e.KeyRef, e.DekVersion))];
        (PayloadProtectionStateValue<KekInventoryHeadV1>? head, IReadOnlyDictionary<uint, PayloadProtectionStateValue<KekInventoryPageV1>> pages)
            = await ReadIndexAsync(KekLayout(backendDigest, providerDigest), PointPages(identities, inserting), cancellationToken).ConfigureAwait(false);
        return new(backendDigest, providerDigest, head, pages);
    }

    /// <summary>Reads one key's scope and inventory point views at its own addresses and verifies its membership in both.</summary>
    internal async ValueTask<(ScopeIndexView Scope, KekInventoryView Inventory)> ReadMembershipAsync(WrappedDekV1 key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ScopeIndexView scope = await ReadScopeEntryAsync(key.ScopeDigest, key.KeyRef, key.DekVersion, cancellationToken: cancellationToken).ConfigureAwait(false);
        KekInventoryView inventory = await ReadInventoryEntryAsync(PayloadProtectionStateKeys.Digest(key.BackendId), PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId),
            new(key.ScopeDigest, key.KeyRef, key.DekVersion), cancellationToken: cancellationToken).ConfigureAwait(false);
        VerifyMembership(key, scope, inventory);
        return (scope, inventory);
    }

    /// <summary>Requires exact matching membership in both directions, on each entry's bucket page, before a key is used or changed.</summary>
    internal static void VerifyMembership(WrappedDekV1 key, ScopeIndexView scope, KekInventoryView inventory)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(inventory);
        ScopeIndexEntryV1? entry = BucketPage(scope.Head?.Value.PageCount, scope.Pages,
            PayloadProtectionStateCodec.ScopeEntryIdentity(key.KeyRef, key.DekVersion))?.Value.Entries
            .SingleOrDefault(e => e.KeyRef == key.KeyRef && e.DekVersion == key.DekVersion);
        var inventoryEntry = new KekInventoryEntryV1(key.ScopeDigest, key.KeyRef, key.DekVersion);
        if (scope.ScopeDigest != key.ScopeDigest || entry is null || entry.State != key.State
            || entry.ProviderVersionDigest != PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId)
            || inventory.BackendDigest != PayloadProtectionStateKeys.Digest(key.BackendId)
            || inventory.ProviderDigest != entry.ProviderVersionDigest
            || BucketPage(inventory.Head?.Value.PageCount, inventory.Pages,
                PayloadProtectionStateCodec.KekEntryIdentity(key.ScopeDigest, key.KeyRef, key.DekVersion))?.Value.Entries.Contains(inventoryEntry) != true)
        {
            throw Mismatch();
        }
    }

    /// <summary>Returns unchanged-value conditions for the head and every page the view read.</summary>
    internal static IReadOnlyList<PayloadProtectionStateMutation> Conditions(ScopeIndexView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Head is null ? throw Mismatch() : [PayloadProtectionStateMutation.Compare(view.Head), .. view.Pages.Values.Select(PayloadProtectionStateMutation.Compare)];
    }

    /// <summary>Returns unchanged-value conditions for the head and every page the view read.</summary>
    internal static IReadOnlyList<PayloadProtectionStateMutation> Conditions(KekInventoryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Head is null ? throw Mismatch() : [PayloadProtectionStateMutation.Compare(view.Head), .. view.Pages.Values.Select(PayloadProtectionStateMutation.Compare)];
    }

    /// <summary>Creates or changes one exact scope membership, writing only the head and the touched pages.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> SetScopeEntry(ScopeIndexView current, ScopeIndexEntryV1 entry, bool creating)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(entry);
        return Plan(ScopeLayout(current.ScopeDigest), current.Head, current.Pages, entry, creating ? 1 : 0);
    }

    /// <summary>Adds or removes exact inventory membership, writing only the head and the touched pages; emptied pages remain addressed.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> SetInventoryEntry(KekInventoryView current, KekInventoryEntryV1 entry, bool adding)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(entry);
        return Plan(KekLayout(current.BackendDigest, current.ProviderDigest), current.Head, current.Pages, entry, adding ? 1 : -1);
    }

    /// <summary>Removes an exact scope entry only with the key deletion transaction; emptied pages retain their addresses.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> RemoveScopeEntry(ScopeIndexView current, ScopeIndexEntryV1 entry)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(entry);
        return Plan(ScopeLayout(current.ScopeDigest), current.Head, current.Pages, entry, -1);
    }

    private async ValueTask<(PayloadProtectionStateValue<THead>? Head, IReadOnlyDictionary<uint, PayloadProtectionStateValue<TPage>> Pages)> ReadIndexAsync<THead, TPage, TEntry>(
        PayloadIndexLayout<THead, TPage, TEntry> layout, Func<(ulong Generation, uint PageCount, ulong EntryCount), IReadOnlyCollection<uint>>? select,
        CancellationToken cancellationToken)
        where THead : class
        where TPage : class
        where TEntry : class
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long started = _clock.GetTimestamp();
        for (int attempt = 0; attempt <= Restarts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PayloadProtectionStateValue<THead>? head = await PayloadOperationBudget.ReadAsync<THead>(store, layout.HeadKey, cancellationToken).ConfigureAwait(false);
            if (head is null)
            {
                if (await PayloadOperationBudget.ReadAsync<TPage>(store, layout.PageKey(0), cancellationToken).ConfigureAwait(false) is not null)
                {
                    if (await PayloadOperationBudget.ReadAsync<THead>(store, layout.HeadKey, cancellationToken).ConfigureAwait(false) is not null) { continue; }
                    throw Mismatch();
                }
                return (null, ReadOnlyDictionary<uint, PayloadProtectionStateValue<TPage>>.Empty);
            }
            (ulong generation, uint pageCount, ulong entryCount) = layout.Head(head.Value);
            // A head that names another index, or declares more entries than its pages can hold, is checked again after confirmation.
            bool consistent = layout.OwnsHead(head.Value) && entryCount <= (ulong)pageCount * PayloadIndexPlacement.PageCapacity;
            IReadOnlyCollection<uint> selected = !consistent ? [] : select?.Invoke((generation, pageCount, entryCount)) ?? [];
            long planned = !consistent ? 0 : select is null ? pageCount : selected.Count;
            // A full read keeps one whole-index preflight; a point read preflights each chunk of selected pages before reading it.
            if (select is null || planned == 0) { PayloadOperationBudget.RequireReadCapacity((planned + 1) * PayloadProtectionStateCodec.MaximumBytes, planned + 1); }
            IEnumerable<uint> numbers = select is null ? Enumerable.Range(0, checked((int)planned)).Select(page => (uint)page) : selected;
            var pages = new SortedDictionary<uint, PayloadProtectionStateValue<TPage>>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            ulong count = 0;
            long position = 0;
            foreach (uint number in numbers)
            {
                if (select is not null && position % PointReadChunk == 0)
                {
                    long chunk = Math.Min(PointReadChunk, planned - position);
                    PayloadOperationBudget.RequireReadCapacity((chunk + 1) * PayloadProtectionStateCodec.MaximumBytes, chunk + 1);
                }
                position++;
                CheckBudget(started, cancellationToken);
                PayloadProtectionStateValue<TPage> page = await RequiredAsync<TPage>(layout.PageKey(number), cancellationToken).ConfigureAwait(false);
                if (!layout.OwnsPage(page.Value, number)) { throw Mismatch(); }
                (ulong pageGeneration, TEntry[] entries) = layout.Page(page.Value);
                // A page newer than this head is a concurrent commit unless the confirmed head is unchanged.
                if (pageGeneration > generation) { consistent = false; break; }
                foreach (TEntry entry in entries)
                {
                    count = checked(count + 1);
                    string identity = layout.Identity(entry);
                    if (!identities.Add(identity) || PayloadIndexPlacement.Bucket(identity, pageCount) != number) { consistent = false; }
                }
                pages.Add(number, page);
            }
            PayloadProtectionStateValue<THead> confirmed = await RequiredAsync<THead>(layout.HeadKey, cancellationToken).ConfigureAwait(false);
            if (head.ETag != confirmed.ETag || generation != layout.Head(confirmed.Value).Generation) { continue; }
            if (!consistent || (select is null && count != entryCount)) { throw Mismatch(); }
            CheckBudget(started, cancellationToken);
            return (confirmed, pages.AsReadOnly());
        }
        throw Unavailable();
    }

    private IReadOnlyList<PayloadProtectionStateMutation> Plan<THead, TPage, TEntry>(PayloadIndexLayout<THead, TPage, TEntry> layout,
        PayloadProtectionStateValue<THead>? head, IReadOnlyDictionary<uint, PayloadProtectionStateValue<TPage>> pages, TEntry entry, int change)
        where THead : class
        where TPage : class
        where TEntry : class
    {
        // change: 1 inserts an absent identity, 0 replaces a present identity, -1 removes this exact present entry.
        if (head is null && change != 1) { throw Mismatch(); }
        (ulong generation, uint pageCount, ulong count) = head is null ? (0UL, 0u, 0UL) : layout.Head(head.Value);
        string identity = layout.Identity(entry);
        ulong hash = PayloadIndexPlacement.Hash(identity);
        var touched = new SortedDictionary<uint, (List<TEntry> Entries, string? ETag)>();
        if (pageCount == 0)
        {
            if (change != 1) { throw Mismatch(); }
        }
        else
        {
            List<TEntry> bucket = Load(PayloadIndexPlacement.Bucket(hash, pageCount));
            int index = bucket.FindIndex(e => layout.Identity(e) == identity);
            if ((index >= 0) == (change == 1)) { throw Mismatch(); }
            if (change == 0) { bucket[index] = entry; }
            else if (change < 0)
            {
                if (count == 0 || !EqualityComparer<TEntry>.Default.Equals(bucket[index], entry)) { throw Mismatch(); }
                bucket.RemoveAt(index);
            }
        }
        uint nextPageCount = pageCount;
        if (change == 1)
        {
            if (PayloadIndexPlacement.Grows(pageCount, count))
            {
                nextPageCount = checked(pageCount + 1);
                if (pageCount == 0) { touched.Add(0, ([], null)); }
                else
                {
                    // Linear hashing remaps only bucket s: its entries stay on s or move to the new page P.
                    uint split = PayloadIndexPlacement.SplitPage(pageCount);
                    List<TEntry> source = Load(split);
                    string? splitETag = touched[split].ETag;
                    var kept = new List<TEntry>();
                    var moved = new List<TEntry>();
                    foreach (TEntry existing in source)
                    {
                        uint target = PayloadIndexPlacement.Bucket(layout.Identity(existing), nextPageCount);
                        if (target == split) { kept.Add(existing); }
                        else if (target == pageCount) { moved.Add(existing); }
                        else { throw Mismatch(); }
                    }
                    touched[split] = (kept, splitETag);
                    touched.Add(pageCount, (moved, null));
                }
            }
            Load(PayloadIndexPlacement.Bucket(hash, nextPageCount)).Add(entry);
        }
        ulong nextCount = change switch { 1 => checked(count + 1), -1 => count - 1, _ => count };
        if (nextCount > layout.MaximumEntries) { throw Capacity(); }
        ulong nextGeneration = checked(generation + 1);
        var mutations = new List<PayloadProtectionStateMutation>(touched.Count + 1)
        {
            PayloadProtectionStateMutation.Put(layout.CreateHead(nextGeneration, nextPageCount, nextCount, Now()), head?.ETag),
        };
        foreach ((uint number, (List<TEntry> entries, string? etag)) in touched)
        {
            // A full bucket is a bounded admission limit; the codec cap is never exceeded or rebalanced silently.
            if (entries.Count > PayloadIndexPlacement.PageCapacity) { throw Capacity(); }
            entries.Sort((a, b) => StringComparer.Ordinal.Compare(layout.Identity(a), layout.Identity(b)));
            mutations.Add(PayloadProtectionStateMutation.Put(layout.CreatePage(nextGeneration, number, [.. entries]), etag));
        }
        PayloadProtectionOperationalLimits.RequireTransactionCapacity(mutations);
        return mutations.AsReadOnly();

        List<TEntry> Load(uint number)
        {
            if (!touched.TryGetValue(number, out (List<TEntry> Entries, string? ETag) page))
            {
                // A plan never writes a page its view did not read under the confirmed head.
                PayloadProtectionStateValue<TPage> read = pages.TryGetValue(number, out PayloadProtectionStateValue<TPage>? value) ? value : throw Mismatch();
                page = ([.. layout.Page(read.Value).Entries], read.ETag);
                touched.Add(number, page);
            }
            return page.Entries;
        }
    }

    private static Func<(ulong Generation, uint PageCount, ulong EntryCount), IReadOnlyCollection<uint>> PointPages(IReadOnlyCollection<string> identities, bool inserting)
        => head =>
        {
            var numbers = new SortedSet<uint>();
            if (head.PageCount == 0) { return numbers; }
            foreach (string identity in identities) { _ = numbers.Add(PayloadIndexPlacement.Bucket(identity, head.PageCount)); }
            if (inserting && PayloadIndexPlacement.Grows(head.PageCount, head.EntryCount)) { _ = numbers.Add(PayloadIndexPlacement.SplitPage(head.PageCount)); }
            return numbers;
        };

    private static PayloadProtectionStateValue<TPage>? BucketPage<TPage>(uint? pageCount, IReadOnlyDictionary<uint, PayloadProtectionStateValue<TPage>> pages, string identity)
        where TPage : class
        => pageCount is null or 0 ? null
            : pages.TryGetValue(PayloadIndexPlacement.Bucket(identity, pageCount.Value), out PayloadProtectionStateValue<TPage>? page) ? page : throw Mismatch();

    private static PayloadIndexLayout<ScopeIndexHeadV1, ScopeIndexPageV1, ScopeIndexEntryV1> ScopeLayout(string scope) => new()
    {
        HeadKey = PayloadProtectionStateKeys.IndexHead(scope),
        PageKey = page => PayloadProtectionStateKeys.IndexPage(scope, page),
        OwnsHead = head => head.ScopeDigest == scope,
        Head = head => (head.Generation, head.PageCount, head.EntryCount),
        OwnsPage = (page, number) => page.ScopeDigest == scope && page.Page == number,
        Page = page => (page.Generation, page.Entries),
        Identity = entry => PayloadProtectionStateCodec.ScopeEntryIdentity(entry.KeyRef, entry.DekVersion),
        MaximumEntries = uint.MaxValue,
        CreateHead = (generation, pages, count, now) => new ScopeIndexHeadV1(scope, generation, pages, checked((uint)count), now),
        CreatePage = (generation, page, entries) => new ScopeIndexPageV1(scope, generation, page, entries),
    };

    private static PayloadIndexLayout<KekInventoryHeadV1, KekInventoryPageV1, KekInventoryEntryV1> KekLayout(string backend, string provider) => new()
    {
        HeadKey = PayloadProtectionStateKeys.KekHead(backend, provider),
        PageKey = page => PayloadProtectionStateKeys.KekPage(backend, provider, page),
        OwnsHead = head => head.BackendIdDigest == backend && head.ProviderVersionDigest == provider,
        Head = head => (head.Generation, head.PageCount, head.EntryCount),
        OwnsPage = (page, number) => page.BackendIdDigest == backend && page.ProviderVersionDigest == provider && page.Page == number,
        Page = page => (page.Generation, page.Entries),
        Identity = entry => PayloadProtectionStateCodec.KekEntryIdentity(entry.ScopeDigest, entry.KeyRef, entry.DekVersion),
        MaximumEntries = ulong.MaxValue,
        CreateHead = (generation, pages, count, now) => new KekInventoryHeadV1(backend, provider, generation, pages, count, now),
        CreatePage = (generation, page, entries) => new KekInventoryPageV1(backend, provider, generation, page, entries),
    };

    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => await PayloadOperationBudget.ReadAsync<T>(store, key, cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
    private string Now() => PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
    private void CheckBudget(long started, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { throw Unavailable(); }
    }
    private static PayloadProtectionStateException Capacity() => new(UnreadableProtectedDataReason.ProviderUnavailable, isAdmissionLimit: true);
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
    private static PayloadProtectionStateException Unavailable() => new(UnreadableProtectedDataReason.ProviderUnavailable);
}
