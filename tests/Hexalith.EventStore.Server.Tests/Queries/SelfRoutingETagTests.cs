
using Hexalith.EventStore.Server.Queries;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class SelfRoutingETagTests
{
    // ===== Encode tests =====

    [Fact]
    public void Encode_ProducesExpectedFormat()
    {
        string result = SelfRoutingETag.Encode("counter", "KW4RnPjU7EuIVqT4LX_AKA");

        // "counter" → base64url "Y291bnRlcg"
        result.ShouldBe("Y291bnRlcg.KW4RnPjU7EuIVqT4LX_AKA");
    }

    [Fact]
    public void Encode_ProjectionTypeWithHyphen_EncodesCorrectly()
    {
        string result = SelfRoutingETag.Encode("user-profile", "KW4RnPjU7EuIVqT4LX_AKA");

        // "user-profile" → base64url "dXNlci1wcm9maWxl"
        result.ShouldBe("dXNlci1wcm9maWxl.KW4RnPjU7EuIVqT4LX_AKA");
    }

    [Fact]
    public void Encode_ProjectionTypeWithUpperCase_EncodesCorrectly()
    {
        string result = SelfRoutingETag.Encode("OrderList", "KW4RnPjU7EuIVqT4LX_AKA");

        // "OrderList" → base64url "T3JkZXJMaXN0"
        result.ShouldBe("T3JkZXJMaXN0.KW4RnPjU7EuIVqT4LX_AKA");
    }

    // ===== GenerateNew tests =====

    [Fact]
    public void GenerateNew_ContainsDotSeparator()
    {
        string etag = SelfRoutingETag.GenerateNew("counter");

        etag.ShouldContain(".");
    }

    [Fact]
    public void GenerateNew_PrefixDecodesToProjectionType()
    {
        string etag = SelfRoutingETag.GenerateNew("counter");

        bool decoded = SelfRoutingETag.TryDecode(etag, out string? projectionType, out _);

        decoded.ShouldBeTrue();
        projectionType.ShouldBe("counter");
    }

    [Fact]
    public void GenerateNew_GuidPartIsBase64Url()
    {
        string etag = SelfRoutingETag.GenerateNew("counter");

        int dotIndex = etag.IndexOf('.');
        string guidPart = etag[(dotIndex + 1)..];

        // Base64url GUID is 22 chars
        guidPart.Length.ShouldBe(22);
        guidPart.ShouldNotContain("+");
        guidPart.ShouldNotContain("/");
        guidPart.ShouldNotContain("=");
    }

    [Fact]
    public void GenerateNew_ProducesUniqueValues()
    {
        string e1 = SelfRoutingETag.GenerateNew("counter");
        string e2 = SelfRoutingETag.GenerateNew("counter");

        e1.ShouldNotBe(e2);
    }

    // ===== TryDecode tests =====

    [Fact]
    public void TryDecode_ValidSelfRoutingETag_ReturnsTrue()
    {
        string etag = SelfRoutingETag.GenerateNew("order-list");

        bool result = SelfRoutingETag.TryDecode(etag, out string? projectionType, out string? guidPart);

        result.ShouldBeTrue();
        projectionType.ShouldBe("order-list");
        guidPart.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryDecode_NoDotSeparator_ReturnsFalse()
    {
        bool result = SelfRoutingETag.TryDecode("KW4RnPjU7EuIVqT4LX_AKA", out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_EmptyString_ReturnsFalse()
    {
        bool result = SelfRoutingETag.TryDecode("", out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_NullString_ReturnsFalse()
    {
        bool result = SelfRoutingETag.TryDecode(null!, out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_EmptyPrefix_ReturnsFalse()
    {
        bool result = SelfRoutingETag.TryDecode(".KW4RnPjU7EuIVqT4LX_AKA", out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_EmptyGuidPart_ReturnsFalse()
    {
        bool result = SelfRoutingETag.TryDecode("Y291bnRlcg.", out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_InvalidBase64UrlPrefix_ReturnsFalse()
    {
        // "!!!" is not valid base64
        bool result = SelfRoutingETag.TryDecode("!!!.KW4RnPjU7EuIVqT4LX_AKA", out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_PrefixContainsColon_ReturnsFalse()
    {
        // Base64url of "counter:tenant" would decode to something with colon
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("counter:tenant");
        string encoded = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        bool result = SelfRoutingETag.TryDecode($"{encoded}.KW4RnPjU7EuIVqT4LX_AKA", out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_OldFormatGuidOnly_ReturnsFalse()
    {
        // Old format: 22-char base64url GUID without dot separator
        string oldFormat = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        bool result = SelfRoutingETag.TryDecode(oldFormat, out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_SingleCharBase64_ReturnsFalse()
    {
        // base64 length % 4 == 1 is always invalid
        bool result = SelfRoutingETag.TryDecode("A.KW4RnPjU7EuIVqT4LX_AKA", out _, out _);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryDecode_MultipleDots_UsesFirstDotAsSeparator()
    {
        // Edge case: projection type that base64-encodes with no dots,
        // but someone sends multiple dots — should use first dot
        string etag = SelfRoutingETag.Encode("counter", "part1.part2");

        bool result = SelfRoutingETag.TryDecode(etag, out string? projectionType, out string? guidPart);

        result.ShouldBeTrue();
        projectionType.ShouldBe("counter");
        guidPart.ShouldBe("part1.part2");
    }

    // ===== Roundtrip tests =====

    [Theory]
    [InlineData("counter")]
    [InlineData("order-list")]
    [InlineData("OrderList")]
    [InlineData("user-profile")]
    [InlineData("a")]
    [InlineData("very-long-projection-type-name-for-testing")]
    public void Roundtrip_EncodeDecodePreservesProjectionType(string projectionType)
    {
        string etag = SelfRoutingETag.GenerateNew(projectionType);

        bool decoded = SelfRoutingETag.TryDecode(etag, out string? decodedType, out _);

        decoded.ShouldBeTrue();
        decodedType.ShouldBe(projectionType);
    }
}
