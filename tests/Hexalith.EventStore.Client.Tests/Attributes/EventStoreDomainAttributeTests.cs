
using Hexalith.EventStore.Client.Attributes;

namespace Hexalith.EventStore.Client.Tests.Attributes;

public class EventStoreDomainAttributeTests {
    [Fact]
    public void Constructor_ValidDomainName_SetsProperty() {
        var attribute = new EventStoreDomainAttribute("billing");

        Assert.Equal("billing", attribute.DomainName);
    }

    [Fact]
    public void Constructor_ValidKebabCaseName_SetsProperty() {
        var attribute = new EventStoreDomainAttribute("user-management");

        Assert.Equal("user-management", attribute.DomainName);
    }

    [Fact]
    public void Constructor_NullDomainName_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(() => new EventStoreDomainAttribute(null!));
    }

    [Fact]
    public void Constructor_EmptyDomainName_ThrowsArgumentException() {
        _ = Assert.Throws<ArgumentException>(() => new EventStoreDomainAttribute(""));
    }

    [Fact]
    public void Constructor_WhitespaceDomainName_ThrowsArgumentException() {
        _ = Assert.Throws<ArgumentException>(() => new EventStoreDomainAttribute("  "));
    }
}
