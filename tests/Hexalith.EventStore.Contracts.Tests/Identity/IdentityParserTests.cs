
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Tests.Identity;

public class IdentityParserTests {
    [Fact]
    public void Parse_ValidCanonicalString_ReturnsIdentity() {
        AggregateIdentity identity = IdentityParser.Parse("acme:payments:order-123");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
    }

    [Theory]
    [InlineData("acme:payments:order-123")]
    [InlineData("a:b:c")]
    [InlineData("tenant-1:domain-2:Order_123.v2")]
    public void Parse_RoundTrip_PreservesIdentity(string canonical) {
        AggregateIdentity identity = IdentityParser.Parse(canonical);
        AggregateIdentity roundTripped = IdentityParser.Parse(identity.ToString());

        Assert.Equal(identity, roundTripped);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("acme")]
    [InlineData("acme:payments")]
    [InlineData("acme:payments:order:extra")]
    public void Parse_InvalidInput_ThrowsFormatException(string? input) => Assert.Throws<FormatException>(() => IdentityParser.Parse(input!));

    [Fact]
    public void TryParse_ValidCanonicalString_ReturnsTrueAndIdentity() {
        bool result = IdentityParser.TryParse("acme:payments:order-123", out AggregateIdentity? identity);

        Assert.True(result);
        Assert.NotNull(identity);
        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
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

        Assert.False(result);
        Assert.Null(identity);
    }

    [Fact]
    public void ParseStateStoreKey_ValidKey_ReturnsIdentityAndSuffix() {
        (AggregateIdentity identity, string suffix) = IdentityParser.ParseStateStoreKey("acme:payments:order-123:events:5");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
        Assert.Equal("events:5", suffix);
    }

    [Fact]
    public void ParseStateStoreKey_MetadataKey_ReturnsIdentityAndSuffix() {
        (AggregateIdentity identity, string suffix) = IdentityParser.ParseStateStoreKey("acme:payments:order-123:metadata");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
        Assert.Equal("metadata", suffix);
    }

    [Fact]
    public void ParseStateStoreKey_SnapshotKey_ReturnsIdentityAndSuffix() {
        (AggregateIdentity identity, string suffix) = IdentityParser.ParseStateStoreKey("acme:payments:order-123:snapshot");

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("payments", identity.Domain);
        Assert.Equal("order-123", identity.AggregateId);
        Assert.Equal("snapshot", suffix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("acme")]
    [InlineData("acme:payments")]
    [InlineData("acme:payments:order-123")]
    public void ParseStateStoreKey_InvalidKey_ThrowsFormatException(string? input) => Assert.Throws<FormatException>(() => IdentityParser.ParseStateStoreKey(input!));
}
