using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.EventStore.Contracts.Tests.Rest;

public class RestApiAttributeTests {
    [Fact]
    public void Constructor_AllArguments_SetsProperties() {
        var attribute = new RestApiAttribute("api/tenants", "tenants", RestTenantSource.Route);

        attribute.RoutePrefix.ShouldBe("api/tenants");
        attribute.Tag.ShouldBe("tenants");
        attribute.TenantSource.ShouldBe(RestTenantSource.Route);
    }

    [Fact]
    public void Constructor_OnlyRoutePrefix_AppliesDefaults() {
        var attribute = new RestApiAttribute("api/counters");

        attribute.RoutePrefix.ShouldBe("api/counters");
        attribute.Tag.ShouldBeNull();
        attribute.TenantSource.ShouldBe(RestTenantSource.Claims);
    }

    [Fact]
    public void Constructor_NullRoutePrefix_ThrowsArgumentNullException() => _ = Should.Throw<ArgumentNullException>(
            () => new RestApiAttribute(null!));

    [Theory]
    [InlineData(RestTenantSource.Claims)]
    [InlineData(RestTenantSource.Route)]
    [InlineData(RestTenantSource.System)]
    public void RestTenantSource_AllValues_AreDefined(RestTenantSource source) => Enum.IsDefined(source).ShouldBeTrue();

    [Fact]
    public void AttributeUsage_AllowsAssemblyOnly_NonMultiple() {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RestApiAttribute), typeof(AttributeUsageAttribute));

        _ = usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Assembly);
        usage.AllowMultiple.ShouldBeFalse();
    }
}
