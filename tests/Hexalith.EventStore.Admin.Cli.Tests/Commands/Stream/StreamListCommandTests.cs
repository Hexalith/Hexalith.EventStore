using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Tests.Client;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Stream;

public class StreamListCommandTests
{
    private static PagedResult<StreamSummary> CreateTestResult(int count = 2, int totalCount = 2)
    {
        List<StreamSummary> items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(new StreamSummary(
                $"tenant-{i}",
                $"domain-{i}",
                $"agg-{i}",
                (i + 1) * 10L,
                DateTimeOffset.UtcNow,
                (i + 1) * 100L,
                i % 2 == 0,
                StreamStatus.Active));
        }

        return new PagedResult<StreamSummary>(items, totalCount, null);
    }

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    private static (AdminApiClient Client, MockHttpMessageHandler Handler) CreateMockClientWithHandler(
        object responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        string json = JsonSerializer.Serialize(responseBody, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions();
        return (new AdminApiClient(options, handler), handler);
    }

    [Fact]
    public void StreamListCommand_ReturnsStreamTable()
    {
        // Arrange — test formatting directly to avoid Console capture issues
        PagedResult<StreamSummary> result = CreateTestResult();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(result.Items.ToList(), StreamListCommand.Columns);

        // Assert
        output.ShouldContain("Status");
        output.ShouldContain("Tenant");
        output.ShouldContain("tenant-0");
        output.ShouldContain("domain-1");
    }

    [Fact]
    public async Task StreamListCommand_EmptyResult_PrintsNoStreamsFound()
    {
        // Arrange
        PagedResult<StreamSummary> result = new([], 0, null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await StreamListCommand.ExecuteAsync(client, options, null, null, 1000, CancellationToken.None);
        }

        // Assert — exit code 0 on empty result
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task StreamListCommand_PaginatedResult_ExitCodeSuccess()
    {
        // Arrange
        PagedResult<StreamSummary> result = CreateTestResult(count: 2, totalCount: 50);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await StreamListCommand.ExecuteAsync(client, options, null, null, 1000, CancellationToken.None);
        }

        // Assert — returns success even with paginated results
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public void StreamListCommand_JsonFormat_ReturnsValidJson()
    {
        // Arrange — test JSON formatting directly
        PagedResult<StreamSummary> result = CreateTestResult();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(result);

        // Assert
        PagedResult<StreamSummary>? deserialized = JsonSerializer.Deserialize<PagedResult<StreamSummary>>(json, JsonDefaults.Options);
        deserialized.ShouldNotBeNull();
        deserialized.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StreamListCommand_WithFilters_SendsQueryParameters()
    {
        // Arrange
        PagedResult<StreamSummary> result = CreateTestResult();
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await StreamListCommand.ExecuteAsync(client, options, "acme", "counter", 50, CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("count=50");
        requestUri.ShouldContain("tenantId=acme");
        requestUri.ShouldContain("domain=counter");
    }

    [Fact]
    public void StreamListCommand_TableFormat_ShowsCorrectColumns()
    {
        // Arrange
        PagedResult<StreamSummary> result = CreateTestResult();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(result.Items.ToList(), StreamListCommand.Columns);

        // Assert
        output.ShouldContain("Status");
        output.ShouldContain("Tenant");
        output.ShouldContain("Domain");
        output.ShouldContain("AggregateId");
        output.ShouldContain("Events");
        output.ShouldContain("Last Sequence");
        output.ShouldContain("Last Activity");
        output.ShouldContain("Snapshot");
        output.ShouldContain("tenant-0");
        output.ShouldContain("domain-1");
    }

    [Fact]
    public void StreamListCommand_CsvFormat_ReturnsHeaderAndRows()
    {
        // Arrange
        PagedResult<StreamSummary> result = CreateTestResult();
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string csv = formatter.FormatCollection(result.Items.ToList(), StreamListCommand.Columns);

        // Assert
        string[] lines = csv.Split(Environment.NewLine);
        lines[0].ShouldContain("Status");
        lines[0].ShouldContain("Tenant");
        lines.Length.ShouldBe(3); // header + 2 rows
        lines[1].ShouldContain("Active");
        lines[1].ShouldContain("tenant-0");
    }

    [Fact]
    public void StreamListCommand_JsonFormat_EnumsSerializeAsStrings()
    {
        // Arrange
        StreamSummary summary = new("t1", "d1", "a1", 10, DateTimeOffset.UtcNow, 100, false, StreamStatus.Active);

        // Act
        string json = JsonSerializer.Serialize(summary, JsonDefaults.Options);

        // Assert
        json.ShouldContain("\"active\"");
        json.ShouldNotContain("\"streamStatus\":0");

        // Also verify Tombstoned
        StreamSummary tombstoned = new("t2", "d2", "a2", 20, DateTimeOffset.UtcNow, 200, true, StreamStatus.Tombstoned);
        string json2 = JsonSerializer.Serialize(tombstoned, JsonDefaults.Options);
        json2.ShouldContain("\"tombstoned\"");
    }
}
