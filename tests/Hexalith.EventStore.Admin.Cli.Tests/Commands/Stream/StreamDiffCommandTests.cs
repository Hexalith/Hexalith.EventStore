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

public class StreamDiffCommandTests {
    private static AggregateStateDiff CreateTestDiff()
        => new(5, 10,
        [
            FieldChange.Redacted(
                "count",
                AdminRedactedContent.Protected(
                    contentKind: "field-value", reasonCode: "protected-content", stage: "cli-test",
                    metadataVersion: null, retryable: false, permanent: false,
                    safeNextAction: "Inspect descriptor."),
                AdminRedactedContent.Protected(
                    contentKind: "field-value", reasonCode: "protected-content", stage: "cli-test",
                    metadataVersion: null, retryable: false, permanent: false,
                    safeNextAction: "Inspect descriptor.")),
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
    public void StreamDiffCommand_RendersDescriptorContent_WhenPresent() {
        // Arrange — descriptor-bearing field renders the descriptor; raw-only field renders empty (D4 fail-closed).
        AggregateStateDiff diff = CreateTestDiff();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(diff.ChangedFields.ToList(), StreamDiffCommand.Columns);

        // Assert
        output.ShouldContain("FieldPath");
        output.ShouldContain("Old");
        output.ShouldContain("New");
        output.ShouldContain("count");
        output.ShouldContain("lastUpdated");
        output.ShouldContain("Protected content redacted.");
    }

    [Fact]
    public void StreamDiffCommand_RawOnlyChange_RendersEmptyOldNew_FailClosed() {
        // Arrange — D4: when descriptor is null, raw OldValue/NewValue must NOT appear in table output.
        AggregateStateDiff diff = new(5, 10, [new FieldChange("count", "41", "42")]);
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(diff.ChangedFields.ToList(), StreamDiffCommand.Columns);

        // Assert — field path visible, raw values absent (no fallback to OldValue/NewValue).
        output.ShouldContain("count");
        output.ShouldNotContain("41");
        output.ShouldNotContain("42");
    }

    [Fact]
    public void StreamDiffCommand_CsvRawOnlyChange_RendersEmptyOldNew_FailClosed() {
        // Arrange — same D4 fail-closed test for CSV output.
        AggregateStateDiff diff = new(5, 10, [new FieldChange("count", "41", "42")]);
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string output = formatter.FormatCollection(diff.ChangedFields.ToList(), StreamDiffCommand.Columns);

        // Assert
        output.ShouldContain("count");
        output.ShouldNotContain("41");
        output.ShouldNotContain("42");
    }

    [Fact]
    public void StreamDiffCommand_FailClosed_NoSentinelLeak_WhenRawOnlyChangeContainsSentinel() {
        // P17: raw OldValue/NewValue containing a sentinel must not leak through fallback (now removed by D4).
        AggregateStateDiff diff = new(
            5, 10,
            [new FieldChange("count", ProtectedDataLeakSentinel.ProtectedPayloadPlaintext, ProtectedDataLeakSentinel.ProtectedPayloadPlaintext)]);
        IOutputFormatter table = new TableOutputFormatter();
        IOutputFormatter csv = new CsvOutputFormatter();

        // Act
        string tableOutput = table.FormatCollection(diff.ChangedFields.ToList(), StreamDiffCommand.Columns);
        string csvOutput = csv.FormatCollection(diff.ChangedFields.ToList(), StreamDiffCommand.Columns);

        // Assert
        ProtectedDataLeakSentinel.AssertNoLeak([tableOutput, csvOutput]);
    }

    [Fact]
    public void StreamDiffCommand_TableColumns_DoNotSelectRawFieldValues() {
        IReadOnlyList<string> propertyNames = StreamDiffCommand.Columns
            .Select(c => c.PropertyName)
            .ToList();

        propertyNames.ShouldNotContain("OldValue");
        propertyNames.ShouldNotContain("NewValue");
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
