using Hexalith.EventStore.Contracts.Security;
// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10 and 15; amendment sections 1-4.
namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exact catalog/cache bounds prevent silent eviction of durable member or work authority.</summary>
public sealed class LifecycleCapacityTests
{
    [Fact]
    public async Task CombinedReservationTransactionRejectsCapacityEvenWhenBothIndividualIndexPlansFit()
    {
        var f = await ReservationTestFixture.CreateAsync();
        const int entries = 7000;
        string backend = PayloadProtectionStateKeys.Digest(f.Backend.BackendId);
        string provider = PayloadProtectionStateKeys.Digest("fixture:version-01");
        string now = PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow());
        uint pageCount = (entries + 255u) / 256u;
        var seeded = new List<PayloadProtectionStateMutation>
        {
            PayloadProtectionStateMutation.Put(new ScopeIndexHeadV1(f.Scope, 1, pageCount, entries, now)),
            PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(backend, provider, 1, pageCount, entries, now)),
        };
        // Synthetic retained index pages isolate full-transaction admission from backend capacity.
        for (uint page = 0; page < pageCount; page++)
        {
            var refs = Enumerable.Range((int)page * 256, Math.Min(256, entries - (int)page * 256)).Select(i => (i + 1).ToString("D26")).ToArray();
            seeded.Add(PayloadProtectionStateMutation.Put(new ScopeIndexPageV1(f.Scope, 1, page, refs.Select(key => new ScopeIndexEntryV1(key, 1, provider, "Reserved")).ToArray())));
            seeded.Add(PayloadProtectionStateMutation.Put(new KekInventoryPageV1(backend, provider, 1, page, refs.Select(key => new KekInventoryEntryV1(f.Scope, key, 1)).ToArray())));
        }
        await f.Store.CommitAsync(seeded);
        var indexes = new PayloadKeyIndexes(f.Store, f.Clock);
        var scopeBefore = await indexes.ReadScopeAsync(f.Scope); var inventoryBefore = await indexes.ReadKekAsync(backend, provider);
        bool rejected = false;
        f.Store.Committing = mutations =>
        {
            if (!mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:key:", StringComparison.Ordinal))) { return; }
            f.Store.Committing = null;
            var scopePlan = mutations.Where(m => m.Key.StartsWith("eventstore:pp:v2:index", StringComparison.Ordinal)).ToArray();
            var inventoryPlan = mutations.Where(m => m.Key.StartsWith("eventstore:pp:v2:kek-index", StringComparison.Ordinal)).ToArray();
            PayloadProtectionOperationalLimits.RequireTransactionCapacity(scopePlan);
            PayloadProtectionOperationalLimits.RequireTransactionCapacity(inventoryPlan);
            (mutations.Sum(m => (long)PayloadProtectionOperationalLimits.Charge(m)) + 1024).ShouldBeGreaterThan(PayloadProtectionOperationalLimits.TransactionBytes);
            rejected = true;
            // The same complete-plan admission runs in Dapr before submitting any transaction.
            PayloadProtectionOperationalLimits.RequireTransactionCapacity(mutations);
        };
        await Should.ThrowAsync<PayloadProtectionCryptographicException>(() => f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context()).AsTask());
        rejected.ShouldBeTrue(); f.Backend.WrapCalls.ShouldBe(1);
        var work = await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root);
        var entry = work.Pages.Single().Value.Entries.Single();
        var operation = (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(entry.OperationId)))!.Value;
        operation.Result.ShouldBe("Failed"); operation.ReasonCode.ShouldBe("provider-unavailable");
        var claim = (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(entry.OperationId)))!.Value;
        claim.Status.ShouldBe("Pending"); claim.ReasonCode.ShouldBe("provider-unavailable");
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, operation.KeyRef, operation.DekVersion))).ShouldBeNull();
        var scopeAfter = await indexes.ReadScopeAsync(f.Scope); var inventoryAfter = await indexes.ReadKekAsync(backend, provider);
        scopeAfter.Head!.ETag.ShouldBe(scopeBefore.Head!.ETag); scopeAfter.Head.Bytes.ShouldBe(scopeBefore.Head.Bytes);
        inventoryAfter.Head!.ETag.ShouldBe(inventoryBefore.Head!.ETag); inventoryAfter.Head.Bytes.ShouldBe(inventoryBefore.Head.Bytes);
        foreach (var pair in scopeBefore.Pages.Zip(scopeAfter.Pages)) { pair.First.ETag.ShouldBe(pair.Second.ETag); pair.First.Bytes.ShouldBe(pair.Second.Bytes); }
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
    public void LargeKekInventoryPlanReturnsTypedAdmissionBeforeAnOversizedTransaction()
    {
        string backend = new('a', 64); string provider = new('b', 64); string scope = new('c', 64);
        const string now = "2026-10-23T00:00:00.0000000Z";
        var pages = Enumerable.Range(0, 80).Select(number =>
        {
            var value = new KekInventoryPageV1(backend, provider, 1, (uint)number,
                Enumerable.Range(number * 256, 256).Select(i => new KekInventoryEntryV1(scope, i.ToString("D26"), 1)).ToArray());
            return new PayloadProtectionStateValue<KekInventoryPageV1>(value, "1", PayloadProtectionStateCodec.Encode(value));
        }).ToArray();
        var head = new KekInventoryHeadV1(backend, provider, 1, 80, 80 * 256, now);
        var snapshot = new KekKeyIndexSnapshot(backend, provider, new(head, "1", PayloadProtectionStateCodec.Encode(head)), pages);
        var indexes = new PayloadKeyIndexes(new MemoryPayloadProtectionStateStore());
        var failure = Should.Throw<PayloadProtectionStateException>(() => indexes.SetInventoryEntry(snapshot, new(scope, "01J00000000000000000009401", 1), true));
        failure.Reason.ShouldBe(Hexalith.EventStore.Contracts.Security.UnreadableProtectedDataReason.ProviderUnavailable); failure.IsAdmissionLimit.ShouldBeTrue();
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
