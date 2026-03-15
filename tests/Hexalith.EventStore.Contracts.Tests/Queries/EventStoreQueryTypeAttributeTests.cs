
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class EventStoreQueryTypeAttributeTests {
    [Fact]
    public void Constructor_ValidQueryType_SetsProperty() {
        var attribute = new EventStoreQueryTypeAttribute("get-counter-status");

        Assert.Equal("get-counter-status", attribute.QueryType);
    }

    [Fact]
    public void Constructor_SingleWord_SetsProperty() {
        var attribute = new EventStoreQueryTypeAttribute("orders");

        Assert.Equal("orders", attribute.QueryType);
    }

    [Fact]
    public void Constructor_NullQueryType_ThrowsArgumentNullException() => _ = Assert.Throws<ArgumentNullException>(
            () => new EventStoreQueryTypeAttribute(null!));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhitespace_ThrowsArgumentException(string queryType) => _ = Assert.Throws<ArgumentException>(
            () => new EventStoreQueryTypeAttribute(queryType));

    [Theory]
    [InlineData("get:counter")]
    [InlineData("query:type:name")]
    [InlineData(":leading")]
    [InlineData("trailing:")]
    public void Constructor_QueryTypeWithColon_ThrowsArgumentException(string queryType) {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new EventStoreQueryTypeAttribute(queryType));

        Assert.Contains("colon", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AttributeUsage_AllowsClassOnly() {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(EventStoreQueryTypeAttribute), typeof(AttributeUsageAttribute));

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }
}
