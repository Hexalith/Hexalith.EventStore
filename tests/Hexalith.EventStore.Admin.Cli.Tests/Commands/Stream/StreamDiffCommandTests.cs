using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Stream;

public class StreamDiffCommandTests {
    private static AggregateStateDiff CreateTestDiff()
        => new(5, 10,
        [
            new FieldChange("count", "41", "42"),
            new FieldChange("lastUpdated", "2026-03-24T10:00:00Z", "2026-03-25T10:00:00Z"),
        ]);

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

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
    public void StreamDiffCommand_ReturnsFieldChanges() {
        // Arrange — test formatting directly
        AggregateStateDiff diff = CreateTestDiff();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(diff.ChangedFields.ToList(), StreamDiffCommand.Columns);

        // Assert
        output.ShouldContain("FieldPath");
        output.ShouldContain("OldValue");
        output.ShouldContain("NewValue");
        output.ShouldContain("count");
        output.ShouldContain("41");
        output.ShouldContain("42");
    }

    [Fact]
    public async Task StreamDiffCommand_NotFound_PrintsError() {
        // Arrange
        using AdminApiClient client = CreateMockClient(null, HttpStatusCode.NotFound);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode = await StreamDiffCommand.ExecuteAsync(client, options, "acme", "counter", "01JARX7K9M2T5N", 5, 10, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void StreamDiffCommand_RequiresFromAndTo() {
        // Verify command structure has required options
        var binding = GlobalOptionsBinding.Create();
        System.CommandLine.Command command = StreamDiffCommand.Create(binding);

        System.CommandLine.Option? fromOption = command.Options.FirstOrDefault(o => o.Name == "--from");
        _ = fromOption.ShouldNotBeNull();
        fromOption.Required.ShouldBeTrue();

        System.CommandLine.Option? toOption = command.Options.FirstOrDefault(o => o.Name == "--to");
        _ = toOption.ShouldNotBeNull();
        toOption.Required.ShouldBeTrue();
    }
}
