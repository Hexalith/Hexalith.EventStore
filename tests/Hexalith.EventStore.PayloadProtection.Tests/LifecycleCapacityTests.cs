using Hexalith.EventStore.Contracts.Security;
// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10 and 15; amendment sections 1-4.
namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exact catalog/cache bounds prevent silent eviction of durable member or work authority.</summary>
public sealed class LifecycleCapacityTests
{
    [Fact]
    public async Task ReservationIntoAFullBucketPageRecordsATypedAdmissionFailureWithoutChangingEitherIndex()
    {
        var f = await ReservationTestFixture.CreateAsync();
        const string keyRef = "01J00000000000000000009401";
        const uint pages = 8;
        string backend = PayloadProtectionStateKeys.Digest(f.Backend.BackendId);
        string provider = PayloadProtectionStateKeys.Digest("fixture:version-01");
        uint bucket = IndexPlacementTests.Bucket(new KekInventoryEntryV1(f.Scope, keyRef, 1), pages);
        // Another scope's 256 memberships fill the reserved key's bucket; 257 entries on eight pages need no growth step.
        KekInventoryEntryV1[] full = [.. Enumerable.Range(1, 20_000).Select(i => new KekInventoryEntryV1(new string('c', 64), IndexSeed.KeyRef(i), 1))
            .Where(e => IndexPlacementTests.Bucket(e, pages) == bucket).Take(256)];
        full.Length.ShouldBe(256);
        await f.Store.CommitAsync(IndexSeed.Inventory(backend, provider, pages, full));
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var entropy = new SequenceEntropy([keyRef, .. Enumerable.Range(9500, 64).Select(i => "01J" + i.ToString("D23", System.Globalization.CultureInfo.InvariantCulture))]);
        var reservations = new KeyReservationCoordinator(f.Store, roots, f.Leases, new(roots, [new("parties", f.Erasure)]), f.Backend, new(f.Clock),
            _ => true, "fixture-actor", timeProvider: f.Clock, entropy: entropy);
        var indexes = new PayloadKeyIndexes(f.Store, f.Clock);
        var inventoryBefore = await indexes.ReadKekAsync(backend, provider);
        int reservationCommits = 0;
        f.Store.Committing = mutations => { if (mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:key:", StringComparison.Ordinal))) { reservationCommits++; } };
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        // The full bucket is refused while planning, before any reservation transaction is submitted.
        reservationCommits.ShouldBe(0); f.Backend.WrapCalls.ShouldBe(1);
        var work = await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root);
        var entry = work.Pages.Single().Value.Entries.Single();
        var operation = (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(entry.OperationId)))!.Value;
        operation.KeyRef.ShouldBe(keyRef); operation.Result.ShouldBe("Failed"); operation.ReasonCode.ShouldBe("provider-unavailable");
        var claim = (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(entry.OperationId)))!.Value;
        claim.Status.ShouldBe("Pending"); claim.ReasonCode.ShouldBe("provider-unavailable");
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, keyRef, 1))).ShouldBeNull();
        (await indexes.ReadScopeAsync(f.Scope)).Head.ShouldBeNull();
        var inventoryAfter = await indexes.ReadKekAsync(backend, provider);
        inventoryAfter.Head!.ETag.ShouldBe(inventoryBefore.Head!.ETag); inventoryAfter.Head.Bytes.ShouldBe(inventoryBefore.Head.Bytes);
        foreach (var pair in inventoryBefore.Pages.Zip(inventoryAfter.Pages)) { pair.First.ETag.ShouldBe(pair.Second.ETag); pair.First.Bytes.ShouldBe(pair.Second.Bytes); }
    }

    [Theory]
    [InlineData(254, true)]
    [InlineData(255, false)]
    public async Task WholeIndexPreflightUsesMaximumPageSizesEvenWhenRetainedPagesAreSparse(int pages, bool admitted)
    {
        var clock = new LifecycleTestClock(); var store = new MemoryPayloadProtectionStateStore(clock);
        string backend = new('a', 64); string provider = new('b', 64);
        var head = new KekInventoryHeadV1(backend, provider, 1, (uint)pages, 0, PayloadProtectionStateCodec.Timestamp(clock.GetUtcNow()));
        var mutations = Enumerable.Range(0, pages).Select(page => PayloadProtectionStateMutation.Put(new KekInventoryPageV1(backend, provider, 1, (uint)page, []))).ToList();
        mutations.Add(PayloadProtectionStateMutation.Put(head)); await store.CommitAsync(mutations);
        mutations.Sum(m => (long)m.Bytes!.Length).ShouldBeLessThan(PayloadProtectionOperationalLimits.ReadBytes / 100);
        int reads = 0; store.Reading = _ => reads++;
        using var budget = new PayloadOperationBudget(clock, default);
        // The qualified primitive charges the actual first head response to its enclosing phase.
        PayloadOperationBudget.ChargeRead(PayloadProtectionStateCodec.Encode(head).Length, operation: true);
        if (admitted)
        {
            (await new PayloadKeyIndexes(store, clock).ReadKekAsync(backend, provider)).Pages.Count.ShouldBe(pages);
            reads.ShouldBe(pages + 2);
        }
        else
        {
            var failure = await Should.ThrowAsync<PayloadProtectionStateException>(() => new PayloadKeyIndexes(store, clock).ReadKekAsync(backend, provider).AsTask());
            failure.IsAdmissionLimit.ShouldBeTrue(); reads.ShouldBe(1);
        }
    }

    [Fact]
    public void TwentyThousandEntryInventoryPlanWritesOnlyTheHeadAndOneBucketPageWithinTransactionCapacity()
    {
        string backend = new('a', 64); string provider = new('b', 64); string scope = new('c', 64);
        List<PayloadProtectionStateMutation> seed = IndexSeed.Inventory(backend, provider, 313, IndexSeed.Entries(20_000, scope));
        // The superseded every-page rewrite of this inventory would itself exceed the qualified transaction profile.
        (seed.Sum(m => (long)PayloadProtectionOperationalLimits.Charge(m)) + 1024).ShouldBeGreaterThan(PayloadProtectionOperationalLimits.TransactionBytes);
        KekInventoryView snapshot = IndexSeed.InventoryView(seed);
        var added = new KekInventoryEntryV1(scope, "01J00000000000000000009401", 1);
        IReadOnlyList<PayloadProtectionStateMutation> plan = new PayloadKeyIndexes(new MemoryPayloadProtectionStateStore()).SetInventoryEntry(snapshot, added, true);
        plan.Select(m => m.Key).ShouldBe([PayloadProtectionStateKeys.KekHead(backend, provider), PayloadProtectionStateKeys.KekPage(backend, provider, IndexPlacementTests.Bucket(added, 313))]);
        PayloadProtectionOperationalLimits.RequireTransactionCapacity(plan);
        plan.Sum(m => (long)PayloadProtectionOperationalLimits.Charge(m)).ShouldBeLessThan(64 * 1024);
        snapshot.Head!.Value.Generation.ShouldBe(1ul);
    }

    [Fact]
    public async Task OversizedInventoryReadPhaseFailsBeforeTheFirstPageRead()
    {
        var clock = new LifecycleTestClock(); var store = new MemoryPayloadProtectionStateStore(clock);
        string backend = new('a', 64); string provider = new('b', 64);
        await store.CommitAsync([PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(backend, provider, 1, 256, 65536, PayloadProtectionStateCodec.Timestamp(clock.GetUtcNow())))]);
        int reads = 0; store.Reading = _ => reads++;
        var failure = await Should.ThrowAsync<PayloadProtectionStateException>(() => new PayloadKeyIndexes(store, clock).ReadKekAsync(backend, provider).AsTask());
        failure.Reason.ShouldBe(Hexalith.EventStore.Contracts.Security.UnreadableProtectedDataReason.ProviderUnavailable); failure.IsAdmissionLimit.ShouldBeTrue(); reads.ShouldBe(1);
    }

    [Fact]
    public void FullPermanentMemberAndWorkCatalogsRejectTheNextAdmissionWithoutPruning()
    {
        string root = new('a', 64); string scope = new('b', 64);
        const string now = "2026-10-23T00:00:00.0000000Z";
        var memberPages = new List<PayloadProtectionStateValue<MemberPageV1>>();
        var workPages = new List<PayloadProtectionStateValue<WorkPageV1>>();
        for (uint page = 0; page < 256; page++)
        {
            MemberEntryV1[] members = Enumerable.Range(0, 256).Select(i => { uint ordinal = page * 256 + (uint)i + 1; return new MemberEntryV1(ordinal, 1, ordinal.ToString("D26"), "Cleared"); }).ToArray();
            WorkEntryV1[] work = members.Select(m => new WorkEntryV1(m.InstanceId, scope)).ToArray();
            var member = new MemberPageV1(root, page, members); var pending = new WorkPageV1(root, page, work);
            byte[] memberBytes = PayloadProtectionStateCodec.Encode(member); byte[] workBytes = PayloadProtectionStateCodec.Encode(pending);
            memberBytes.Length.ShouldBeLessThanOrEqualTo(65536); workBytes.Length.ShouldBeLessThanOrEqualTo(65536);
            memberPages.Add(new(member, "1", memberBytes)); workPages.Add(new(pending, "1", workBytes));
        }
        var membersHead = new MemberHeadV1(root, 1, 256, 65536, now); var workHead = new WorkHeadV1(root, 1, 256, 65536, now);
        var catalogs = new PayloadProtectionCatalogs(new MemoryPayloadProtectionStateStore());
        var membersSnapshot = new MemberCatalogSnapshot(new(membersHead, "1", PayloadProtectionStateCodec.Encode(membersHead)), memberPages);
        var workSnapshot = new WorkCatalogSnapshot(new(workHead, "1", PayloadProtectionStateCodec.Encode(workHead)), workPages);
        PayloadProtectionStateException memberCapacity = Should.Throw<PayloadProtectionStateException>(() => catalogs.AppendMember(membersSnapshot, 2, 65537.ToString("D26")));
        memberCapacity.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable); memberCapacity.IsAdmissionLimit.ShouldBeTrue();
        PayloadProtectionStateException workCapacity = Should.Throw<PayloadProtectionStateException>(() => catalogs.AddWork(workSnapshot, new(65537.ToString("D26"), scope)));
        workCapacity.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable); workCapacity.IsAdmissionLimit.ShouldBeTrue();
        memberPages.Sum(p => p.Value.Entries.Length).ShouldBe(65536); workPages.Sum(p => p.Value.Entries.Length).ShouldBe(65536);
        membersSnapshot.Head.Value.Revision.ShouldBe(1ul); workSnapshot.Head.Value.Revision.ShouldBe(1ul);
        // Permanent Cleared tombstones add catalog bytes, without one acknowledgement read per incarnation.
        PayloadProtectionOperationalLimits.RequireClearingCapacity(membersSnapshot, "01J00000000000000000000999", append: false);
        var unclearedHead = membersHead with { PageCount = 16, EntryCount = 4096 };
        var unclearedPages = memberPages.Take(16).Select(page =>
        {
            var value = page.Value with { Entries = page.Value.Entries.Select(member => member with { State = "Serving" }).ToArray() };
            return new PayloadProtectionStateValue<MemberPageV1>(value, page.ETag, PayloadProtectionStateCodec.Encode(value));
        }).ToArray();
        var uncleared = new MemberCatalogSnapshot(new(unclearedHead, "1", PayloadProtectionStateCodec.Encode(unclearedHead)), unclearedPages);
        PayloadProtectionStateException clearingCapacity = Should.Throw<PayloadProtectionStateException>(() => PayloadProtectionOperationalLimits.RequireClearingCapacity(uncleared, "01J00000000000000000000999", append: false));
        clearingCapacity.Reason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable); clearingCapacity.IsAdmissionLimit.ShouldBeTrue();
    }

    [Fact]
    public async Task V072_V137_ActualFourThousandAndNinetySeventhCacheEntryEvictsAndClearsTheOldestArray()
    {
        var owned = new List<byte[]>();
        var clock = new LifecycleTestClock();
        await using var cache = new PayloadKeyCache(4096, TimeSpan.FromMinutes(5), clock);
        for (uint version = 1; version <= 4097; version++)
        {
            using PayloadKeyResolution copy = await cache.GetAsync(new(new('a', 64), "01J00000000000000000000000", version, 1, "fixture:version-01"), _ =>
            {
                byte[] bytes = Enumerable.Repeat((byte)0x75, 32).ToArray(); owned.Add(bytes);
                return ValueTask.FromResult(PayloadKeyResolution.Success(bytes));
            });
        }
        owned.Count.ShouldBe(4097); owned[0].ShouldAllBe(b => b == 0);
        owned.Skip(1).ShouldAllBe(bytes => bytes.All(b => b == 0x75));
        await cache.DisposeAsync(); owned.ShouldAllBe(bytes => bytes.All(b => b == 0));
    }
}
