using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.EventStore.Contracts.Tests.Rest;

public class RestQueryBindingAttributeTests
{
    [Fact]
    public void Constructor_ConstantAggregateBinding_SetsProperties()
    {
        var attribute = new RestQueryBindingAttribute(RestQueryBindingSource.Constant, "index");

        attribute.AggregateSource.ShouldBe(RestQueryBindingSource.Constant);
        attribute.AggregateValue.ShouldBe("index");
        attribute.EntitySource.ShouldBe(RestQueryBindingSource.None);
        attribute.EntityValue.ShouldBeNull();
    }

    [Fact]
    public void Constructor_RouteAggregateAndConstantEntityBinding_SetsProperties()
    {
        var attribute = new RestQueryBindingAttribute(
            RestQueryBindingSource.Route,
            "tenantId",
            RestQueryBindingSource.Constant,
            "summary");

        attribute.AggregateSource.ShouldBe(RestQueryBindingSource.Route);
        attribute.AggregateValue.ShouldBe("tenantId");
        attribute.EntitySource.ShouldBe(RestQueryBindingSource.Constant);
        attribute.EntityValue.ShouldBe("summary");
    }

    [Fact]
    public void Constructor_ConstantAggregateAndRouteEntityBinding_SetsProperties()
    {
        var attribute = new RestQueryBindingAttribute(
            RestQueryBindingSource.Constant,
            "index",
            RestQueryBindingSource.Route,
            "userId");

        attribute.AggregateSource.ShouldBe(RestQueryBindingSource.Constant);
        attribute.AggregateValue.ShouldBe("index");
        attribute.EntitySource.ShouldBe(RestQueryBindingSource.Route);
        attribute.EntityValue.ShouldBe("userId");
    }

    [Fact]
    public void Constructor_NoneAggregateSource_ThrowsArgumentOutOfRangeException() => _ =
        Should.Throw<ArgumentOutOfRangeException>(
            () => new RestQueryBindingAttribute(RestQueryBindingSource.None, "index"));

    [Fact]
    public void Constructor_UnsupportedAggregateSource_ThrowsArgumentOutOfRangeException() => _ =
        Should.Throw<ArgumentOutOfRangeException>(
            () => new RestQueryBindingAttribute((RestQueryBindingSource)42, "index"));

    [Fact]
    public void Constructor_UnsupportedEntitySource_ThrowsArgumentOutOfRangeException() => _ =
        Should.Throw<ArgumentOutOfRangeException>(
            () => new RestQueryBindingAttribute(
                RestQueryBindingSource.Constant,
                "index",
                (RestQueryBindingSource)42,
                "entity"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_MissingAggregateValue_ThrowsArgumentException(string? aggregateValue) => _ =
        Should.Throw<ArgumentException>(
            () => new RestQueryBindingAttribute(RestQueryBindingSource.Constant, aggregateValue!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_MissingEntityValue_ThrowsArgumentException(string? entityValue) => _ =
        Should.Throw<ArgumentException>(
            () => new RestQueryBindingAttribute(
                RestQueryBindingSource.Constant,
                "index",
                RestQueryBindingSource.Route,
                entityValue));

    [Theory]
    [InlineData(RestQueryBindingSource.None)]
    [InlineData(RestQueryBindingSource.Constant)]
    [InlineData(RestQueryBindingSource.Route)]
    public void RestQueryBindingSource_AllValues_AreDefined(RestQueryBindingSource source) => Enum.IsDefined(source).ShouldBeTrue();

    [Fact]
    public void AttributeUsage_AllowsClassOnly_NonMultiple_NonInherited()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RestQueryBindingAttribute),
            typeof(AttributeUsageAttribute));

        _ = usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.AllowMultiple.ShouldBeFalse();
        usage.Inherited.ShouldBeFalse();
    }
}
