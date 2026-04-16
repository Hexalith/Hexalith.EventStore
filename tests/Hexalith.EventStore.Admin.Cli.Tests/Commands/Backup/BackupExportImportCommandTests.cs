using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Backup;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Backup;

public class BackupExportImportCommandTests : IDisposable {
    private readonly List<string> _tempFiles = [];

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    private static (AdminApiClient Client, MockHttpMessageHandler Handler) CreateMockClientWithHandler(
        object responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK) {
        string json = JsonSerializer.Serialize(responseBody, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(statusCode) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions();
        return (new AdminApiClient(options, handler), handler);
    }

    [Fact]
    public async Task BackupExportStreamCommand_Success_ReturnsSummary() {
        // Arrange
        StreamExportResult result = new(true, "acme", "counter", "order-123", 42, "[{\"type\":\"OrderCreated\"}]", "acme_counter_order-123.json", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await BackupExportStreamCommand.ExecuteAsync(client, options, "acme", "counter", "order-123", "JSON", CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task BackupExportStreamCommand_Failed_ReturnsError() {
        // Arrange
        StreamExportResult result = new(false, "acme", "counter", "order-123", 0, null, null, "Aggregate not found");
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await BackupExportStreamCommand.ExecuteAsync(client, options, "acme", "counter", "order-123", "JSON", CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task BackupExportStreamCommand_ExportFormat_PassesInBody() {
        // Arrange
        StreamExportResult result = new(true, "acme", "counter", "order-123", 10, "content", "file.json", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client) {
            _ = await BackupExportStreamCommand.ExecuteAsync(client, options, "acme", "counter", "order-123", "CloudEvents", CancellationToken.None);
        }

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        _ = handler.LastRequest.Content.ShouldNotBeNull();
        string body = await handler.LastRequest.Content!.ReadAsStringAsync();
        body.ShouldContain("CloudEvents");
    }

    [Fact]
    public async Task BackupImportStreamCommand_Success_ReturnsOperationResult() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "Imported", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        string tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);
        await File.WriteAllTextAsync(tempFile, "[{\"type\":\"OrderCreated\"}]");

        // Act
        int exitCode;
        using (client) {
            exitCode = await BackupImportStreamCommand.ExecuteAsync(client, options, "acme", tempFile, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
        _ = handler.LastRequest.ShouldNotBeNull();
    }

    [Fact]
    public async Task BackupImportStreamCommand_FileNotFound_ReturnsError() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await BackupImportStreamCommand.ExecuteAsync(client, options, "acme", "/nonexistent/file.json", CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task BackupImportStreamCommand_PassesTenantIdInQueryString() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "Imported", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        string tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);
        await File.WriteAllTextAsync(tempFile, "[]");

        // Act
        using (client) {
            _ = await BackupImportStreamCommand.ExecuteAsync(client, options, "acme-corp", tempFile, CancellationToken.None);
        }

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("tenantId=acme-corp");
    }

    public void Dispose() {
        foreach (string file in _tempFiles) {
            try {
                File.Delete(file);
            }
            catch {
                // Ignore cleanup failures in tests
            }
        }

        GC.SuppressFinalize(this);
    }
}
