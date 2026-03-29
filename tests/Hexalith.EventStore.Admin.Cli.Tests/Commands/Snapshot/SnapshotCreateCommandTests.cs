using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Snapshot;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Snapshot;

public class SnapshotCreateCommandTests
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
    public async Task SnapshotCreateCommand_Success_ReturnsOperationResult()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "Snapshot created", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await SnapshotCreateCommand.ExecuteAsync(client, options, "acme", "counter", "order-123", CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task SnapshotCreateCommand_403_ReturnsError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Forbidden));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await SnapshotCreateCommand.ExecuteAsync(client, options, "acme", "counter", "order-123", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task SnapshotCreateCommand_404_ReturnsError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await SnapshotCreateCommand.ExecuteAsync(client, options, "acme", "counter", "order-123", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task SnapshotCreateCommand_SpecialCharsInIds_AreUrlEncoded()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await SnapshotCreateCommand.ExecuteAsync(client, options, "acme corp", "my/domain", "order 123", CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("acme%20corp");
        requestUri.ShouldContain("my%2Fdomain");
        requestUri.ShouldContain("order%20123");
    }
}
