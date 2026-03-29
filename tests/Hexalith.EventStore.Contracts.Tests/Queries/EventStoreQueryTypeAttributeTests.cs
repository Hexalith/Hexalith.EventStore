
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public class EventStoreQueryTypeAttributeTests {
    [Fact]
    public void Constructor_ValidQueryType_SetsProperty() {
        var attribute = new EventStoreQueryTypeAttribute("get-counter-status");

        attribute.QueryType.ShouldBe("get-counter-status");
    }

    [Fact]
    public void Constructor_SingleWord_SetsProperty() {
        var attribute = new EventStoreQueryTypeAttribute("orders");

        attribute.QueryType.ShouldBe("orders");
    }

    [Fact]
    public void Constructor_NullQueryType_ThrowsArgumentNullException() => _ = Should.Throw<ArgumentNullException>(
            () => new EventStoreQueryTypeAttribute(null!));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhitespace_ThrowsArgumentException(string queryType) => _ = Should.Throw<ArgumentException>(
            () => new EventStoreQueryTypeAttribute(queryType));

    [Theory]
    [InlineData("get:counter")]
    [InlineData("query:type:name")]
    [InlineData(":leading")]
    [InlineData("trailing:")]
    public void Constructor_QueryTypeWithColon_ThrowsArgumentException(string queryType) {
        ArgumentException ex = Should.Throw<ArgumentException>(
            () => new EventStoreQueryTypeAttribute(queryType));

        ex.Message.ShouldContain("colon", Case.Insensitive);
    }

    [Fact]
    public void AttributeUsage_AllowsClassOnly() {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(EventStoreQueryTypeAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.AllowMultiple.ShouldBeFalse();
        usage.Inherited.ShouldBeFalse();
    }
}
