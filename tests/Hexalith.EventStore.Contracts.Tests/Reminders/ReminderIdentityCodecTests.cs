using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.Contracts.Tests.Reminders;

/// <summary>Golden version-one AD-11/AD-25 reminder vectors, calculated independently of this codec.</summary>
public class ReminderIdentityCodecTests
{
    private static readonly DateTimeOffset Due = new(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);

    /// <summary>The actor identifier digests the length-prefixed tuple (reminder-actor, tenant, item).</summary>
    [Theory]
    [InlineData("tenant-a", "item-1", "wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG")]
    [InlineData("tenant-a", "Item-1", "wra-750NNZXCE0515AR0KPZV9Y1ZWYEYKXFT0ERPXMXV6B9W0V27MFPG")]
    [InlineData("tenant-b", "item-1", "wra-GMY7NNDVYYZYFJY27HQQHR157F3DWVQAK5TPVTJY6HP5TA1P55EG")]
    public void ActorIdGoldenVectors(string tenant, string item, string expected)
    {
        string actorId = ReminderIdentityCodec.ComputeActorId(tenant, item);

        actorId.ShouldBe(expected);
        actorId.Length.ShouldBe(56);
    }

    /// <summary>The actor tuple bytes follow the AD-26 text rule: four-byte big-endian length then UTF-8.</summary>
    [Fact]
    public void ActorTupleEncodingIsLengthPrefixed()
        => Convert.ToHexStringLower(ReminderIdentityCodec.EncodeActorTuple("tenant-a", "item-1"))
            .ShouldBe("0000000e72656d696e6465722d6163746f720000000874656e616e742d61000000066974656d2d31");

    /// <summary>The schedule token digests (schedule, tenant, item, due UTC ticks, revision).</summary>
    [Theory]
    [InlineData("item-1", 0L, "wrs-DH9MMM0QHN2MWP2XDVTVGDQ8PMVMN8KDNFAPWMAJQNA7T0E6PB20")]
    [InlineData("item-1", 1L, "wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0")]
    [InlineData("Item-1", 1L, "wrs-6P3J38VYKVQCKFH8WGX4ZP2KCD7QJVT8MN0D74K352JFEMV75NYG")]
    public void ScheduleTokenGoldenVectors(string item, long revision, string expected)
    {
        Due.UtcTicks.ShouldBe(639264438000000000);
        ReminderIdentityCodec.ComputeScheduleToken("tenant-a", item, Due, revision).ShouldBe(expected);
    }

    /// <summary>The closed kind map names date-resume and expiry reminders from the schedule token.</summary>
    [Theory]
    [InlineData(EffectKindCatalog.DateResume, "date-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0")]
    [InlineData(EffectKindCatalog.Expiry, "expiry-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0")]
    public void ReminderNameGoldenVectors(string kind, string expected)
    {
        ReminderIntent intent = Intent() with { Kind = kind };

        ReminderIdentityCodec.ComputeReminderName(intent).ShouldBe(expected);
        ReminderIdentityCodec.ComputeActorId(intent).ShouldBe("wra-HZT1ANXRJ95M9MPNEVPJYRE4QS912QRKTCMEZWE0EBFQK015G1WG");
        ReminderIdentityCodec.TryParseReminderName(expected, out string parsedKind, out string token).ShouldBeTrue();
        parsedKind.ShouldBe(kind);
        token.ShouldBe(ReminderIdentityCodec.ComputeScheduleToken(intent));
    }

    /// <summary>The same instant in another offset is not canonical and is refused rather than converted.</summary>
    [Fact]
    public void NonUtcDueInstantIsRejected()
    {
        DateTimeOffset shifted = Due.ToOffset(TimeSpan.FromHours(2));

        shifted.UtcTicks.ShouldBe(Due.UtcTicks);
        Should.Throw<ArgumentException>(() => ReminderIdentityCodec.ComputeScheduleToken("tenant-a", "item-1", shifted, 1));
    }

    /// <summary>Tenant, item, due instant, and revision each change the witness; only tenant and item bind the actor.</summary>
    [Fact]
    public void EveryWitnessFieldIsBound()
    {
        string token = ReminderIdentityCodec.ComputeScheduleToken("tenant-a", "item-1", Due, 1);

        ReminderIdentityCodec.ComputeScheduleToken("tenant-b", "item-1", Due, 1).ShouldNotBe(token);
        ReminderIdentityCodec.ComputeScheduleToken("tenant-a", "item-2", Due, 1).ShouldNotBe(token);
        ReminderIdentityCodec.ComputeScheduleToken("tenant-a", "item-1", Due.AddTicks(1), 1).ShouldNotBe(token);
        ReminderIdentityCodec.ComputeScheduleToken("tenant-a", "item-1", Due, 2).ShouldNotBe(token);
        ReminderIdentityCodec.ComputeActorId("tenant-a", "item-2")
            .ShouldNotBe(ReminderIdentityCodec.ComputeActorId("tenant-a", "item-1"));
    }

    /// <summary>A stored tuple re-derives its actor and name; any altered field is a detected collision.</summary>
    [Fact]
    public void StoredTupleCollisionIsDetected()
    {
        string actorId = ReminderIdentityCodec.ComputeActorId("tenant-a", "item-1");
        string name = ReminderIdentityCodec.ComputeReminderName(Intent());

        ReminderIdentityCodec.Rederives("tenant-a", "item-1", EffectKindCatalog.DateResume, Due, 1, actorId, name).ShouldBeTrue();
        ReminderIdentityCodec.Rederives("tenant-b", "item-1", EffectKindCatalog.DateResume, Due, 1, actorId, name).ShouldBeFalse();
        ReminderIdentityCodec.Rederives("tenant-a", "item-2", EffectKindCatalog.DateResume, Due, 1, actorId, name).ShouldBeFalse();
        ReminderIdentityCodec.Rederives("tenant-a", "item-1", EffectKindCatalog.Expiry, Due, 1, actorId, name).ShouldBeFalse();
        ReminderIdentityCodec.Rederives("tenant-a", "item-1", EffectKindCatalog.DateResume, Due.AddSeconds(1), 1, actorId, name).ShouldBeFalse();
        ReminderIdentityCodec.Rederives("tenant-a", "item-1", EffectKindCatalog.DateResume, Due, 2, actorId, name).ShouldBeFalse();
        ReminderIdentityCodec.Rederives("tenant-a", "item-1", "works.unknown.v1", Due, 1, actorId, name).ShouldBeFalse();
    }

    /// <summary>Only the closed version-one map yields reminder names; other catalog kinds and unknown kinds are refused.</summary>
    [Theory]
    [InlineData(EffectKindCatalog.ChildCompletionResume)]
    [InlineData(EffectKindCatalog.CascadeCancel)]
    [InlineData(EffectKindCatalog.CascadeExpire)]
    [InlineData(EffectKindCatalog.Registry)]
    [InlineData(EffectKindCatalog.LateAttach)]
    [InlineData("works.unknown.v1")]
    public void UnknownOrNonReminderKindsAreRejected(string kind)
    {
        ReminderIdentityCodec.IsSupportedKind(kind).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => ReminderIdentityCodec.GetReminderNamePrefix(kind));
        Should.Throw<ArgumentException>(() => ReminderIdentityCodec.ComputeReminderName(Intent() with { Kind = kind }));
    }

    /// <summary>Malformed or foreign names never parse into a kind and token.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("drain-unpublished-1")]
    [InlineData("date-")]
    [InlineData("date-wrs-short")]
    [InlineData("date-wra-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0")]
    [InlineData("date-wrs-aed0kv4p0sf4raqgxrj8azjej1ne6tvf5gwfhd0z3hrmvrsqa8d0")]
    [InlineData("DATE-wrs-AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0")]
    public void MalformedReminderNamesDoNotParse(string? name)
        => ReminderIdentityCodec.TryParseReminderName(name, out _, out _).ShouldBeFalse();

    /// <summary>Non-canonical tenants, invalid items, and negative revisions are refused before hashing.</summary>
    [Fact]
    public void InvalidCoordinatesAreRejected()
    {
        Should.Throw<ArgumentException>(() => ReminderIdentityCodec.ComputeActorId("Tenant-a", "item-1"));
        Should.Throw<ArgumentException>(() => ReminderIdentityCodec.ComputeActorId("tenant-a", "item:1"));
        Should.Throw<ArgumentException>(() => ReminderIdentityCodec.ComputeActorId("tenant-a", " "));
        Should.Throw<ArgumentOutOfRangeException>(() => ReminderIdentityCodec.ComputeScheduleToken("tenant-a", "item-1", Due, -1));
        Should.Throw<ArgumentException>(() => ReminderIdentityCodec.ComputeReminderName(EffectKindCatalog.DateResume, "wrs-too-short"));
    }

    /// <summary>The reminder codec leaves the frozen AD-26 effect codec and its golden vectors unchanged.</summary>
    [Fact]
    public void EffectIdentityCodecVectorsAreUnchanged()
    {
        var identity = new EffectIdentity("tenant-a", "works", "source-1", 42, EffectKindCatalog.DateResume, "works", "target-2", 0);

        EffectIdentityCodec.Version.ShouldBe(1);
        ReminderIdentityCodec.Version.ShouldBe(1);
        EffectIdentityCodec.ComputeEffectId(identity).ShouldBe("9S99K6NV36NBMFZTSV8ZSMQZPSF084R7K1RN0EQ6JPDK8MJVWNXG");
        EffectIdentityCodec.ComputeEffectId(identity with { EffectKind = EffectKindCatalog.Expiry })
            .ShouldBe("KVJBKV0VKM3NAQ7VK2PH2ZW1SXQE48GVPJ0P93778DHGZ3VRBJPG");
    }

    private static ReminderIntent Intent()
        => new(
            "tenant-a",
            "works",
            "item-1",
            Due,
            EffectKindCatalog.DateResume,
            "works.date-resume-payload.v1",
            [1, 2, 3],
            "works",
            "item-1",
            7,
            1);
}
