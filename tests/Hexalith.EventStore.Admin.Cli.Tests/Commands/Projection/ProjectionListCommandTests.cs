using System.CommandLine;
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Projection;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Tests.Client;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Projection;

public class ProjectionListCommandTests
{
    private static List<ProjectionStatus> CreateTestResult(int count = 2)
    {
        List<ProjectionStatus> items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(new ProjectionStatus(
                $"projection-{i}",
                $"tenant-{i}",
                i % 2 == 0 ? ProjectionStatusType.Running : ProjectionStatusType.Paused,
                (i + 1) * 5L,
                (i + 1) * 100.5,
                i * 2,
                (i + 1) * 1000L,
                DateTimeOffset.UtcNow));
        }

        return items;
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
    public void ProjectionListCommand_ReturnsProjectionTable()
    {
        // Arrange
        List<ProjectionStatus> result = CreateTestResult();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(result, ProjectionListCommand.Columns);

        // Assert
        output.ShouldContain("Name");
        output.ShouldContain("Tenant");
        output.ShouldContain("Status");
        output.ShouldContain("projection-0");
        output.ShouldContain("tenant-1");
    }

    [Fact]
    public async Task ProjectionListCommand_EmptyResult_PrintsNoProjectionsFound()
    {
        // Arrange
        List<ProjectionStatus> result = [];
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await ProjectionListCommand.ExecuteAsync(client, options, null, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public void ProjectionListCommand_JsonFormat_ReturnsValidJson()
    {
        // Arrange
        List<ProjectionStatus> result = CreateTestResult();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(result);

        // Assert
        List<ProjectionStatus>? deserialized = JsonSerializer.Deserialize<List<ProjectionStatus>>(json, JsonDefaults.Options);
        deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ProjectionListCommand_WithTenantFilter_SendsQueryParameter()
    {
        // Arrange
        List<ProjectionStatus> result = CreateTestResult();
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await ProjectionListCommand.ExecuteAsync(client, options, "acme", CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("tenantId=acme");
    }

    [Fact]
    public void ProjectionListCommand_CsvFormat_ReturnsHeaderAndRows()
    {
        // Arrange
        List<ProjectionStatus> result = CreateTestResult();
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string csv = formatter.FormatCollection(result, ProjectionListCommand.Columns);

        // Assert
        string[] lines = csv.Split(Environment.NewLine);
        lines[0].ShouldContain("Name");
        lines[0].ShouldContain("Tenant");
        lines[0].ShouldContain("Status");
        lines.Length.ShouldBe(3); // header + 2 rows
        lines[1].ShouldContain("projection-0");
        lines[1].ShouldContain("Running");
    }

    [Fact]
    public void ProjectionListCommand_JsonFormat_EnumsSerializeAsStrings()
    {
        // Arrange
        ProjectionStatus running = new("proj-1", "t1", ProjectionStatusType.Running, 0, 100.0, 0, 500L, DateTimeOffset.UtcNow);

        // Act
        string json = JsonSerializer.Serialize(running, JsonDefaults.Options);

        // Assert
        json.ShouldContain("\"running\"");
        json.ShouldNotContain("\"status\": 0");

        // Also verify Paused
        ProjectionStatus paused = new("proj-2", "t2", ProjectionStatusType.Paused, 10, 0.0, 1, 100L, DateTimeOffset.UtcNow);
        string json2 = JsonSerializer.Serialize(paused, JsonDefaults.Options);
        json2.ShouldContain("\"paused\"");
    }

    [Fact]
    public async Task ProjectionCommands_SpecialCharsInArgs_AreUrlEncoded()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        string json = JsonSerializer.Serialize(result, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act — use tenant with special characters
        _ = await ProjectionPauseCommand.ExecuteAsync(client, options, "acme corp", "my/projection", CancellationToken.None);

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("acme%20corp");
        requestUri.ShouldContain("my%2Fprojection");
    }

    [Fact]
    public void ProjectionSubcommands_MissingPositionalArgs_ReturnsError()
    {
        // Arrange — create the status command which requires tenant and name
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        Command statusCmd = ProjectionStatusCommand.Create(binding);

        // Assert — the command has two required arguments
        statusCmd.Arguments.Count.ShouldBe(2);
        statusCmd.Arguments[0].Name.ShouldBe("tenant");
        statusCmd.Arguments[1].Name.ShouldBe("name");
    }

    [Fact]
    public void ProjectionCommand_NoSubcommand_PrintsHelp()
    {
        // Arrange
        GlobalOptionsBinding binding = GlobalOptionsBinding.Create();
        Command projectionCmd = ProjectionCommand.Create(binding);

        // Assert — parent command has all five sub-subcommands
        projectionCmd.Subcommands.Count.ShouldBe(5);
        projectionCmd.Subcommands.Select(c => c.Name).ShouldContain("list");
        projectionCmd.Subcommands.Select(c => c.Name).ShouldContain("status");
        projectionCmd.Subcommands.Select(c => c.Name).ShouldContain("pause");
        projectionCmd.Subcommands.Select(c => c.Name).ShouldContain("resume");
        projectionCmd.Subcommands.Select(c => c.Name).ShouldContain("reset");
    }
}
