namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Seeds hash-placed index records directly, isolating capacity and layout tests from reservation traffic.</summary>
internal static class IndexSeed
{
    /// <summary>Gets the fixed seed timestamp.</summary>
    internal const string Timestamp = "2026-11-10T00:00:00.0000000Z";

    /// <summary>Creates synthetic inventory memberships of another scope with distinct canonical key references.</summary>
    internal static KekInventoryEntryV1[] Entries(int count, string? scope = null, int offset = 0)
        => [.. Enumerable.Range(offset, count).Select(i => new KekInventoryEntryV1(scope ?? new string('c', 64), KeyRef(i), 1))];

    /// <summary>Formats a distinct canonical key reference.</summary>
    internal static string KeyRef(int index) => "01K" + index.ToString("D23", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Gets an inventory entry's codec identity.</summary>
    internal static string Identity(KekInventoryEntryV1 entry) => PayloadProtectionStateCodec.KekEntryIdentity(entry.ScopeDigest, entry.KeyRef, entry.DekVersion);

    /// <summary>Gets a scope entry's codec identity.</summary>
    internal static string Identity(ScopeIndexEntryV1 entry) => PayloadProtectionStateCodec.ScopeEntryIdentity(entry.KeyRef, entry.DekVersion);

    /// <summary>Builds a placed inventory head and every page at one generation.</summary>
    internal static List<PayloadProtectionStateMutation> Inventory(string backend, string provider, uint pageCount, IReadOnlyCollection<KekInventoryEntryV1> entries, ulong generation = 1)
    {
        List<KekInventoryEntryV1>[] pages = Place(pageCount, entries, Identity);
        var mutations = new List<PayloadProtectionStateMutation> { PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(backend, provider, generation, pageCount, (ulong)entries.Count, Timestamp)) };
        mutations.AddRange(pages.Select((page, number) => PayloadProtectionStateMutation.Put(new KekInventoryPageV1(backend, provider, generation, (uint)number, [.. page]))));
        return mutations;
    }

    /// <summary>Builds a placed scope head and every page at one generation.</summary>
    internal static List<PayloadProtectionStateMutation> Scope(string scope, uint pageCount, IReadOnlyCollection<ScopeIndexEntryV1> entries, ulong generation = 1)
    {
        List<ScopeIndexEntryV1>[] pages = Place(pageCount, entries, Identity);
        var mutations = new List<PayloadProtectionStateMutation> { PayloadProtectionStateMutation.Put(new ScopeIndexHeadV1(scope, generation, pageCount, (uint)entries.Count, Timestamp)) };
        mutations.AddRange(pages.Select((page, number) => PayloadProtectionStateMutation.Put(new ScopeIndexPageV1(scope, generation, (uint)number, [.. page]))));
        return mutations;
    }

    /// <summary>Returns the exact page addresses of one inventory, split into its head and page prefixes.</summary>
    internal static (string Head, string PagePrefix) InventoryKeys(string backend, string provider)
        => (PayloadProtectionStateKeys.KekHead(backend, provider), PayloadProtectionStateKeys.KekPage(backend, provider, 0)[..^10]);

    /// <summary>Creates a complete view directly from seeded head and page mutations.</summary>
    internal static KekInventoryView InventoryView(IReadOnlyList<PayloadProtectionStateMutation> seed)
    {
        KekInventoryHeadV1 head = PayloadProtectionStateCodec.Decode<KekInventoryHeadV1>(seed[0].Bytes!);
        return new(head.BackendIdDigest, head.ProviderVersionDigest, new(head, "1", seed[0].Bytes!),
            seed.Skip(1).Select(m => new PayloadProtectionStateValue<KekInventoryPageV1>(PayloadProtectionStateCodec.Decode<KekInventoryPageV1>(m.Bytes!), "1", m.Bytes!))
                .ToDictionary(p => p.Value.Page));
    }

    /// <summary>Creates a complete view directly from seeded head and page mutations.</summary>
    internal static ScopeIndexView ScopeView(IReadOnlyList<PayloadProtectionStateMutation> seed)
    {
        ScopeIndexHeadV1 head = PayloadProtectionStateCodec.Decode<ScopeIndexHeadV1>(seed[0].Bytes!);
        return new(head.ScopeDigest, new(head, "1", seed[0].Bytes!),
            seed.Skip(1).Select(m => new PayloadProtectionStateValue<ScopeIndexPageV1>(PayloadProtectionStateCodec.Decode<ScopeIndexPageV1>(m.Bytes!), "1", m.Bytes!))
                .ToDictionary(p => p.Value.Page));
    }

    private static List<T>[] Place<T>(uint pageCount, IReadOnlyCollection<T> entries, Func<T, string> identity)
    {
        List<T>[] pages = [.. Enumerable.Range(0, (int)pageCount).Select(_ => new List<T>())];
        foreach (T entry in entries) { pages[PayloadIndexPlacement.Bucket(identity(entry), pageCount)].Add(entry); }
        foreach (List<T> page in pages) { page.Sort((a, b) => StringComparer.Ordinal.Compare(identity(a), identity(b))); }
        return pages;
    }
}
