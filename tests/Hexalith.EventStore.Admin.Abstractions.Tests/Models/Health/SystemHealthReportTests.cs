using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Health;

public class SystemHealthReportTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var links = new ObservabilityLinks(null, null, null);
        var report = new SystemHealthReport(HealthStatus.Healthy, 1000, 50.5, 0.1, [], links);

        report.OverallStatus.ShouldBe(HealthStatus.Healthy);
        report.TotalEventCount.ShouldBe(1000);
        report.EventsPerSecond.ShouldBe(50.5);
        report.ErrorPercentage.ShouldBe(0.1);
    }

    [Fact]
    public void Constructor_WithNullDaprComponents_ThrowsArgumentNullException()
    {
        var links = new ObservabilityLinks(null, null, null);
        Should.Throw<ArgumentNullException>(() =>
            new SystemHealthReport(HealthStatus.Healthy, 0, 0, 0, null!, links));
    }

    [Fact]
    public void Constructor_WithNullObservabilityLinks_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SystemHealthReport(HealthStatus.Healthy, 0, 0, 0, [], null!));
    }

    [Fact]
    public void Constructor_WithNaNEventsPerSecond_ThrowsArgumentOutOfRangeException()
    {
        var links = new ObservabilityLinks(null, null, null);
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new SystemHealthReport(HealthStatus.Healthy, 0, double.NaN, 0, [], links));
    }

    [Fact]
    public void Constructor_WithInfinityEventsPerSecond_ThrowsArgumentOutOfRangeException()
    {
        var links = new ObservabilityLinks(null, null, null);
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new SystemHealthReport(HealthStatus.Healthy, 0, double.PositiveInfinity, 0, [], links));
    }

    [Fact]
    public void Constructor_WithNaNErrorPercentage_ThrowsArgumentOutOfRangeException()
    {
        var links = new ObservabilityLinks(null, null, null);
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new SystemHealthReport(HealthStatus.Healthy, 0, 0, double.NaN, [], links));
    }

    [Fact]
    public void Constructor_WithInfinityErrorPercentage_ThrowsArgumentOutOfRangeException()
    {
        var links = new ObservabilityLinks(null, null, null);
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new SystemHealthReport(HealthStatus.Healthy, 0, 0, double.NegativeInfinity, [], links));
    }
}
