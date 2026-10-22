using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Backup;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Backup;

public class BackupTriggerCommandTests {
    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    private static (AdminApiClient Client, MockHttpMessageHandler Handler) CreateMockClientWithHandler(
        object responseBody,
        HttpStatusCode statusCode = HttpStatusCode.Accepted) {
        string json = JsonSerializer.Serialize(responseBody, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(statusCode) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions();
        return (new AdminApiClient(options, handler), handler);
    }

    [Fact]
    public async Task BackupTriggerCommand_Success_ReturnsOperationResult() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "Backup triggered", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await BackupTriggerCommand.ExecuteAsync(client, options, "acme", null, false, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task BackupTriggerCommand_NoSnapshots_PassesFalse() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client) {
            _ = await BackupTriggerCommand.ExecuteAsync(client, options, "acme", null, true, CancellationToken.None);
        }

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("includeSnapshots=false");
    }

    [Fact]
    public async Task BackupTriggerCommand_Description_PassesQueryParam() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client) {
            _ = await BackupTriggerCommand.ExecuteAsync(client, options, "acme", "Weekly backup", false, CancellationToken.None);
        }

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("description=Weekly%20backup");
    }

    [Fact]
    public async Task BackupTriggerCommand_403_ReturnsError() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Forbidden));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await BackupTriggerCommand.ExecuteAsync(client, options, "acme", null, false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
