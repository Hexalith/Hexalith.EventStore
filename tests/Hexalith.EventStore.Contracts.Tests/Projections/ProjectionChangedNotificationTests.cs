
using System.Text.Json;

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
        notification.GroupScope.ShouldBeNull();
        notification.Metadata.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithDetailFields_CreatesInstance() {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["freshness"] = "changed",
        };

        var notification = new ProjectionChangedNotification(
            ProjectionType: "order-list",
            TenantId: "acme",
            EntityId: "order-123",
            GroupScope: "order-123",
            Metadata: metadata);

        notification.GroupScope.ShouldBe("order-123");
        notification.Metadata.ShouldBeSameAs(metadata);
    }

    [Fact]
    public void LegacyConstructors_RemainAvailable() {
        Type[] twoArgumentSignature = [typeof(string), typeof(string)];
        Type[] threeArgumentSignature = [typeof(string), typeof(string), typeof(string)];

        typeof(ProjectionChangedNotification).GetConstructor(twoArgumentSignature).ShouldNotBeNull();
        typeof(ProjectionChangedNotification).GetConstructor(threeArgumentSignature).ShouldNotBeNull();
    }

    [Fact]
    public void LegacyDeconstruct_ReturnsOriginalThreeFields() {
        var notification = new ProjectionChangedNotification(
            ProjectionType: "order-list",
            TenantId: "acme",
            EntityId: "order-123",
            GroupScope: "scope-1",
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal) {
                ["freshness"] = "changed",
            });

        (string projectionType, string tenantId, string? entityId) = notification;

        projectionType.ShouldBe("order-list");
        tenantId.ShouldBe("acme");
        entityId.ShouldBe("order-123");
    }

    [Fact]
    public void JsonDeserialization_UsesFullConstructorWhenLegacyConstructorsExist() {
        const string json = """
            {
              "projectionType": "order-list",
              "tenantId": "acme",
              "entityId": "order-123",
              "groupScope": "scope-1",
              "metadata": {
                "freshness": "changed"
              }
            }
            """;

        ProjectionChangedNotification? notification =
            JsonSerializer.Deserialize<ProjectionChangedNotification>(
                json,
                new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true,
                });

        notification.ShouldNotBeNull();
        notification.ProjectionType.ShouldBe("order-list");
        notification.TenantId.ShouldBe("acme");
        notification.EntityId.ShouldBe("order-123");
        notification.GroupScope.ShouldBe("scope-1");
        notification.Metadata.ShouldNotBeNull();
        notification.Metadata["freshness"].ShouldBe("changed");
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
