using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands;

public class HealthCommandTests {
    private static SystemHealthReport CreateTestReport(
        HealthStatus overallStatus = HealthStatus.Healthy,
        long totalEvents = 1000,
        double eventsPerSec = 42.5,
        double errorPercent = 0.12) => new(
            overallStatus,
            totalEvents,
            eventsPerSec,
            errorPercent,
            [
                new DaprComponentHealth("state-redis", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow),
                new DaprComponentHealth("pubsub-redis", "pubsub.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow),
            ],
            new ObservabilityLinks(null, null, null));

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    private static HttpClient CreateMockHttpClient(SystemHealthReport report) {
        string json = JsonSerializer.Serialize(report, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002") };
    }

    [Fact]
    public async Task HealthCommand_HealthyReport_ReturnsExitCode0() {
        // Arrange
        SystemHealthReport report = CreateTestReport(HealthStatus.Healthy);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode;
        using (HttpClient httpClient = CreateMockHttpClient(report)) {
            exitCode = await ExecuteHealthWithMockAsync(httpClient, options);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task HealthCommand_DegradedReport_ReturnsExitCode1() {
        // Arrange
        SystemHealthReport report = CreateTestReport(HealthStatus.Degraded);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode;
        using (HttpClient httpClient = CreateMockHttpClient(report)) {
            exitCode = await ExecuteHealthWithMockAsync(httpClient, options);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Degraded);
    }

    [Fact]
    public async Task HealthCommand_UnhealthyReport_ReturnsExitCode2() {
        // Arrange
        SystemHealthReport report = CreateTestReport(HealthStatus.Unhealthy);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode;
        using (HttpClient httpClient = CreateMockHttpClient(report)) {
            exitCode = await ExecuteHealthWithMockAsync(httpClient, options);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void HealthCommand_TableFormat_ShowsOverviewAndComponents() {
        // Arrange
        SystemHealthReport report = CreateTestReport();
        IOutputFormatter formatter = new TableOutputFormatter();

        int healthyCount = report.DaprComponents.Count(c => c.Status == HealthStatus.Healthy);
        var overview = new {
            report.OverallStatus,
            TotalEvents = report.TotalEventCount,
            EventsPerSec = report.EventsPerSecond.ToString("F1"),
            ErrorPercent = report.ErrorPercentage.ToString("F2"),
            DaprComponents = report.DaprComponents.Count,
            Healthy = healthyCount,
            Degraded = 0,
            Unhealthy = 0,
        };

        List<ColumnDefinition> columns =
        [
            new("Component Name", "ComponentName"),
            new("Type", "ComponentType"),
            new("Status", "Status"),
            new("Last Check", "LastCheckUtc"),
        ];

        // Act
        string overviewOutput = formatter.Format(overview);
        string componentOutput = formatter.FormatCollection(report.DaprComponents.ToList(), columns);

        // Assert — overview section
        overviewOutput.ShouldContain("OverallStatus");
        overviewOutput.ShouldContain("Healthy");
        overviewOutput.ShouldContain("TotalEvents");
        overviewOutput.ShouldContain("1000");

        // Assert — component section
        componentOutput.ShouldContain("state-redis");
        componentOutput.ShouldContain("pubsub-redis");
        componentOutput.ShouldContain("Component Name");
        componentOutput.ShouldContain("Type");
    }

    [Fact]
    public void HealthCommand_JsonFormat_ReturnsValidJson() {
        // Arrange
        SystemHealthReport report = CreateTestReport();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(report);

        // Assert
        SystemHealthReport? deserialized = JsonSerializer.Deserialize<SystemHealthReport>(json, JsonDefaults.Options);
        _ = deserialized.ShouldNotBeNull();
        deserialized.OverallStatus.ShouldBe(report.OverallStatus);
        deserialized.TotalEventCount.ShouldBe(report.TotalEventCount);
        deserialized.DaprComponents.Count.ShouldBe(report.DaprComponents.Count);
    }

    [Fact]
    public void HealthCommand_CsvFormat_ReturnsComponentRows() {
        // Arrange
        SystemHealthReport report = CreateTestReport();
        IOutputFormatter formatter = new CsvOutputFormatter();
        List<ColumnDefinition> columns =
        [
            new("ComponentName", "ComponentName"),
            new("ComponentType", "ComponentType"),
            new("Status", "Status"),
            new("LastCheckUtc", "LastCheckUtc"),
        ];

        // Act
        string csv = formatter.FormatCollection(report.DaprComponents.ToList(), columns);

        // Assert
        string[] lines = csv.Split(Environment.NewLine);
        lines[0].ShouldBe("ComponentName,ComponentType,Status,LastCheckUtc");
        lines.Length.ShouldBe(3); // header + 2 components
        lines[1].ShouldStartWith("state-redis,state.redis,Healthy,");
        lines[2].ShouldStartWith("pubsub-redis,pubsub.redis,Healthy,");
    }

    private static async Task<int> ExecuteHealthWithMockAsync(HttpClient httpClient, GlobalOptions options) {
        using AdminApiClient client = new(httpClient);
        IOutputFormatter formatter = OutputFormatterFactory.Create(options.Format);

        SystemHealthReport report = await client
            .GetAsync<SystemHealthReport>("/api/v1/admin/health", CancellationToken.None)
            .ConfigureAwait(false);

        return report.OverallStatus switch {
            HealthStatus.Healthy => ExitCodes.Success,
            HealthStatus.Degraded => ExitCodes.Degraded,
            _ => ExitCodes.Error,
        };
    }
}
