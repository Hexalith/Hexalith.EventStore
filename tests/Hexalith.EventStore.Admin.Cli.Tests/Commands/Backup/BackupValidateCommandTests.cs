using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Backup;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Backup;

public class BackupValidateCommandTests
{
    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    private static (AdminApiClient Client, MockHttpMessageHandler Handler) CreateMockClientWithHandler(
        object responseBody,
        HttpStatusCode statusCode = HttpStatusCode.Accepted)
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
    public async Task BackupValidateCommand_Success_ReturnsOperationResult()
    {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "Validation started", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await BackupValidateCommand.ExecuteAsync(client, options, "bkp-abc123", CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task BackupValidateCommand_404_ReturnsError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await BackupValidateCommand.ExecuteAsync(client, options, "bkp-notfound", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
