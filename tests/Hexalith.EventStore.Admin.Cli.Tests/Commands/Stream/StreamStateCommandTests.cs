using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Stream;

public class StreamStateCommandTests {
    private static AggregateStateSnapshot CreateTestSnapshot()
        => new(
            "acme",
            "counter",
            "01JARX7K9M2T5N",
            10,
            DateTimeOffset.UtcNow,
            """{"count":42,"lastUpdated":"2026-03-25T10:00:00Z"}""");

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
    public void StreamStateCommand_ReturnsSnapshot() {
        // Arrange — test formatting directly to avoid Console capture race conditions
        AggregateStateSnapshot snapshot = CreateTestSnapshot();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.Format(snapshot, StreamStateCommand.Columns);

        // Assert
        output.ShouldContain("Tenant");
        output.ShouldContain("acme");
        output.ShouldContain("StateJson");
    }

    [Fact]
    public async Task StreamStateCommand_NotFound_PrintsError() {
        // Arrange
        using AdminApiClient client = CreateMockClient(null, HttpStatusCode.NotFound);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode = await StreamStateCommand.ExecuteAsync(client, options, "acme", "counter", "01JARX7K9M2T5N", 10, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void StreamStateCommand_RequiresAtOption() {
        // Verify command structure has --at as required
        var binding = GlobalOptionsBinding.Create();
        System.CommandLine.Command command = StreamStateCommand.Create(binding);

        System.CommandLine.Option? atOption = command.Options.FirstOrDefault(o => o.Name == "--at");
        _ = atOption.ShouldNotBeNull();
        atOption.Required.ShouldBeTrue();
    }
}
