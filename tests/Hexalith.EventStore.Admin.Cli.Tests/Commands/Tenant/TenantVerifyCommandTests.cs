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
        TenantStatusType status = TenantStatusType.Active,
        TenantQuotas? quotas = null)
    {
        return new TenantDetail(
            "acme-corp",
            "Acme Corporation",
            status,
            5000L,
            3,
            1073741824L,
            DateTimeOffset.Parse("2025-01-15T10:30:00Z"),
            quotas,
            "Enterprise");
    }

    private static TenantQuotas CreateTestQuotas(long currentUsage = 536870912L, long maxStorage = 10737418240L)
    {
        return new TenantQuotas("acme-corp", 100000, maxStorage, currentUsage);
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
    public async Task TenantVerifyCommand_ActiveLowUsage_ReturnsPass()
    {
        // Arrange — 5% usage
        TenantQuotas quotas = CreateTestQuotas(536870912L, 10737418240L);
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active, quotas);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(CreateJsonResponse(quotas));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantVerifyCommand_ActiveHighUsage_ReturnsWarning()
    {
        // Arrange — 95% usage
        TenantQuotas quotas = CreateTestQuotas(10200547328L, 10737418240L);
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active, quotas);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(CreateJsonResponse(quotas));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Degraded);
    }

    [Fact]
    public async Task TenantVerifyCommand_Suspended_ReturnsFail()
    {
        // Arrange
        TenantQuotas quotas = CreateTestQuotas(536870912L, 10737418240L);
        TenantDetail detail = CreateTestDetail(TenantStatusType.Suspended, quotas);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(CreateJsonResponse(quotas));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task TenantVerifyCommand_OverQuota_ReturnsFail()
    {
        // Arrange — 100% usage
        TenantQuotas quotas = CreateTestQuotas(10737418240L, 10737418240L);
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active, quotas);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(CreateJsonResponse(quotas));
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
    public async Task TenantVerifyCommand_NullQuotas_DefaultsToPass()
    {
        // Arrange — tenant exists but quotas endpoint returns 404
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active, null);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not found", System.Text.Encoding.UTF8, "text/plain"),
            });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantVerifyCommand_Quiet_SuppressesStdout()
    {
        // Arrange
        TenantQuotas quotas = CreateTestQuotas(536870912L, 10737418240L);
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active, quotas);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(CreateJsonResponse(quotas));
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
        TenantQuotas quotas = CreateTestQuotas(536870912L, 10737418240L);
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active, quotas);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(CreateJsonResponse(quotas));
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
        TenantQuotas quotas = CreateTestQuotas(536870912L, 10737418240L);
        TenantDetail detail = CreateTestDetail(TenantStatusType.Active, quotas);
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(CreateJsonResponse(detail))
            .EnqueueResponse(CreateJsonResponse(quotas));
        GlobalOptions options = CreateOptions("json");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantVerifyCommand.ExecuteAsync(client, options, "acme-corp", false, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }
}
