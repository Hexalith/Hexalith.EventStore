
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Contracts.Tests.Projections;

public class ProjectionChangedNotificationTests {
    [Fact]
    public void Constructor_WithAllFields_CreatesInstance() {
        var notification = new ProjectionChangedNotification(
            ProjectionType: "order-list",
            TenantId: "acme",
            EntityId: "order-123");

        notification.ProjectionType.ShouldBe("order-list");
        notification.TenantId.ShouldBe("acme");
        notification.EntityId.ShouldBe("order-123");
    }

    [Fact]
    public void Constructor_WithoutEntityId_DefaultsToNull() {
        var notification = new ProjectionChangedNotification(
            ProjectionType: "order-list",
            TenantId: "acme");

        notification.EntityId.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var n1 = new ProjectionChangedNotification("order-list", "acme", "order-123");
        var n2 = new ProjectionChangedNotification("order-list", "acme", "order-123");

        n2.ShouldBe(n1);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual() {
        var n1 = new ProjectionChangedNotification("order-list", "acme");
        var n2 = new ProjectionChangedNotification("order-list", "other-tenant");

        n2.ShouldNotBe(n1);
    }

    [Fact]
    public void RecordEquality_WithAndWithoutEntityId_AreNotEqual() {
        var n1 = new ProjectionChangedNotification("order-list", "acme", "order-123");
        var n2 = new ProjectionChangedNotification("order-list", "acme");

        n2.ShouldNotBe(n1);
    }
}
