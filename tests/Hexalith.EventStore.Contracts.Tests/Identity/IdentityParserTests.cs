
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Tests.Identity;

public class IdentityParserTests {
    [Fact]
    public void Parse_ValidCanonicalString_ReturnsIdentity() {
        AggregateIdentity identity = IdentityParser.Parse("acme:payments:order-123");

        identity.TenantId.ShouldBe("acme");
        identity.Domain.ShouldBe("payments");
        identity.AggregateId.ShouldBe("order-123");
    }

    [Theory]
    [InlineData("acme:payments:order-123")]
    [InlineData("a:b:c")]
    [InlineData("tenant-1:domain-2:Order_123.v2")]
    public void Parse_RoundTrip_PreservesIdentity(string canonical) {
        AggregateIdentity identity = IdentityParser.Parse(canonical);
        AggregateIdentity roundTripped = IdentityParser.Parse(identity.ToString());

        roundTripped.ShouldBe(identity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("acme")]
    [InlineData("acme:payments")]
    [InlineData("acme:payments:order:extra")]
    public void Parse_InvalidInput_ThrowsFormatException(string? input) => Should.Throw<FormatException>(() => IdentityParser.Parse(input!));

    [Fact]
    public void TryParse_ValidCanonicalString_ReturnsTrueAndIdentity() {
        bool result = IdentityParser.TryParse("acme:payments:order-123", out AggregateIdentity? identity);

        result.ShouldBeTrue();
        _ = identity.ShouldNotBeNull();
        identity.TenantId.ShouldBe("acme");
        identity.Domain.ShouldBe("payments");
        identity.AggregateId.ShouldBe("order-123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("acme")]
    [InlineData("acme:payments")]
    [InlineData("acme:payments:order:extra")]
    public void TryParse_InvalidInput_ReturnsFalseAndNull(string? input) {
        bool result = IdentityParser.TryParse(input!, out AggregateIdentity? identity);

        result.ShouldBeFalse();
        identity.ShouldBeNull();
    }

    [Fact]
    public void ParseStateStoreKey_ValidKey_ReturnsIdentityAndSuffix() {
        (AggregateIdentity identity, string suffix) = IdentityParser.ParseStateStoreKey("acme:payments:order-123:events:5");

        identity.TenantId.ShouldBe("acme");
        identity.Domain.ShouldBe("payments");
        identity.AggregateId.ShouldBe("order-123");
        suffix.ShouldBe("events:5");
    }

    [Fact]
    public void ParseStateStoreKey_MetadataKey_ReturnsIdentityAndSuffix() {
        (AggregateIdentity identity, string suffix) = IdentityParser.ParseStateStoreKey("acme:payments:order-123:metadata");

        identity.TenantId.ShouldBe("acme");
        identity.Domain.ShouldBe("payments");
        identity.AggregateId.ShouldBe("order-123");
        suffix.ShouldBe("metadata");
    }

    [Fact]
    public void ParseStateStoreKey_SnapshotKey_ReturnsIdentityAndSuffix() {
        (AggregateIdentity identity, string suffix) = IdentityParser.ParseStateStoreKey("acme:payments:order-123:snapshot");

        identity.TenantId.ShouldBe("acme");
        identity.Domain.ShouldBe("payments");
        identity.AggregateId.ShouldBe("order-123");
        suffix.ShouldBe("snapshot");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("acme")]
    [InlineData("acme:payments")]
    [InlineData("acme:payments:order-123")]
    public void ParseStateStoreKey_InvalidKey_ThrowsFormatException(string? input) => Should.Throw<FormatException>(() => IdentityParser.ParseStateStoreKey(input!));
}
