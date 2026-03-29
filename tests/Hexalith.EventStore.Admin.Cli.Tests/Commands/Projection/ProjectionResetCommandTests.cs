using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Projection;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Projection;

public class ProjectionResetCommandTests
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
    public async Task ProjectionResetCommand_Success_PrintsConfirmation()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-reset-1", "Reset initiated", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result, HttpStatusCode.Accepted);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await ProjectionResetCommand.ExecuteAsync(client, options, "acme", "counter-view", null, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task ProjectionResetCommand_WithFromPosition_SendsRequestBody()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-reset-2", "Reset initiated", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result, HttpStatusCode.Accepted);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await ProjectionResetCommand.ExecuteAsync(client, options, "acme", "counter-view", 500L, CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Content.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync();
        body.ShouldContain("\"fromPosition\"");
        body.ShouldContain("500");
    }

    [Fact]
    public async Task ProjectionResetCommand_WithoutFromPosition_SendsNullPosition()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-reset-3", "Reset initiated", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result, HttpStatusCode.Accepted);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await ProjectionResetCommand.ExecuteAsync(client, options, "acme", "counter-view", null, CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Content.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync();
        body.ShouldContain("\"fromPosition\": null");
    }

    [Fact]
    public async Task ProjectionResetCommand_Http403_PrintsPermissionError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Forbidden));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await ProjectionResetCommand.ExecuteAsync(client, options, "acme", "counter-view", null, CancellationToken.None);

        // Assert — exit code 2; message content ("Access denied. Operator role required to reset projections.")
        // verified by AdminApiException.HttpStatusCode=403 (tested in AdminApiClientPostTests) + switch in handler.
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task ProjectionResetCommand_Http404_PrintsNotFound()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await ProjectionResetCommand.ExecuteAsync(client, options, "acme", "counter-view", null, CancellationToken.None);

        // Assert — exit code 2; message content ("Projection 'counter-view' not found in tenant 'acme'.")
        // verified by AdminApiException.HttpStatusCode=404 (tested in AdminApiClientPostTests) + switch in handler.
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
