// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.2-10.4, 14 and 15.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Verifies complete scoped and environment-wide indexes and builds all-page generation changes for atomic enlistment.</summary>
internal sealed class PayloadKeyIndexes(IPayloadProtectionStateStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Reads only exact scope head/page keys and confirms an unchanged head after enumeration.</summary>
    internal async ValueTask<ScopeKeyIndexSnapshot> ReadScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long started = _clock.GetTimestamp();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PayloadProtectionStateValue<ScopeIndexHeadV1>? head = await PayloadOperationBudget.ReadAsync<ScopeIndexHeadV1>(store, PayloadProtectionStateKeys.IndexHead(scope), cancellationToken).ConfigureAwait(false);
            if (head is null)
            {
                if (await PayloadOperationBudget.ReadAsync<ScopeIndexPageV1>(store, PayloadProtectionStateKeys.IndexPage(scope, 0), cancellationToken).ConfigureAwait(false) is not null) { throw Mismatch(); }
                return new(scope, null, []);
            }
            PayloadOperationBudget.RequireReadCapacity(((long)head.Value.PageCount + 1) * PayloadProtectionStateCodec.MaximumBytes, (long)head.Value.PageCount + 1);
            var pages = new List<PayloadProtectionStateValue<ScopeIndexPageV1>>();
            var pairs = new HashSet<(string Key, uint Version)>();
            uint count = 0;
            for (uint page = 0; page < head.Value.PageCount; page++)
            {
                CheckBudget(started, cancellationToken);
                PayloadProtectionStateValue<ScopeIndexPageV1> value = await RequiredAsync<ScopeIndexPageV1>(PayloadProtectionStateKeys.IndexPage(scope, page), cancellationToken).ConfigureAwait(false);
                if (value.Value.ScopeDigest != scope || value.Value.Page != page || value.Value.Generation != head.Value.Generation) { throw Mismatch(); }
                foreach (ScopeIndexEntryV1 entry in value.Value.Entries)
                {
                    count = checked(count + 1);
                    if (!pairs.Add((entry.KeyRef, entry.DekVersion))) { throw Mismatch(); }
                }
                pages.Add(value);
            }
            PayloadProtectionStateValue<ScopeIndexHeadV1> confirmed = await RequiredAsync<ScopeIndexHeadV1>(PayloadProtectionStateKeys.IndexHead(scope), cancellationToken).ConfigureAwait(false);
            if (head.ETag != confirmed.ETag || head.Value.Generation != confirmed.Value.Generation) { continue; }
            if (head.Value.ScopeDigest != scope || head.Value.EntryCount != count) { throw Mismatch(); }
            CheckBudget(started, cancellationToken);
            return new(scope, confirmed, pages.AsReadOnly());
        }
        throw Unavailable();
    }

    /// <summary>Reads only exact environment-wide KEK inventory keys; display fingerprints never address records.</summary>
    internal async ValueTask<KekKeyIndexSnapshot> ReadKekAsync(string backendDigest, string providerDigest, CancellationToken cancellationToken = default)
    {
        using var operationBudget = new PayloadOperationBudget(_clock, cancellationToken);
        long started = _clock.GetTimestamp();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PayloadProtectionStateValue<KekInventoryHeadV1>? head = await PayloadOperationBudget.ReadAsync<KekInventoryHeadV1>(store, PayloadProtectionStateKeys.KekHead(backendDigest, providerDigest), cancellationToken).ConfigureAwait(false);
            if (head is null)
            {
                if (await PayloadOperationBudget.ReadAsync<KekInventoryPageV1>(store, PayloadProtectionStateKeys.KekPage(backendDigest, providerDigest, 0), cancellationToken).ConfigureAwait(false) is not null) { throw Mismatch(); }
                return new(backendDigest, providerDigest, null, []);
            }
            PayloadOperationBudget.RequireReadCapacity(((long)head.Value.PageCount + 1) * PayloadProtectionStateCodec.MaximumBytes, (long)head.Value.PageCount + 1);
            var pages = new List<PayloadProtectionStateValue<KekInventoryPageV1>>();
            var pairs = new HashSet<(string Scope, string Key, uint Version)>();
            ulong count = 0;
            for (uint page = 0; page < head.Value.PageCount; page++)
            {
                CheckBudget(started, cancellationToken);
                PayloadProtectionStateValue<KekInventoryPageV1> value = await RequiredAsync<KekInventoryPageV1>(PayloadProtectionStateKeys.KekPage(backendDigest, providerDigest, page), cancellationToken).ConfigureAwait(false);
                if (value.Value.BackendIdDigest != backendDigest || value.Value.ProviderVersionDigest != providerDigest
                    || value.Value.Page != page || value.Value.Generation != head.Value.Generation) { throw Mismatch(); }
                foreach (KekInventoryEntryV1 entry in value.Value.Entries)
                {
                    count = checked(count + 1);
                    if (!pairs.Add((entry.ScopeDigest, entry.KeyRef, entry.DekVersion))) { throw Mismatch(); }
                }
                pages.Add(value);
            }
            PayloadProtectionStateValue<KekInventoryHeadV1> confirmed = await RequiredAsync<KekInventoryHeadV1>(PayloadProtectionStateKeys.KekHead(backendDigest, providerDigest), cancellationToken).ConfigureAwait(false);
            if (head.ETag != confirmed.ETag || head.Value.Generation != confirmed.Value.Generation) { continue; }
            if (head.Value.BackendIdDigest != backendDigest || head.Value.ProviderVersionDigest != providerDigest || head.Value.EntryCount != count) { throw Mismatch(); }
            CheckBudget(started, cancellationToken);
            return new(backendDigest, providerDigest, confirmed, pages.AsReadOnly());
        }
        throw Unavailable();
    }

    /// <summary>Requires exact matching membership in both directions before a key is used or changed.</summary>
    internal static void VerifyMembership(WrappedDekV1 key, ScopeKeyIndexSnapshot scope, KekKeyIndexSnapshot inventory)
    {
        ScopeIndexEntryV1? entry = scope.Pages.SelectMany(p => p.Value.Entries).SingleOrDefault(e => e.KeyRef == key.KeyRef && e.DekVersion == key.DekVersion);
        if (scope.ScopeDigest != key.ScopeDigest || entry is null || entry.State != key.State
            || entry.ProviderVersionDigest != PayloadProtectionStateKeys.Digest(key.ProviderKeyVersionId)
            || inventory.BackendDigest != PayloadProtectionStateKeys.Digest(key.BackendId)
            || inventory.ProviderDigest != entry.ProviderVersionDigest
            || !inventory.Pages.SelectMany(p => p.Value.Entries).Contains(new KekInventoryEntryV1(key.ScopeDigest, key.KeyRef, key.DekVersion))) { throw Mismatch(); }
    }

    /// <summary>Creates or changes one exact scope membership while preserving every other indexed record.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> SetScopeEntry(ScopeKeyIndexSnapshot current, ScopeIndexEntryV1 entry, bool creating)
    {
        var entries = current.Pages.SelectMany(p => p.Value.Entries).ToList();
        int index = entries.FindIndex(e => e.KeyRef == entry.KeyRef && e.DekVersion == entry.DekVersion);
        if ((index < 0) != creating) { throw Mismatch(); }
        if (creating) { entries.Add(entry); } else { entries[index] = entry; }
        entries.Sort((a, b) => { int key = StringComparer.Ordinal.Compare(a.KeyRef, b.KeyRef); return key != 0 ? key : a.DekVersion.CompareTo(b.DekVersion); });
        ulong generation = checked((current.Head?.Value.Generation ?? 0) + 1);
        uint pages = Math.Max(current.Head?.Value.PageCount ?? 0, checked((uint)((entries.Count + 255L) / 256)));
        var mutations = new List<PayloadProtectionStateMutation>
        {
            PayloadProtectionStateMutation.Put(new ScopeIndexHeadV1(current.ScopeDigest, generation, pages, checked((uint)entries.Count), Now()), current.Head?.ETag),
        };
        for (uint page = 0; page < pages; page++)
        {
            mutations.Add(PayloadProtectionStateMutation.Put(new ScopeIndexPageV1(current.ScopeDigest, generation, page,
                entries.Skip(checked((int)page * 256)).Take(256).ToArray()), page < current.Pages.Count ? current.Pages[(int)page].ETag : null));
        }
        PayloadProtectionOperationalLimits.RequireTransactionCapacity(mutations);
        return mutations.AsReadOnly();
    }

    /// <summary>Adds/removes exact inventory membership; trailing empty pages remain addressed and generation-consistent.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> SetInventoryEntry(KekKeyIndexSnapshot current, KekInventoryEntryV1 entry, bool adding)
    {
        var entries = current.Pages.SelectMany(p => p.Value.Entries).ToList();
        bool exists = entries.Contains(entry);
        if (exists == adding) { throw Mismatch(); }
        if (adding) { entries.Add(entry); } else { entries.Remove(entry); }
        entries.Sort((a, b) => { int scope = StringComparer.Ordinal.Compare(a.ScopeDigest, b.ScopeDigest); if (scope != 0) { return scope; }
            int key = StringComparer.Ordinal.Compare(a.KeyRef, b.KeyRef); return key != 0 ? key : a.DekVersion.CompareTo(b.DekVersion); });
        ulong generation = checked((current.Head?.Value.Generation ?? 0) + 1);
        uint pages = Math.Max(current.Head?.Value.PageCount ?? 0, checked((uint)((entries.Count + 255L) / 256)));
        var mutations = new List<PayloadProtectionStateMutation>
        {
            PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(current.BackendDigest, current.ProviderDigest, generation, pages, checked((ulong)entries.Count), Now()), current.Head?.ETag),
        };
        for (uint page = 0; page < pages; page++)
        {
            mutations.Add(PayloadProtectionStateMutation.Put(new KekInventoryPageV1(current.BackendDigest, current.ProviderDigest, generation, page,
                entries.Skip(checked((int)page * 256)).Take(256).ToArray()), page < current.Pages.Count ? current.Pages[(int)page].ETag : null));
        }
        PayloadProtectionOperationalLimits.RequireTransactionCapacity(mutations);
        return mutations.AsReadOnly();
    }

    /// <summary>Removes an exact scope entry only with the key deletion transaction; empty pages retain their addresses.</summary>
    internal IReadOnlyList<PayloadProtectionStateMutation> RemoveScopeEntry(ScopeKeyIndexSnapshot current, ScopeIndexEntryV1 entry)
    {
        var entries = current.Pages.SelectMany(p => p.Value.Entries).ToList();
        if (current.Head is null || !entries.Remove(entry)) { throw Mismatch(); }
        ulong generation = checked(current.Head.Value.Generation + 1);
        var mutations = new List<PayloadProtectionStateMutation>
        {
            PayloadProtectionStateMutation.Put(current.Head.Value with { Generation = generation, EntryCount = checked((uint)entries.Count), UpdatedAtUtc = Now() }, current.Head.ETag),
        };
        for (uint page = 0; page < current.Head.Value.PageCount; page++)
        {
            mutations.Add(PayloadProtectionStateMutation.Put(new ScopeIndexPageV1(current.ScopeDigest, generation, page,
                entries.Skip(checked((int)page * 256)).Take(256).ToArray()), current.Pages[(int)page].ETag));
        }
        PayloadProtectionOperationalLimits.RequireTransactionCapacity(mutations);
        return mutations;
    }

    private async ValueTask<PayloadProtectionStateValue<T>> RequiredAsync<T>(string key, CancellationToken cancellationToken) where T : class
        => await PayloadOperationBudget.ReadAsync<T>(store, key, cancellationToken).ConfigureAwait(false) ?? throw Mismatch();
    private string Now() => PayloadProtectionStateCodec.Timestamp(_clock.GetUtcNow());
    private void CheckBudget(long started, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_clock.GetElapsedTime(started) >= TimeSpan.FromSeconds(10)) { throw Unavailable(); }
    }
    private static PayloadProtectionStateException Mismatch() => new(UnreadableProtectedDataReason.ConsistencyMismatch);
    private static PayloadProtectionStateException Unavailable() => new(UnreadableProtectedDataReason.ProviderUnavailable);
}
