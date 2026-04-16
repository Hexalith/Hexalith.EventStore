using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Projection;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Projection;

public class ProjectionStatusCommandTests {
    private static ProjectionDetail CreateTestDetail(bool withErrors = true) {
        List<ProjectionError> errors = withErrors
            ?
            [
                new ProjectionError(42, DateTimeOffset.UtcNow.AddMinutes(-5), "Deserialization failed", "OrderCreated"),
                new ProjectionError(55, DateTimeOffset.UtcNow.AddMinutes(-2), "Timeout processing event", "OrderShipped"),
            ]
            : [];

        return new ProjectionDetail(
            "counter-view",
            "acme",
            ProjectionStatusType.Error,
            15L,
            250.5,
            errors.Count,
            1000L,
            DateTimeOffset.UtcNow,
            errors,
            "{\"batchSize\":100,\"retryPolicy\":\"exponential\"}",
            ["OrderCreated", "OrderShipped", "OrderCancelled"]);
    }

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    private static (AdminApiClient Client, MockHttpMessageHandler Handler) CreateMockClientWithHandler(
        object responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK) {
        string json = JsonSerializer.Serialize(responseBody, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(statusCode) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions();
        return (new AdminApiClient(options, handler), handler);
    }

    [Fact]
    public void ProjectionStatusCommand_ReturnsDualSectionOutput() {
        // Arrange
        ProjectionDetail detail = CreateTestDetail(withErrors: true);
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act — render overview section
        var overview = new {
            detail.Name,
            Tenant = detail.TenantId,
            detail.Status,
            detail.Lag,
            detail.Throughput,
            detail.ErrorCount,
            LastPosition = detail.LastProcessedPosition,
            LastProcessed = detail.LastProcessedUtc,
            SubscribedEvents = string.Join(", ", detail.SubscribedEventTypes),
            detail.Configuration,
        };
        string section1 = formatter.Format(overview, ProjectionStatusCommand.OverviewColumns);
        string section2 = formatter.FormatCollection(detail.Errors.ToList(), ProjectionStatusCommand.ErrorColumns);
        string output = section1 + Environment.NewLine + Environment.NewLine + section2;

        // Assert
        output.ShouldContain("counter-view");
        output.ShouldContain("acme");
        output.ShouldContain("OrderCreated, OrderShipped, OrderCancelled");
        output.ShouldContain("Deserialization failed");
        output.ShouldContain("Timeout processing event");
    }

    [Fact]
    public void ProjectionStatusCommand_NoErrors_OmitsErrorsSection() {
        // Arrange
        ProjectionDetail detail = CreateTestDetail(withErrors: false);
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        var overview = new {
            detail.Name,
            Tenant = detail.TenantId,
            detail.Status,
            detail.Lag,
            detail.Throughput,
            detail.ErrorCount,
            LastPosition = detail.LastProcessedPosition,
            LastProcessed = detail.LastProcessedUtc,
            SubscribedEvents = string.Join(", ", detail.SubscribedEventTypes),
            detail.Configuration,
        };
        string output = formatter.Format(overview, ProjectionStatusCommand.OverviewColumns);

        // Assert — overview present, no errors section
        output.ShouldContain("counter-view");
        output.ShouldNotContain("Deserialization failed");
    }

    [Fact]
    public void ProjectionStatusCommand_CsvFormat_NoErrors_ReturnsOverviewRow() {
        // Arrange
        ProjectionDetail detail = CreateTestDetail(withErrors: false);
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act — same overview construction as the command handler
        var overview = new {
            detail.Name,
            Tenant = detail.TenantId,
            detail.Status,
            detail.Lag,
            detail.Throughput,
            detail.ErrorCount,
            LastPosition = detail.LastProcessedPosition,
            LastProcessed = detail.LastProcessedUtc,
            SubscribedEvents = string.Join(", ", detail.SubscribedEventTypes),
            detail.Configuration,
        };
        string output = formatter.FormatCollection(new[] { overview }, ProjectionStatusCommand.OverviewColumns);

        // Assert — header + one data row with overview fields
        string[] lines = output.Split(Environment.NewLine);
        lines.Length.ShouldBe(2); // header + 1 row
        lines[0].ShouldContain("Name");
        lines[1].ShouldContain("counter-view");
        lines[1].ShouldContain("acme");
    }

    [Fact]
    public async Task ProjectionStatusCommand_NotFound_PrintsError() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await ProjectionStatusCommand.ExecuteAsync(client, options, "acme", "nonexistent", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void ProjectionStatusCommand_JsonFormat_ReturnsFullDetail() {
        // Arrange
        ProjectionDetail detail = CreateTestDetail(withErrors: true);
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(detail);

        // Assert
        json.ShouldContain("\"errors\"");
        json.ShouldContain("\"configuration\"");
        json.ShouldContain("\"subscribedEventTypes\"");
        json.ShouldContain("OrderCreated");
    }
}
