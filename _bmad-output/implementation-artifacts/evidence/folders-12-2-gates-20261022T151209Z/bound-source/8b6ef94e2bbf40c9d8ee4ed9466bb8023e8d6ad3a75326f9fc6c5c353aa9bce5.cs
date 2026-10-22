using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Backup;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Backup;

public class BackupRestoreCommandTests {
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
    public async Task BackupRestoreCommand_Success_ReturnsOperationResult() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "Restore initiated", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await BackupRestoreCommand.ExecuteAsync(client, options, "bkp-abc123", null, false, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task BackupRestoreCommand_DryRun_PassesQueryParam() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client) {
            _ = await BackupRestoreCommand.ExecuteAsync(client, options, "bkp-abc123", null, true, CancellationToken.None);
        }

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("dryRun=true");
    }

    [Fact]
    public async Task BackupRestoreCommand_PointInTime_PassesQueryParam() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");
        DateTimeOffset pointInTime = new(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);

        // Act
        using (client) {
            _ = await BackupRestoreCommand.ExecuteAsync(client, options, "bkp-abc123", pointInTime, false, CancellationToken.None);
        }

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("pointInTime=");
        requestUri.ShouldContain("2026");
    }

    [Fact]
    public async Task BackupRestoreCommand_404_ReturnsError() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await BackupRestoreCommand.ExecuteAsync(client, options, "bkp-notfound", null, false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
