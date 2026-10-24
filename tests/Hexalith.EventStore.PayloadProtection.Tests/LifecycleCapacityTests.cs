// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10 and 15; amendment sections 1-4.
namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exact catalog/cache bounds prevent silent eviction of durable member or work authority.</summary>
public sealed class LifecycleCapacityTests
{
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
        Should.Throw<PayloadProtectionStateException>(() => catalogs.AppendMember(membersSnapshot, 2, 65537.ToString("D26")));
        Should.Throw<PayloadProtectionStateException>(() => catalogs.AddWork(workSnapshot, new(65537.ToString("D26"), scope)));
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
        Should.Throw<PayloadProtectionStateException>(() => PayloadProtectionOperationalLimits.RequireClearingCapacity(uncleared, "01J00000000000000000000999", append: false));
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
