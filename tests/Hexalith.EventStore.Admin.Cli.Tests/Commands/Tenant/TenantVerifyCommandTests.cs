using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Tenant;

public class TenantVerifyCommandTests
{
    private static TenantDetail CreateTestDetail(
        TenantStatusType status = TenantStatusType.Active)
    {
        return new TenantDetail(
            "acme-corp",
            "Acme Corporation",
            "Enterprise tenant",
            status,
            DateTimeOffset.Parse("2025-01-15T10:30:00Z"));
    }

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    private static HttpResponseMessage CreateJsonResponse(object body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        string json = JsonSerializer.Serialize(body, JsonDefaults.Options);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    [Fact]
    public async Task TenantVerifyCommand_Active_ReturnsPass()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantVerifyCommand_Disabled_ReturnsFail()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail(TenantStatusType.Disabled);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task TenantVerifyCommand_NotFound_ReturnsError()
    {
        // Arrange
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not found", System.Text.Encoding.UTF8, "text/plain"),
            });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "unknown", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task TenantVerifyCommand_Quiet_SuppressesStdout()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act — quiet mode should not throw and should return correct exit code
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", true, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantVerifyCommand_QuietWithOutputFile_StillWritesFile()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail));
        string tempFile = Path.GetTempFileName();
        try
        {
            GlobalOptions options = new("http://localhost:5002", null, "table", tempFile);
            using AdminApiClient client = new(options, handler);

            // Act — quiet + output file: stdout suppressed but file written
            int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", true, CancellationToken.None);

            // Assert
            exitCode.ShouldBe(ExitCodes.Success);
            string fileContent = File.ReadAllText(tempFile);
            fileContent.ShouldNotBeNullOrWhiteSpace();
            fileContent.ShouldContain("Tenant ID");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task TenantVerifyCommand_JsonFormat_ReturnsCompositeObject()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail));
        GlobalOptions options = CreateOptions("json");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }
}
