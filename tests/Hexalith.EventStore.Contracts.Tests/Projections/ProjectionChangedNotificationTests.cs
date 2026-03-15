
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Contracts.Tests.Projections;

public class ProjectionChangedNotificationTests {
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance() {
        var notification = new ProjectionChangedNotification(
            ProjectionType: "order-list",
            TenantId: "acme",
            EntityId: "order-123");

        Assert.Equal("order-list", notification.ProjectionType);
        Assert.Equal("acme", notification.TenantId);
        Assert.Equal("order-123", notification.EntityId);
    }

    [Fact]
    public void Constructor_WithoutEntityId_DefaultsToNull() {
        var notification = new ProjectionChangedNotification(
            ProjectionType: "order-list",
            TenantId: "acme");

        Assert.Null(notification.EntityId);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var n1 = new ProjectionChangedNotification("order-list", "acme", "order-123");
        var n2 = new ProjectionChangedNotification("order-list", "acme", "order-123");

        Assert.Equal(n1, n2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual() {
        var n1 = new ProjectionChangedNotification("order-list", "acme");
        var n2 = new ProjectionChangedNotification("order-list", "other-tenant");

        Assert.NotEqual(n1, n2);
    }

    [Fact]
    public void RecordEquality_WithAndWithoutEntityId_AreNotEqual() {
        var n1 = new ProjectionChangedNotification("order-list", "acme", "order-123");
        var n2 = new ProjectionChangedNotification("order-list", "acme");

        Assert.NotEqual(n1, n2);
    }
}
