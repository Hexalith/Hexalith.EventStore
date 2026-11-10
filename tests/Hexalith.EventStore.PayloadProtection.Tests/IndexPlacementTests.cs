// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.2; owner decision D1 (2026-11-10).
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Hash-bucket placement, head-generation pages and head-plus-touched-page plans under the unchanged record layout.</summary>
public sealed class IndexPlacementTests
{
    private static readonly uint[] PageCounts = [1, 2, 3, 4, 5, 6, 7, 8, 100, 313];
    private static readonly string Scope = PayloadProtectionStateKeys.ScopeDigest(new("tenant-a", "parties", "party-01"));
    private static readonly string Backend = PayloadProtectionStateKeys.Digest("fixture-backend");
    private static readonly string Provider = PayloadProtectionStateKeys.Digest("fixture:version-01");

    [Theory]
    [InlineData("01J00000000000000000000003", 1u, 0x98c9eae8ff8f3fbaUL, new uint[] { 0, 0, 2, 2, 2, 2, 2, 2, 58, 186 })]
    [InlineData("01J00000000000000000000002", 1u, 0x7eea06f19d73e0edUL, new uint[] { 0, 1, 1, 1, 1, 5, 5, 5, 45, 237 })]
    [InlineData("01J00000000000000000000008", 1u, 0x20f41d131daee304UL, new uint[] { 0, 0, 0, 0, 4, 4, 4, 4, 4, 260 })]
    [InlineData("01J00000000000000000000007", 1u, 0x33e342230cf2ce06UL, new uint[] { 0, 0, 2, 2, 2, 2, 6, 6, 6, 6 })]
    [InlineData("7ZZZZZZZZZZZZZZZZZZZZZZZZZ", uint.MaxValue, 0x51f88b585697c0bcUL, new uint[] { 0, 0, 0, 0, 4, 4, 4, 4, 60, 188 })]
    public void GoldenScopePlacementPinsTheHashAndEveryBucket(string keyRef, uint version, ulong hash, uint[] buckets)
    {
        // Vectors were computed independently from SHA-256 over the codec identity "keyRef:dekVersion(D10)".
        string identity = PayloadProtectionStateCodec.ScopeEntryIdentity(keyRef, version);
        identity.ShouldBe(keyRef + ":" + version.ToString("D10", System.Globalization.CultureInfo.InvariantCulture));
        PayloadIndexPlacement.Hash(identity).ShouldBe(hash);
        PageCounts.Select(pages => PayloadIndexPlacement.Bucket(identity, pages)).ShouldBe(buckets);
    }

    [Theory]
    [InlineData("01J00000000000000000000003", 3u, 0x6d5d6cde048be90eUL, new uint[] { 0, 0, 2, 2, 2, 2, 6, 6, 14, 270 })]
    [InlineData("01J00000000000000000000008", 8u, 0xa449fb6826bbcf65UL, new uint[] { 0, 1, 1, 1, 1, 5, 5, 5, 37, 101 })]
    public void GoldenInventoryPlacementPinsTheHashAndEveryBucket(string keyRef, uint version, ulong hash, uint[] buckets)
    {
        string identity = PayloadProtectionStateCodec.KekEntryIdentity(new string('a', 64), keyRef, version);
        identity.ShouldBe(new string('a', 64) + ":" + keyRef + ":" + version.ToString("D10", System.Globalization.CultureInfo.InvariantCulture));
        PayloadIndexPlacement.Hash(identity).ShouldBe(hash);
        PageCounts.Select(pages => PayloadIndexPlacement.Bucket(identity, pages)).ShouldBe(buckets);
    }

    [Fact]
    public void LinearHashingRemapsOnlyTheSplitBucketAndGrowsAtAnAverageOfSixtyFourEntries()
    {
        // With three pages (L = 1, s = 1), a hash ending in binary 10 moves from bucket 0 to bucket 2; one ending in 01 stays in bucket 1.
        PayloadIndexPlacement.Bucket(0b10, 3).ShouldBe(2u);
        PayloadIndexPlacement.Bucket(0b00, 3).ShouldBe(0u);
        PayloadIndexPlacement.Bucket(0b01, 3).ShouldBe(1u);
        PayloadIndexPlacement.Bucket(0b11, 3).ShouldBe(1u);
        uint[] splits = [.. new uint[] { 1, 2, 3, 4, 5, 7, 8, 313 }.Select(PayloadIndexPlacement.SplitPage)];
        splits.ShouldBe([0u, 0u, 1u, 0u, 1u, 3u, 0u, 57u]);
        for (ulong hash = 0; hash < 4096; hash++)
        {
            for (uint pages = 1; pages < 40; pages++)
            {
                uint before = PayloadIndexPlacement.Bucket(hash, pages), after = PayloadIndexPlacement.Bucket(hash, pages + 1);
                if (before != PayloadIndexPlacement.SplitPage(pages)) { after.ShouldBe(before); }
                else { after.ShouldBeOneOf(before, pages); }
            }
        }
        PayloadIndexPlacement.Grows(0, 0).ShouldBeTrue();
        PayloadIndexPlacement.Grows(1, 63).ShouldBeFalse();
        PayloadIndexPlacement.Grows(1, 64).ShouldBeTrue();
        PayloadIndexPlacement.Grows(313, 20_000).ShouldBeFalse();
        (Should.Throw<PayloadProtectionStateException>(() => PayloadIndexPlacement.Bucket(1, 0))).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    [Fact]
    public async Task AddChangeAndRemoveWithoutGrowthWriteOnlyTheHeadAndTheEntryBucketPage()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        ScopeIndexEntryV1[] seeded = [.. IndexSeed.Entries(200).Select(e => new ScopeIndexEntryV1(e.KeyRef, 1, Provider, "Reserved"))];
        KekInventoryEntryV1[] memberships = IndexSeed.Entries(200, Scope);
        (await store.CommitAsync([.. IndexSeed.Scope(Scope, 4, seeded), .. IndexSeed.Inventory(Backend, Provider, 4, memberships)])).ShouldBe(PayloadStateCommitResult.Committed);
        // Point views and complete views plan the same head-plus-bucket-page change.
        var added = new ScopeIndexEntryV1(IndexSeed.KeyRef(900), 1, Provider, "Reserved");
        await AssertTouchedOnlyAsync(store, indexes, PayloadProtectionStateKeys.IndexPage(Scope, Bucket(added, 4)), async () =>
            indexes.SetScopeEntry(await indexes.ReadScopeEntryAsync(Scope, added.KeyRef, 1, inserting: true), added, true));
        ScopeIndexEntryV1 changed = seeded[17] with { State = "Active" };
        await AssertTouchedOnlyAsync(store, indexes, PayloadProtectionStateKeys.IndexPage(Scope, Bucket(changed, 4)), async () =>
            indexes.SetScopeEntry(await indexes.ReadScopeAsync(Scope), changed, false));
        await AssertTouchedOnlyAsync(store, indexes, PayloadProtectionStateKeys.IndexPage(Scope, Bucket(changed, 4)), async () =>
            indexes.RemoveScopeEntry(await indexes.ReadScopeEntryAsync(Scope, changed.KeyRef, 1), changed));
        var membership = new KekInventoryEntryV1(Scope, IndexSeed.KeyRef(900), 1);
        await AssertTouchedOnlyAsync(store, indexes, PayloadProtectionStateKeys.KekPage(Backend, Provider, Bucket(membership, 4)), async () =>
            indexes.SetInventoryEntry(await indexes.ReadKekAsync(Backend, Provider), membership, true));
        await AssertTouchedOnlyAsync(store, indexes, PayloadProtectionStateKeys.KekPage(Backend, Provider, Bucket(memberships[3], 4)), async () =>
            indexes.SetInventoryEntry(await indexes.ReadInventoryEntryAsync(Backend, Provider, memberships[3]), memberships[3], false));
        (await indexes.ReadScopeAsync(Scope)).Head!.Value.EntryCount.ShouldBe(200u);
        (await indexes.ReadKekAsync(Backend, Provider)).Head!.Value.EntryCount.ShouldBe(200ul);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GrowthWritesTheHeadSplitPageConditionallyCreatedNewPageAndADifferentTargetPage(bool targetInSplitBucket)
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        // 192 = 64 x 3 entries on three pages: the next insertion splits bucket s = 1 into the new page 3.
        ScopeIndexEntryV1[] seeded = [.. IndexSeed.Entries(192).Select(e => new ScopeIndexEntryV1(e.KeyRef, 1, Provider, "Reserved"))];
        await store.CommitAsync(IndexSeed.Scope(Scope, 3, seeded));
        ScopeIndexEntryV1 added = Enumerable.Range(1000, 64).Select(i => new ScopeIndexEntryV1(IndexSeed.KeyRef(i), 1, Provider, "Reserved"))
            .First(e => (Bucket(e, 3) == 1) == targetInSplitBucket);
        ScopeIndexView view = await indexes.ReadScopeEntryAsync(Scope, added.KeyRef, 1, inserting: true);
        view.Pages.Keys.ShouldBe(targetInSplitBucket ? [1u] : new[] { Bucket(added, 3), 1u }.Order().ToArray());
        IReadOnlyList<PayloadProtectionStateMutation> plan = indexes.SetScopeEntry(view, added, true);
        var expected = new List<string> { PayloadProtectionStateKeys.IndexHead(Scope), PayloadProtectionStateKeys.IndexPage(Scope, 1), PayloadProtectionStateKeys.IndexPage(Scope, 3) };
        if (!targetInSplitBucket) { expected.Add(PayloadProtectionStateKeys.IndexPage(Scope, Bucket(added, 4))); }
        plan.Select(m => m.Key).Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
        plan.Single(m => m.Key == PayloadProtectionStateKeys.IndexPage(Scope, 3)).ExpectedETag.ShouldBeNull();
        plan.Where(m => m.Key != PayloadProtectionStateKeys.IndexPage(Scope, 3)).ShouldAllBe(m => m.ExpectedETag != null);
        (await store.CommitAsync(plan)).ShouldBe(PayloadStateCommitResult.Committed);
        ScopeKeyIndexSnapshot grown = await indexes.ReadScopeAsync(Scope);
        grown.Head!.Value.PageCount.ShouldBe(4u); grown.Head.Value.EntryCount.ShouldBe(193u);
        grown.Pages[3].Value.Entries.ShouldNotBeEmpty();
        grown.Pages.ShouldAllBe(page => page.Value.Entries.All(e => Bucket(e, 4) == page.Value.Page));
        // A stale page at the new address makes the conditional creation fail as one transaction.
        var stale = new MemoryPayloadProtectionStateStore();
        await stale.CommitAsync([.. IndexSeed.Scope(Scope, 3, seeded), PayloadProtectionStateMutation.Put(new ScopeIndexPageV1(Scope, 1, 3, []))]);
        (await stale.CommitAsync(new PayloadKeyIndexes(stale).SetScopeEntry(await new PayloadKeyIndexes(stale).ReadScopeEntryAsync(Scope, added.KeyRef, 1, inserting: true), added, true)))
            .ShouldBe(PayloadStateCommitResult.Conflict);
    }

    [Fact]
    public async Task FirstInsertionConditionallyCreatesTheHeadAndPageZero()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        var entry = new KekInventoryEntryV1(Scope, IndexSeed.KeyRef(1), 1);
        KekInventoryView empty = await indexes.ReadInventoryEntryAsync(Backend, Provider, entry, inserting: true);
        empty.Head.ShouldBeNull(); empty.Pages.ShouldBeEmpty();
        IReadOnlyList<PayloadProtectionStateMutation> plan = indexes.SetInventoryEntry(empty, entry, true);
        plan.Select(m => m.Key).ShouldBe([PayloadProtectionStateKeys.KekHead(Backend, Provider), PayloadProtectionStateKeys.KekPage(Backend, Provider, 0)]);
        plan.ShouldAllBe(m => m.ExpectedETag == null);
        (await store.CommitAsync(plan)).ShouldBe(PayloadStateCommitResult.Committed);
        KekKeyIndexSnapshot created = await indexes.ReadKekAsync(Backend, Provider);
        created.Head!.Value.PageCount.ShouldBe(1u); created.Pages.Single().Value.Entries.ShouldBe([entry]);
    }

    [Fact]
    public async Task FiveThousandSequentialInsertionsStayPlacedCountedAndFindableAcrossEveryGrowthStep()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        uint pages = 0;
        int growthSteps = 0;
        for (int i = 0; i < 5000; i++)
        {
            string keyRef = IndexSeed.KeyRef(i);
            ScopeIndexView scope = await indexes.ReadScopeEntryAsync(Scope, keyRef, 1, inserting: true);
            KekInventoryView inventory = await indexes.ReadInventoryEntryAsync(Backend, Provider, new(Scope, keyRef, 1), inserting: true);
            (await store.CommitAsync([.. indexes.SetScopeEntry(scope, new(keyRef, 1, Provider, "Reserved"), true),
                .. indexes.SetInventoryEntry(inventory, new(Scope, keyRef, 1), true)])).ShouldBe(PayloadStateCommitResult.Committed);
            ScopeIndexHeadV1 head = (await store.ReadAsync<ScopeIndexHeadV1>(PayloadProtectionStateKeys.IndexHead(Scope)))!.Value;
            if (head.PageCount == pages) { continue; }
            pages = head.PageCount; growthSteps++;
            // Every full read at a growth step passes generation, placement, uniqueness and count checks.
            ScopeKeyIndexSnapshot fullScope = await indexes.ReadScopeAsync(Scope);
            KekKeyIndexSnapshot fullInventory = await indexes.ReadKekAsync(Backend, Provider);
            fullScope.Head!.Value.EntryCount.ShouldBe((uint)(i + 1)); fullInventory.Head!.Value.EntryCount.ShouldBe((ulong)(i + 1));
            fullScope.Pages.Count.ShouldBe((int)pages); fullInventory.Pages.Count.ShouldBe((int)pages);
        }
        pages.ShouldBe(79u); growthSteps.ShouldBe(79);
        ScopeKeyIndexSnapshot final = await indexes.ReadScopeAsync(Scope);
        final.Pages.Max(page => page.Value.Entries.Length).ShouldBeLessThan(PayloadIndexPlacement.PageCapacity);
        (await indexes.ReadKekAsync(Backend, Provider)).Head!.Value.EntryCount.ShouldBe(5000ul);
        for (int i = 0; i < 5000; i++)
        {
            string keyRef = IndexSeed.KeyRef(i);
            ScopeIndexView scope = await indexes.ReadScopeEntryAsync(Scope, keyRef, 1);
            scope.Pages.Values.Single().Value.Entries.ShouldContain(new ScopeIndexEntryV1(keyRef, 1, Provider, "Reserved"));
            KekInventoryView inventory = await indexes.ReadInventoryEntryAsync(Backend, Provider, new(Scope, keyRef, 1));
            inventory.Pages.Values.Single().Value.Entries.ShouldContain(new KekInventoryEntryV1(Scope, keyRef, 1));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MisplacedOrDuplicatedEntriesAreRejectedByEveryReadThatSeesThem(bool duplicate)
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        KekInventoryEntryV1[] entries = IndexSeed.Entries(100, Scope);
        List<PayloadProtectionStateMutation> seed = IndexSeed.Inventory(Backend, Provider, 4, entries);
        KekInventoryEntryV1 moved = entries.First(e => Bucket(e, 4) == 1);
        KekInventoryEntryV1 resident = entries.First(e => Bucket(e, 4) == 2);
        KekInventoryEntryV1 elsewhere = entries.First(e => Bucket(e, 4) == 3);
        KekInventoryPageV1 page1 = PayloadProtectionStateCodec.Decode<KekInventoryPageV1>(seed[2].Bytes!);
        KekInventoryPageV1 page2 = PayloadProtectionStateCodec.Decode<KekInventoryPageV1>(seed[3].Bytes!);
        // The moved entry also appears on page 2; without duplication it leaves its own bucket page 1.
        KekInventoryEntryV1[] corrupted = [.. page2.Entries.Append(moved).OrderBy(IndexSeed.Identity, StringComparer.Ordinal)];
        seed[3] = PayloadProtectionStateMutation.Put(page2 with { Entries = corrupted });
        if (!duplicate) { seed[2] = PayloadProtectionStateMutation.Put(page1 with { Entries = [.. page1.Entries.Where(e => e != moved)] }); }
        else { seed[0] = PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(Backend, Provider, 1, 4, 101, IndexSeed.Timestamp)); }
        await store.CommitAsync(seed);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => indexes.ReadKekAsync(Backend, Provider).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => indexes.ReadInventoryEntryAsync(Backend, Provider, resident).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        // A point read covers only its own bucket page, so an untouched page stays readable.
        (await indexes.ReadInventoryEntryAsync(Backend, Provider, elsewhere)).Pages.Keys.ShouldBe([3u]);
    }

    [Theory]
    [InlineData(false, 99ul)]
    [InlineData(false, 101ul)]
    [InlineData(true, 1025ul)]
    public async Task AHeadCountThatDisagreesWithItsPagesIsAMismatch(bool point, ulong declared)
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        KekInventoryEntryV1[] entries = IndexSeed.Entries(100, Scope);
        List<PayloadProtectionStateMutation> seed = IndexSeed.Inventory(Backend, Provider, 4, entries);
        // A point read cannot count every page, but no head may declare more entries than its pages can hold.
        seed[0] = PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(Backend, Provider, 1, 4, declared, IndexSeed.Timestamp));
        await store.CommitAsync(seed);
        Task Read() => point ? indexes.ReadInventoryEntryAsync(Backend, Provider, entries[0]).AsTask() : indexes.ReadKekAsync(Backend, Provider).AsTask();
        (await Should.ThrowAsync<PayloadProtectionStateException>(Read)).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PointReadsAcceptOlderPagesAndRejectAPageNewerThanAnUnchangedHead(bool inventory)
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        KekInventoryEntryV1[] entries = IndexSeed.Entries(100, Scope);
        await store.CommitAsync(inventory ? IndexSeed.Inventory(Backend, Provider, 4, entries, generation: 5)
            : IndexSeed.Scope(Scope, 4, [.. entries.Select(e => new ScopeIndexEntryV1(e.KeyRef, 1, Provider, "Reserved"))], generation: 5));
        KekInventoryEntryV1 entry = entries[0];
        uint bucket = inventory ? Bucket(entry, 4) : Bucket(new ScopeIndexEntryV1(entry.KeyRef, 1, Provider, "Reserved"), 4);
        async Task<int> ReadAsync() => inventory ? (await indexes.ReadInventoryEntryAsync(Backend, Provider, entry)).Pages.Count
            : (await indexes.ReadScopeEntryAsync(Scope, entry.KeyRef, 1)).Pages.Count;
        await RewritePageGenerationAsync(store, inventory, bucket, 2);
        (await ReadAsync()).ShouldBe(1);
        await RewritePageGenerationAsync(store, inventory, bucket, 6);
        (await Should.ThrowAsync<PayloadProtectionStateException>(ReadAsync)).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        if (inventory) { (await Should.ThrowAsync<PayloadProtectionStateException>(() => indexes.ReadKekAsync(Backend, Provider).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch); }
        else { (await Should.ThrowAsync<PayloadProtectionStateException>(() => indexes.ReadScopeAsync(Scope).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch); }
    }

    [Theory]
    [InlineData(false, 3, true)]
    [InlineData(false, 4, false)]
    [InlineData(true, 3, true)]
    [InlineData(true, 4, false)]
    public async Task ConcurrentCommitsRestartAReadAtMostThreeTimes(bool full, int disturbances, bool succeeds)
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        KekInventoryEntryV1[] entries = IndexSeed.Entries(100, Scope);
        await store.CommitAsync(IndexSeed.Inventory(Backend, Provider, 4, entries));
        string pageKey = PayloadProtectionStateKeys.KekPage(Backend, Provider, Bucket(entries[0], 4));
        int injected = 0;
        bool committing = false;
        store.Reading = key =>
        {
            if (committing || key != pageKey || injected == disturbances) { return; }
            injected++;
            committing = true;
            try
            {
                // Each concurrent commit adds one membership to the page being read, so that page is newer than this attempt's head.
                KekInventoryView current = indexes.ReadInventoryEntryAsync(Backend, Provider, entries[0]).AsTask().GetAwaiter().GetResult();
                store.CommitAsync(indexes.SetInventoryEntry(current, Enumerable.Range(5000, 500).Select(i => new KekInventoryEntryV1(Scope, IndexSeed.KeyRef(i), 1))
                    .First(e => Bucket(e, 4) == Bucket(entries[0], 4) && !current.Pages.Values.Single().Value.Entries.Contains(e)), true)).AsTask().GetAwaiter().GetResult()
                    .ShouldBe(PayloadStateCommitResult.Committed);
            }
            finally { committing = false; }
        };
        async Task<ulong> ReadAsync() => full ? (await indexes.ReadKekAsync(Backend, Provider)).Head!.Value.EntryCount
            : (await indexes.ReadInventoryEntryAsync(Backend, Provider, entries[0])).Head!.Value.EntryCount;
        if (succeeds) { (await ReadAsync()).ShouldBe(103ul); }
        else { (await Should.ThrowAsync<PayloadProtectionStateException>(ReadAsync)).Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable); }
        injected.ShouldBe(disturbances);
    }

    [Fact]
    public async Task ChangingOrRemovingAnAbsentEntryAndAddingAPresentOneAreMismatches()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        KekInventoryEntryV1[] entries = IndexSeed.Entries(100, Scope);
        ScopeIndexEntryV1[] scoped = [.. entries.Select(e => new ScopeIndexEntryV1(e.KeyRef, 1, Provider, "Reserved"))];
        await store.CommitAsync([.. IndexSeed.Scope(Scope, 2, scoped), .. IndexSeed.Inventory(Backend, Provider, 2, entries)]);
        var absent = new ScopeIndexEntryV1(IndexSeed.KeyRef(7777), 1, Provider, "Reserved");
        ScopeIndexView absentView = await indexes.ReadScopeEntryAsync(Scope, absent.KeyRef, 1);
        Mismatch(() => indexes.SetScopeEntry(absentView, absent, false));
        Mismatch(() => indexes.RemoveScopeEntry(absentView, absent));
        ScopeIndexView presentView = await indexes.ReadScopeEntryAsync(Scope, scoped[5].KeyRef, 1, inserting: true);
        Mismatch(() => indexes.SetScopeEntry(presentView, scoped[5], true));
        // Removal is exact: a stale state copy is not the indexed membership.
        Mismatch(() => indexes.RemoveScopeEntry(presentView, scoped[5] with { State = "Active" }));
        var membership = new KekInventoryEntryV1(Scope, absent.KeyRef, 1);
        Mismatch(() => indexes.SetInventoryEntry(indexes.ReadInventoryEntryAsync(Backend, Provider, membership).AsTask().GetAwaiter().GetResult(), membership, false));
        Mismatch(() => indexes.SetInventoryEntry(indexes.ReadInventoryEntryAsync(Backend, Provider, entries[5], inserting: true).AsTask().GetAwaiter().GetResult(), entries[5], true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFullBucketPageReturnsTheTypedAdmissionLimitWithoutRebalancing(bool inventory)
    {
        const uint pages = 8;
        string scope = new('c', 64);
        var added = new KekInventoryEntryV1(scope, IndexSeed.KeyRef(0), 1);
        uint bucket = Bucket(added, pages);
        // 256 memberships share the added entry's bucket; 257 entries on eight pages stay below the growth threshold.
        KekInventoryEntryV1[] full = [.. Enumerable.Range(1, 20_000).Select(i => new KekInventoryEntryV1(scope, IndexSeed.KeyRef(i), 1)).Where(e => Bucket(e, pages) == bucket).Take(256)];
        full.Length.ShouldBe(256);
        var indexes = new PayloadKeyIndexes(new MemoryPayloadProtectionStateStore());
        PayloadProtectionStateException failure;
        if (inventory)
        {
            KekInventoryView view = IndexSeed.InventoryView(IndexSeed.Inventory(Backend, Provider, pages, full));
            failure = Should.Throw<PayloadProtectionStateException>(() => indexes.SetInventoryEntry(view, added, true));
            view.Head!.Value.Generation.ShouldBe(1ul);
        }
        else
        {
            // The scope identity omits the scope digest, so the bucket is recomputed for scope entries.
            var scopedAdded = new ScopeIndexEntryV1(added.KeyRef, 1, Provider, "Reserved");
            uint scopeBucket = Bucket(scopedAdded, pages);
            ScopeIndexEntryV1[] scoped = [.. Enumerable.Range(1, 20_000).Select(i => new ScopeIndexEntryV1(IndexSeed.KeyRef(i), 1, Provider, "Reserved")).Where(e => Bucket(e, pages) == scopeBucket).Take(256)];
            ScopeIndexView view = IndexSeed.ScopeView(IndexSeed.Scope(Scope, pages, scoped));
            failure = Should.Throw<PayloadProtectionStateException>(() => indexes.SetScopeEntry(view, scopedAdded, true));
        }
        failure.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        failure.IsAdmissionLimit.ShouldBeTrue();
    }

    [Fact]
    public async Task PlansAndMembershipChecksNeverUsePagesTheirViewDidNotRead()
    {
        var store = new MemoryPayloadProtectionStateStore();
        var indexes = new PayloadKeyIndexes(store);
        KekInventoryEntryV1[] entries = IndexSeed.Entries(100, Scope);
        await store.CommitAsync(IndexSeed.Inventory(Backend, Provider, 4, entries));
        KekInventoryEntryV1 entry = entries.First(e => Bucket(e, 4) == 0);
        KekInventoryEntryV1 other = entries.First(e => Bucket(e, 4) == 2);
        KekInventoryView view = await indexes.ReadInventoryEntryAsync(Backend, Provider, entry);
        view.Pages.Keys.ShouldBe([0u]);
        Mismatch(() => indexes.SetInventoryEntry(view, other, false));
        KekInventoryView headOnly = view with { Pages = new Dictionary<uint, PayloadProtectionStateValue<KekInventoryPageV1>>() };
        Mismatch(() => indexes.SetInventoryEntry(headOnly, entry, false));
        // A growth insertion needs its split page in the view.
        var growthStore = new MemoryPayloadProtectionStateStore();
        await growthStore.CommitAsync(IndexSeed.Inventory(Backend, Provider, 3, IndexSeed.Entries(192, Scope)));
        var growing = new PayloadKeyIndexes(growthStore);
        KekInventoryEntryV1 outside = Enumerable.Range(3000, 64).Select(i => new KekInventoryEntryV1(Scope, IndexSeed.KeyRef(i), 1)).First(e => Bucket(e, 3) != 1);
        Mismatch(() => growing.SetInventoryEntry(growing.ReadInventoryEntryAsync(Backend, Provider, outside).AsTask().GetAwaiter().GetResult(), outside, true));
        growing.SetInventoryEntry(await growing.ReadInventoryEntryAsync(Backend, Provider, outside, inserting: true), outside, true).Count.ShouldBe(4);
    }

    [Fact]
    public void MembershipIsProvenOnlyOnTheEntryBucketPageOfItsView()
    {
        const string keyRef = "01J00000000000000000000731";
        WrappedDekV1 key = PayloadProtectionStateCodec.Seal(new("tenant-a", "parties", "party-01", Scope, keyRef, 1, "Reserved", "fixture-backend",
            "fixture:version-01", "RSA-OAEP-256", "AA", 1, 1, 1, "01J00000000000000000000732", IndexSeed.Timestamp, IndexSeed.Timestamp, new('0', 64), new('0', 64)));
        var entry = new ScopeIndexEntryV1(keyRef, 1, Provider, "Reserved");
        var membership = new KekInventoryEntryV1(Scope, keyRef, 1);
        ScopeIndexView scope = IndexSeed.ScopeView(IndexSeed.Scope(Scope, 4, [.. IndexSeed.Entries(100).Select(e => new ScopeIndexEntryV1(e.KeyRef, 1, Provider, "Reserved")), entry]));
        KekInventoryView inventory = IndexSeed.InventoryView(IndexSeed.Inventory(Backend, Provider, 4, [.. IndexSeed.Entries(100, Scope), membership]));
        PayloadKeyIndexes.VerifyMembership(key, scope, inventory);
        // A view holding the entry only on another page proves nothing: the bucket page is the sole evidence.
        uint wrongScopePage = (Bucket(entry, 4) + 1) % 4;
        ScopeIndexPageV1 misplacedScope = scope.Pages[wrongScopePage].Value with { Entries = [.. scope.Pages[wrongScopePage].Value.Entries.Append(entry).OrderBy(IndexSeed.Identity, StringComparer.Ordinal)] };
        ScopeIndexView misplacedScopeView = scope with { Pages = new Dictionary<uint, PayloadProtectionStateValue<ScopeIndexPageV1>> { [wrongScopePage] = new(misplacedScope, "1", PayloadProtectionStateCodec.Encode(misplacedScope)) } };
        Mismatch(() => { PayloadKeyIndexes.VerifyMembership(key, misplacedScopeView, inventory); return key; });
        uint wrongInventoryPage = (Bucket(membership, 4) + 1) % 4;
        KekInventoryPageV1 misplacedInventory = inventory.Pages[wrongInventoryPage].Value with { Entries = [.. inventory.Pages[wrongInventoryPage].Value.Entries.Append(membership).OrderBy(IndexSeed.Identity, StringComparer.Ordinal)] };
        KekInventoryView misplacedInventoryView = inventory with { Pages = new Dictionary<uint, PayloadProtectionStateValue<KekInventoryPageV1>> { [wrongInventoryPage] = new(misplacedInventory, "1", PayloadProtectionStateCodec.Encode(misplacedInventory)) } };
        Mismatch(() => { PayloadKeyIndexes.VerifyMembership(key, scope, misplacedInventoryView); return key; });
    }

    internal static uint Bucket(KekInventoryEntryV1 entry, uint pages) => PayloadIndexPlacement.Bucket(IndexSeed.Identity(entry), pages);
    internal static uint Bucket(ScopeIndexEntryV1 entry, uint pages) => PayloadIndexPlacement.Bucket(IndexSeed.Identity(entry), pages);

    private static async Task AssertTouchedOnlyAsync(MemoryPayloadProtectionStateStore store, PayloadKeyIndexes indexes, string touchedPage,
        Func<Task<IReadOnlyList<PayloadProtectionStateMutation>>> plan)
    {
        ScopeKeyIndexSnapshot scopeBefore = await indexes.ReadScopeAsync(Scope);
        KekKeyIndexSnapshot inventoryBefore = await indexes.ReadKekAsync(Backend, Provider);
        IReadOnlyList<PayloadProtectionStateMutation> mutations = await plan();
        string head = touchedPage.Contains(":kek-index:", StringComparison.Ordinal) ? PayloadProtectionStateKeys.KekHead(Backend, Provider) : PayloadProtectionStateKeys.IndexHead(Scope);
        mutations.Select(m => m.Key).ShouldBe([head, touchedPage]);
        mutations.ShouldAllBe(m => m.ExpectedETag != null);
        (await store.CommitAsync(mutations)).ShouldBe(PayloadStateCommitResult.Committed);
        ScopeKeyIndexSnapshot scopeAfter = await indexes.ReadScopeAsync(Scope);
        KekKeyIndexSnapshot inventoryAfter = await indexes.ReadKekAsync(Backend, Provider);
        var before = scopeBefore.Pages.Select(p => (Key: PayloadProtectionStateCodec.KeyFor(p.Value), p.ETag, Bytes: p.Bytes.ToArray()))
            .Concat(inventoryBefore.Pages.Select(p => (Key: PayloadProtectionStateCodec.KeyFor(p.Value), p.ETag, Bytes: p.Bytes.ToArray()))).ToArray();
        var after = scopeAfter.Pages.Select(p => (Key: PayloadProtectionStateCodec.KeyFor(p.Value), p.ETag, Bytes: p.Bytes.ToArray()))
            .Concat(inventoryAfter.Pages.Select(p => (Key: PayloadProtectionStateCodec.KeyFor(p.Value), p.ETag, Bytes: p.Bytes.ToArray()))).ToDictionary(p => p.Key);
        after.Count.ShouldBe(before.Length);
        foreach (var page in before.Where(p => p.Key != touchedPage))
        {
            after[page.Key].ETag.ShouldBe(page.ETag);
            after[page.Key].Bytes.ShouldBe(page.Bytes);
        }
        after[touchedPage].ETag.ShouldNotBe(before.Single(p => p.Key == touchedPage).ETag);
        // The touched page takes the new head generation; untouched pages keep the older generation that last wrote them.
        if (head == PayloadProtectionStateKeys.IndexHead(Scope))
        {
            ulong generation = scopeAfter.Head!.Value.Generation;
            generation.ShouldBe(scopeBefore.Head!.Value.Generation + 1);
            scopeAfter.Pages.ShouldAllBe(p => PayloadProtectionStateCodec.KeyFor(p.Value) == touchedPage ? p.Value.Generation == generation : p.Value.Generation < generation);
        }
        else
        {
            ulong generation = inventoryAfter.Head!.Value.Generation;
            generation.ShouldBe(inventoryBefore.Head!.Value.Generation + 1);
            inventoryAfter.Pages.ShouldAllBe(p => PayloadProtectionStateCodec.KeyFor(p.Value) == touchedPage ? p.Value.Generation == generation : p.Value.Generation < generation);
        }
    }

    private static async Task RewritePageGenerationAsync(MemoryPayloadProtectionStateStore store, bool inventory, uint page, ulong generation)
    {
        if (inventory)
        {
            var current = (await store.ReadAsync<KekInventoryPageV1>(PayloadProtectionStateKeys.KekPage(Backend, Provider, page)))!;
            (await store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { Generation = generation }, current.ETag)])).ShouldBe(PayloadStateCommitResult.Committed);
        }
        else
        {
            var current = (await store.ReadAsync<ScopeIndexPageV1>(PayloadProtectionStateKeys.IndexPage(Scope, page)))!;
            (await store.CommitAsync([PayloadProtectionStateMutation.Put(current.Value with { Generation = generation }, current.ETag)])).ShouldBe(PayloadStateCommitResult.Committed);
        }
    }

    private static void Mismatch(Func<object> action)
        => Should.Throw<PayloadProtectionStateException>(action).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
}
