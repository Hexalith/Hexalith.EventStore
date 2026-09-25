using Hexalith.EventStore.Contracts.Effects;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Effects;

/// <summary>Golden version-one vectors independent of producers.</summary>
public class EffectIdentityCodecTests
{
    /// <summary>Locks the complete family catalog to independently calculated digests.</summary>
    [Theory]
    [InlineData(EffectKindCatalog.ChildCompletionResume, "TT35YRT0R0XWG8YTPMJY86Y0MTSXCSKQMP3R6T286HP97SR8WRRG")]
    [InlineData(EffectKindCatalog.DateResume, "9S99K6NV36NBMFZTSV8ZSMQZPSF084R7K1RN0EQ6JPDK8MJVWNXG")]
    [InlineData(EffectKindCatalog.CascadeCancel, "JPCRXRCZ48HQ3NR8KWAPR7F9VAEPNB8BNG1BA7KMXC0WTCJ86C2G")]
    [InlineData(EffectKindCatalog.CascadeExpire, "8CKQZ44GXH9X5C3HSVXDPKTGAC0BKTW2F9AP97PXJSXD67NYXFGG")]
    [InlineData(EffectKindCatalog.Registry, "XQY5ZB5XF71XNZM45HRF2TGD4WQ2AE310MVVAMAQRXAWP41ZNAY0")]
    [InlineData(EffectKindCatalog.Expiry, "KVJBKV0VKM3NAQ7VK2PH2ZW1SXQE48GVPJ0P93778DHGZ3VRBJPG")]
    [InlineData(EffectKindCatalog.LateAttach, "SMZW56VN7N51CE1856MJRWRCSG0C9PBHX1YMEFM1GJ8ZWCXQE8Z0")]
    public void FamilyGoldenVectors(string kind, string expected)
    {
        var identity = new EffectIdentity("tenant-a", "works", "source-1", 42, kind, "works", "target-2", 0);

        EffectKindCatalog.IsKnown(kind).ShouldBeTrue();
        EffectIdentityCodec.ComputeEffectId(identity).ShouldBe(expected);
        EffectIdentityCodec.ComputeMessageId(identity).ShouldBe("wrk-" + expected);
    }

    /// <summary>Changes in envelope position, target, and ordinal change the effect identity.</summary>
    [Fact]
    public void PositionTargetAndOrdinalAreBound()
    {
        var identity = new EffectIdentity("tenant-a", "works", "source-1", 42, EffectKindCatalog.DateResume, "works", "target-2", 0);
        string original = EffectIdentityCodec.ComputeEffectId(identity);

        EffectIdentityCodec.ComputeEffectId(identity with { SourceEnvelopeSequence = 43 }).ShouldNotBe(original);
        EffectIdentityCodec.ComputeEffectId(identity with { TargetAggregate = "target-3" }).ShouldNotBe(original);
        EffectIdentityCodec.ComputeEffectId(identity with { Ordinal = 1 }).ShouldNotBe(original);
    }

    /// <summary>Noncanonical text and invalid source positions are refused before hashing.</summary>
    [Fact]
    public void InvalidCoordinatesAreRejected()
    {
        var identity = new EffectIdentity("tenant-a", "works", "source-1", 42, EffectKindCatalog.DateResume, "works", "target-2", 0);

        Should.Throw<ArgumentException>(() => EffectIdentityCodec.Encode(identity with { Tenant = "e\u0301" }));
        Should.Throw<ArgumentOutOfRangeException>(() => EffectIdentityCodec.Encode(identity with { SourceEnvelopeSequence = 0 }));
    }
}
