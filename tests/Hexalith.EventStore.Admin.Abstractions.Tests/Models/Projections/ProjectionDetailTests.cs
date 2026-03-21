using Hexalith.EventStore.Admin.Abstractions.Models.Projections;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Projections;

public class ProjectionDetailTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var errors = new List<ProjectionError>();
        var subscribedTypes = new List<string> { "OrderCreated" };

        var detail = new ProjectionDetail("OrderSummary", "acme", ProjectionStatusType.Running, 5, 100.5, 0, 1000, DateTimeOffset.UtcNow, errors, "{}", subscribedTypes);

        detail.Name.ShouldBe("OrderSummary");
        detail.Errors.ShouldBeEmpty();
        detail.Configuration.ShouldBe("{}");
        detail.SubscribedEventTypes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_InheritsFromProjectionStatus()
    {
        var detail = new ProjectionDetail("OrderSummary", "acme", ProjectionStatusType.Running, 5, 100.5, 0, 1000, DateTimeOffset.UtcNow, [], "{}", []);

        ProjectionStatus status = detail;

        status.Name.ShouldBe("OrderSummary");
        status.Status.ShouldBe(ProjectionStatusType.Running);
    }

    [Fact]
    public void Constructor_WithNullErrors_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new ProjectionDetail("OrderSummary", "acme", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UtcNow, null!, "{}", []));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new ProjectionDetail("OrderSummary", "acme", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UtcNow, [], null!, []));
    }

    [Fact]
    public void Constructor_WithNullSubscribedEventTypes_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new ProjectionDetail("OrderSummary", "acme", ProjectionStatusType.Running, 0, 0, 0, 0, DateTimeOffset.UtcNow, [], "{}", null!));
    }

    [Fact]
    public void ToString_RedactsConfiguration()
    {
        var detail = new ProjectionDetail("OrderSummary", "acme", ProjectionStatusType.Running, 5, 100.5, 0, 1000, DateTimeOffset.UtcNow, [], "{\"connectionString\":\"secret\"}", ["OrderCreated"]);

        string result = detail.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("connectionString");
        result.ShouldNotContain("secret");
        result.ShouldContain("OrderSummary");
    }
}
