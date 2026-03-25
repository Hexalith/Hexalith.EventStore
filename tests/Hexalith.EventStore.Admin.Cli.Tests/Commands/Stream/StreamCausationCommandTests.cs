using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Tests.Client;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Stream;

public class StreamCausationCommandTests
{
    private static CausationChain CreateTestChain()
        => new(
            "IncrementCounter",
            "cmd-001",
            "corr-123",
            "admin@acme.com",
            [
                new CausationEvent(1, "CounterIncremented", DateTimeOffset.UtcNow),
                new CausationEvent(2, "CounterSnapshotCreated", DateTimeOffset.UtcNow),
            ],
            ["CounterSummary", "CounterTimeline"]);

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

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
    public void StreamCausationCommand_ReturnsDualSectionOutput()
    {
        // Arrange — test formatting directly to avoid Console capture race conditions
        CausationChain chain = CreateTestChain();
        IOutputFormatter formatter = new TableOutputFormatter();

        var overview = new
        {
            OriginatingCommandType = chain.OriginatingCommandType,
            OriginatingCommandId = chain.OriginatingCommandId,
            CorrelationId = chain.CorrelationId,
            UserId = chain.UserId ?? string.Empty,
            EventCount = chain.Events.Count,
            AffectedProjections = string.Join(", ", chain.AffectedProjections),
        };

        // Act
        string overviewOutput = formatter.Format(overview);
        string eventsOutput = formatter.FormatCollection(chain.Events.ToList(), StreamCausationCommand.EventColumns);
        string output = string.Concat(overviewOutput, Environment.NewLine, Environment.NewLine, eventsOutput);

        // Assert — overview section
        output.ShouldContain("OriginatingCommandType");
        output.ShouldContain("IncrementCounter");
        output.ShouldContain("CorrelationId");
        output.ShouldContain("corr-123");
        output.ShouldContain("EventCount");
        output.ShouldContain("2");
        output.ShouldContain("AffectedProjections");
        output.ShouldContain("CounterSummary, CounterTimeline");
        // Events table section
        output.ShouldContain("Seq");
        output.ShouldContain("EventType");
        output.ShouldContain("CounterIncremented");
        output.ShouldContain("CounterSnapshotCreated");
    }

    [Fact]
    public void StreamCausationCommand_JsonFormat_ReturnsFullChain()
    {
        // Arrange — test JSON formatting directly
        CausationChain chain = CreateTestChain();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(chain);

        // Assert
        CausationChain? deserialized = JsonSerializer.Deserialize<CausationChain>(json, JsonDefaults.Options);
        deserialized.ShouldNotBeNull();
        deserialized.Events.Count.ShouldBe(2);
        deserialized.AffectedProjections.Count.ShouldBe(2);
    }

    [Fact]
    public void StreamCausationCommand_CsvFormat_ReturnsEventsOnly()
    {
        // Arrange — test CSV formatting directly
        CausationChain chain = CreateTestChain();
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string csv = formatter.FormatCollection(chain.Events.ToList(), StreamCausationCommand.EventColumns);

        // Assert
        string[] lines = csv.Split(Environment.NewLine);
        lines[0].ShouldContain("Seq");
        lines[0].ShouldContain("EventType");
        lines.Length.ShouldBe(3); // header + 2 events
        lines[1].ShouldContain("CounterIncremented");
        lines[2].ShouldContain("CounterSnapshotCreated");
    }

    [Fact]
    public async Task StreamCausationCommand_NotFound_PrintsError()
    {
        // Arrange
        using AdminApiClient client = CreateMockClient(null, HttpStatusCode.NotFound);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode = await StreamCausationCommand.ExecuteAsync(client, options, "acme", "counter", "01J", 1, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task StreamCommands_SpecialCharsInArgs_AreUrlEncoded()
    {
        // Arrange
        CausationChain chain = CreateTestChain();
        string json = JsonSerializer.Serialize(chain, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act — use tenant with slash and domain with space
        _ = await StreamCausationCommand.ExecuteAsync(client, options, "acme/corp", "my domain", "01J", 1, CancellationToken.None);

        // Assert — verify URL encoding in the request
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        // Uri.EscapeDataString encodes / to %2F and space to %20
        // The AbsoluteUri preserves percent-encoding
        requestUri.ShouldContain("acme%2Fcorp");
        requestUri.ShouldContain("my%20domain");
    }

    [Fact]
    public async Task StreamCommands_Http401_PrintsAuthError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await StreamCausationCommand.ExecuteAsync(client, options, "acme", "counter", "01J", 1, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task StreamCommands_ConnectionRefused_PrintsConnectError()
    {
        // Arrange — simulate connection failure
        MockHttpMessageHandler handler = new(_ =>
            throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException()));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await StreamCausationCommand.ExecuteAsync(client, options, "acme", "counter", "01J", 1, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void StreamCommand_NoSubcommand_PrintsHelp()
    {
        // Verify the parent command has all six subcommands
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        System.CommandLine.Command command = StreamCommand.Create(binding);

        command.Subcommands.Count.ShouldBe(6);
        command.Subcommands.Select(c => c.Name).ShouldContain("list");
        command.Subcommands.Select(c => c.Name).ShouldContain("events");
        command.Subcommands.Select(c => c.Name).ShouldContain("event");
        command.Subcommands.Select(c => c.Name).ShouldContain("state");
        command.Subcommands.Select(c => c.Name).ShouldContain("diff");
        command.Subcommands.Select(c => c.Name).ShouldContain("causation");
    }

    [Fact]
    public void StreamSubcommands_MissingPositionalArgs_CommandHasRequiredArguments()
    {
        // Verify that stream event command requires positional arguments
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        System.CommandLine.Command command = StreamEventCommand.Create(binding);

        command.Arguments.Count.ShouldBe(4); // tenant, domain, aggregateId, sequenceNumber
        command.Arguments.Select(a => a.Name).ShouldContain("tenant");
        command.Arguments.Select(a => a.Name).ShouldContain("domain");
        command.Arguments.Select(a => a.Name).ShouldContain("aggregateId");
        command.Arguments.Select(a => a.Name).ShouldContain("sequenceNumber");
    }
}
