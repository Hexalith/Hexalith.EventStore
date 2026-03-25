using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Tests.Client;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Stream;

public class StreamEventCommandTests
{
    private static EventDetail CreateTestEvent()
        => new(
            "acme",
            "counter",
            "01JARX7K9M2T5N",
            42,
            "CounterIncremented",
            DateTimeOffset.UtcNow,
            "corr-123",
            "caus-456",
            "admin@acme.com",
            """{"amount":1}""");

    private static GlobalOptions CreateOptions(string format = "table", string? outputFile = null)
        => new("http://localhost:5002", null, format, outputFile);

    private static AdminApiClient CreateMockClient(object? responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        HttpResponseMessage response = new(statusCode);
        if (responseBody is not null)
        {
            string json = JsonSerializer.Serialize(responseBody, JsonDefaults.Options);
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        MockHttpMessageHandler handler = new(response);
        GlobalOptions options = CreateOptions();
        return new AdminApiClient(options, handler);
    }

    [Fact]
    public void StreamEventCommand_ReturnsEventDetail()
    {
        // Arrange — test formatting directly to avoid Console capture issues
        EventDetail detail = CreateTestEvent();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.Format(detail, StreamEventCommand.Columns);

        // Assert
        output.ShouldContain("Tenant");
        output.ShouldContain("acme");
        output.ShouldContain("CounterIncremented");
        output.ShouldContain("EventType");
        output.ShouldContain("CorrelationId");
    }

    [Fact]
    public async Task StreamEventCommand_NotFound_PrintsError()
    {
        // Arrange
        using AdminApiClient client = CreateMockClient(null, HttpStatusCode.NotFound);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode = await StreamEventCommand.ExecuteAsync(client, options, "acme", "counter", "01JARX7K9M2T5N", 42, CancellationToken.None);

        // Assert — exit code 2 on 404
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void StreamEventCommand_JsonFormat_ReturnsFullEventDetail()
    {
        // Arrange — test JSON formatting directly
        EventDetail detail = CreateTestEvent();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(detail);

        // Assert — JSON output should contain payloadJson field with the raw JSON string
        json.ShouldContain("payloadJson");
        json.ShouldContain("amount");
        // Verify it round-trips
        EventDetail? deserialized = JsonSerializer.Deserialize<EventDetail>(json, JsonDefaults.Options);
        deserialized.ShouldNotBeNull();
        deserialized.PayloadJson.ShouldContain("amount");
    }

    [Fact]
    public async Task StreamEventCommand_ParsesSequenceNumberArgument()
    {
        // Arrange — verify URL includes sequence number
        EventDetail detail = CreateTestEvent();
        string json = JsonSerializer.Serialize(detail, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        _ = await StreamEventCommand.ExecuteAsync(client, options, "acme", "counter", "01J", 42, CancellationToken.None);

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("/events/42");
        requestUri.ShouldContain("/acme/");
        requestUri.ShouldContain("/counter/");
        requestUri.ShouldContain("/01J/");
    }
}
