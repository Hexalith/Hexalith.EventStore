using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Stream;

public class StreamEventCommandTests {
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

    private static AdminApiClient CreateMockClient(object? responseBody, HttpStatusCode statusCode = HttpStatusCode.OK) {
        HttpResponseMessage response = new(statusCode);
        if (responseBody is not null) {
            string json = JsonSerializer.Serialize(responseBody, JsonDefaults.Options);
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        MockHttpMessageHandler handler = new(response);
        GlobalOptions options = CreateOptions();
        return new AdminApiClient(options, handler);
    }

    [Fact]
    public void StreamEventCommand_ReturnsEventDetail() {
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
    public void StreamEventCommand_TableColumns_DoNotSelectRawPayloadJson() => StreamEventCommand.Columns
            .Select(c => c.PropertyName)
            .ShouldNotContain("PayloadJson");

    [Fact]
    public async Task StreamEventCommand_NotFound_PrintsError() {
        // Arrange
        using AdminApiClient client = CreateMockClient(null, HttpStatusCode.NotFound);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode = await StreamEventCommand.ExecuteAsync(client, options, "acme", "counter", "01JARX7K9M2T5N", 42, CancellationToken.None);

        // Assert — exit code 2 on 404
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void StreamEventCommand_JsonFormat_ReturnsFullEventDetail() {
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
        _ = deserialized.ShouldNotBeNull();
        deserialized.PayloadJson.ShouldContain("amount");
    }

    [Fact]
    public async Task StreamEventCommand_ParsesSequenceNumberArgument() {
        // Arrange — verify URL includes sequence number
        EventDetail detail = CreateTestEvent();
        string json = JsonSerializer.Serialize(detail, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        _ = await StreamEventCommand.ExecuteAsync(client, options, "acme", "counter", "01J", 42, CancellationToken.None);

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("/events/42");
        requestUri.ShouldContain("/acme/");
        requestUri.ShouldContain("/counter/");
        requestUri.ShouldContain("/01J/");
    }

    [Fact]
    public async Task StreamEventCommand_OutputFile_UsesSafeRedactedDescriptor() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var detail = EventDetail.WithRedactedPayload(
            "acme",
            "orders",
            "order-1",
            42,
            "OrderCreated",
            DateTimeOffset.UtcNow,
            "corr-1",
            "caus-1",
            "user-1",
            AdminRedactedContent.Protected(
                contentKind: "event-payload",
                reasonCode: "protected-content",
                stage: "cli-output-file",
                metadataVersion: 1,
                retryable: false,
                permanent: false,
                safeNextAction: "Inspect protection metadata."));
        string json = JsonSerializer.Serialize(detail, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        string outputFile = Path.Combine(Path.GetTempPath(), $"eventstore-admin-{Guid.NewGuid():N}.json");
        // Use JSON format so the full descriptor (incl. reasonCode/contentKind) survives without table truncation.
        GlobalOptions options = CreateOptions("json", outputFile);
        using AdminApiClient client = new(options, handler);

        try {
            int exitCode = await StreamEventCommand.ExecuteAsync(client, options, "acme", "orders", "order-1", 42, ct);

            exitCode.ShouldBe(ExitCodes.Success);
            string output = await File.ReadAllTextAsync(outputFile, ct);
            ProtectedDataLeakSentinel.AssertNoLeak([output]);
            output.ShouldContain("Protected content redacted.");
            output.ShouldContain("event-payload");
            output.ShouldContain("protected-content");
        }
        finally {
            if (File.Exists(outputFile)) {
                File.Delete(outputFile);
            }
        }
    }

    [Fact]
    public void StreamEventCommand_RawOnlyPayload_TableRendersEmpty_FailClosed() {
        // D4 + P17: when descriptor (Payload) is null and only raw PayloadJson is present,
        // table view must render an empty Payload column rather than falling back to raw JSON.
        EventDetail detail = new(
            "acme", "orders", "order-1", 42, "OrderCreated", DateTimeOffset.UtcNow,
            "corr-1", "caus-1", "user-1",
            ProtectedDataLeakSentinel.ProtectedPayloadPlaintext);
        IOutputFormatter table = new TableOutputFormatter();
        IOutputFormatter csv = new CsvOutputFormatter();

        string tableOutput = table.Format(detail, StreamEventCommand.Columns);
        string csvOutput = csv.Format(detail, StreamEventCommand.Columns);

        ProtectedDataLeakSentinel.AssertNoLeak([tableOutput, csvOutput]);
    }

    [Fact]
    public void StreamEventCommand_RawOnlyPayload_JsonStillExposesRaw_ByDesign() {
        // D4 fallback removal applies to CSV/Table only. JSON output still serializes raw PayloadJson
        // when no descriptor is present — that is the documented contract surface for full content.
        // P5/P19: SafeText case-insensitive marker check filters sentinel-shaped raw content.
        EventDetail detail = new(
            "acme", "orders", "order-1", 42, "OrderCreated", DateTimeOffset.UtcNow,
            "corr-1", "caus-1", "user-1",
            ProtectedDataLeakSentinel.ProtectedPayloadPlaintext);
        IOutputFormatter json = new JsonOutputFormatter();

        string output = json.Format(detail);

        // SafeText filter inside JsonOutputFormatter.SanitizeNode replaces the sentinel-shaped string
        // value with the default placeholder, preserving the no-leak invariant.
        ProtectedDataLeakSentinel.AssertNoLeak([output]);
    }
}
