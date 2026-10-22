
using Hexalith.EventStore.Server.Queries;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class SafeDenialQueryRouteRegistryTests {
    [Fact]
    public void IsOptedIn_RegisteredRoute_ReturnsTrue() {
        var sut = new SafeDenialQueryRouteRegistry([("orders", "list-orders")]);

        sut.IsOptedIn("orders", "list-orders").ShouldBeTrue();
    }

    [Fact]
    public void IsOptedIn_UnregisteredRoute_ReturnsFalse() {
        var sut = new SafeDenialQueryRouteRegistry([("orders", "list-orders")]);

        sut.IsOptedIn("orders", "get-order").ShouldBeFalse();
    }

    [Fact]
    public void IsOptedIn_SameQueryTypeDifferentDomain_ReturnsFalse() {
        var sut = new SafeDenialQueryRouteRegistry([("orders", "list-orders")]);

        sut.IsOptedIn("parties", "list-orders").ShouldBeFalse();
    }

    [Fact]
    public void IsOptedIn_EmptyRegistry_ReturnsFalse() {
        var sut = new SafeDenialQueryRouteRegistry([]);

        sut.IsOptedIn("orders", "list-orders").ShouldBeFalse();
    }

    [Fact]
    public void IsOptedIn_IsCaseSensitive() {
        var sut = new SafeDenialQueryRouteRegistry([("orders", "list-orders")]);

        sut.IsOptedIn("Orders", "list-orders").ShouldBeFalse();
        sut.IsOptedIn("orders", "List-Orders").ShouldBeFalse();
    }

    [Fact]
    public void Constructor_NullRoutes_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SafeDenialQueryRouteRegistry(null!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsOptedIn_InvalidDomain_ThrowsArgumentException(string? domain) {
        var sut = new SafeDenialQueryRouteRegistry([("orders", "list-orders")]);

        Should.Throw<ArgumentException>(() => sut.IsOptedIn(domain!, "list-orders"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsOptedIn_InvalidQueryType_ThrowsArgumentException(string? queryType) {
        var sut = new SafeDenialQueryRouteRegistry([("orders", "list-orders")]);

        Should.Throw<ArgumentException>(() => sut.IsOptedIn("orders", queryType!));
    }
}
