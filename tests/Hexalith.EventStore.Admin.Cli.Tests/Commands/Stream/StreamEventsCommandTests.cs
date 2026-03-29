using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Stream;

public class StreamEventsCommandTests
{
    private static PagedResult<TimelineEntry> CreateTestTimeline(int count = 2, int totalCount = 2)
    {
        List<TimelineEntry> items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(new TimelineEntry(
                i + 1,
                DateTimeOffset.UtcNow,
                i % 2 == 0 ? TimelineEntryType.Command : TimelineEntryType.Event,
                $"Namespace.Type{i}",
                $"corr-{i}",
                $"user-{i}"));
        }

        return new PagedResult<TimelineEntry>(items, totalCount, null);
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
    public async Task StreamEventsCommand_ReturnsTimelineTable()
    {
        // Arrange
        PagedResult<TimelineEntry> result = CreateTestTimeline();
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await StreamEventsCommand.ExecuteAsync(client, options, "acme", "counter", "01J", null, null, 100, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task StreamEventsCommand_WithSequenceRange_SendsFromTo()
    {
        // Arrange
        PagedResult<TimelineEntry> result = CreateTestTimeline();
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await StreamEventsCommand.ExecuteAsync(client, options, "acme", "counter", "01J", 5, 20, 100, CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.ToString();
        requestUri.ShouldContain("fromSequence=5");
        requestUri.ShouldContain("toSequence=20");
    }

    [Fact]
    public void StreamEventsCommand_CsvFormat_ReturnsTimelineRows()
    {
        // Arrange
        PagedResult<TimelineEntry> result = CreateTestTimeline();
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string csv = formatter.FormatCollection(result.Items.ToList(), StreamEventsCommand.Columns);

        // Assert
        string[] lines = csv.Split(Environment.NewLine);
        lines[0].ShouldContain("Seq");
        lines[0].ShouldContain("TypeName");
        lines.Length.ShouldBe(3); // header + 2 rows
        lines[1].ShouldContain("Namespace.Type0");
    }

    [Fact]
    public void StreamEventsCommand_JsonFormat_EntryTypeSerializesAsString()
    {
        // Arrange
        TimelineEntry entry = new(1, DateTimeOffset.UtcNow, TimelineEntryType.Command, "SomeType", "corr-1", "user-1");

        // Act
        string json = JsonSerializer.Serialize(entry, JsonDefaults.Options);

        // Assert
        json.ShouldContain("\"command\"");
        json.ShouldNotContain("\"entryType\":0");
    }
}
