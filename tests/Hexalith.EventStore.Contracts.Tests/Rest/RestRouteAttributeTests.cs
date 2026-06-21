using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.EventStore.Contracts.Tests.Rest;

public class RestRouteAttributeTests {
    [Fact]
    public void Constructor_ValidArguments_SetsProperties() {
        var attribute = new RestRouteAttribute(RestVerb.Post, "{counterId}/increment");

        attribute.Verb.ShouldBe(RestVerb.Post);
        attribute.Template.ShouldBe("{counterId}/increment");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_EmptyOrWhitespaceTemplate_IsAllowed(string template) {
        var attribute = new RestRouteAttribute(RestVerb.Put, template);

        attribute.Template.ShouldBe(template);
    }

    [Fact]
    public void Constructor_NullTemplate_ThrowsArgumentNullException() => _ = Should.Throw<ArgumentNullException>(
            () => new RestRouteAttribute(RestVerb.Get, null!));

    [Theory]
    [InlineData(RestVerb.Get)]
    [InlineData(RestVerb.Post)]
    [InlineData(RestVerb.Put)]
    [InlineData(RestVerb.Patch)]
    [InlineData(RestVerb.Delete)]
    public void RestVerb_AllValues_AreDefined(RestVerb verb) => Enum.IsDefined(verb).ShouldBeTrue();

    [Fact]
    public void AttributeUsage_AllowsClassOnly_NonMultiple_NonInherited() {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(RestRouteAttribute), typeof(AttributeUsageAttribute));

        _ = usage.ShouldNotBeNull();
        usage.ValidOn.ShouldBe(AttributeTargets.Class);
        usage.AllowMultiple.ShouldBeFalse();
        usage.Inherited.ShouldBeFalse();
    }
}
