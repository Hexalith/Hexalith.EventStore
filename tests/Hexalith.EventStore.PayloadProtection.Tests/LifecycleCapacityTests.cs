// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10 and 15; amendment sections 1-4.
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
