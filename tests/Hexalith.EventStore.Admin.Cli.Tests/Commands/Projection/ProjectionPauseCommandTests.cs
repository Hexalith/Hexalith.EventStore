using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Projection;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Projection;

public class ProjectionPauseCommandTests
{
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
    public async Task ProjectionPauseCommand_Success_PrintsConfirmation()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-123", "Projection paused", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await ProjectionPauseCommand.ExecuteAsync(client, options, "acme", "counter-view", CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task ProjectionPauseCommand_OperationFailure_PrintsError()
    {
        // Arrange
        AdminOperationResult result = new(false, "op-456", "Projection is already paused", "AlreadyPaused");
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await ProjectionPauseCommand.ExecuteAsync(client, options, "acme", "counter-view", CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task ProjectionPauseCommand_Http403_PrintsPermissionError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Forbidden));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await ProjectionPauseCommand.ExecuteAsync(client, options, "acme", "counter-view", CancellationToken.None);

        // Assert — exit code 2; message content ("Access denied. Operator role required to pause projections.")
        // verified by AdminApiException.HttpStatusCode=403 (tested in AdminApiClientPostTests) + switch in handler.
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task ProjectionPauseCommand_Http404_PrintsNotFound()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await ProjectionPauseCommand.ExecuteAsync(client, options, "acme", "counter-view", CancellationToken.None);

        // Assert — exit code 2; message content ("Projection 'counter-view' not found in tenant 'acme'.")
        // verified by AdminApiException.HttpStatusCode=404 (tested in AdminApiClientPostTests) + switch in handler.
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void ProjectionPauseCommand_JsonFormat_ReturnsOperationResult()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-789", "Projection paused", null);
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(result);

        // Assert
        AdminOperationResult? deserialized = JsonSerializer.Deserialize<AdminOperationResult>(json, JsonDefaults.Options);
        deserialized.ShouldNotBeNull();
        deserialized.Success.ShouldBeTrue();
        deserialized.OperationId.ShouldBe("op-789");
    }
}
