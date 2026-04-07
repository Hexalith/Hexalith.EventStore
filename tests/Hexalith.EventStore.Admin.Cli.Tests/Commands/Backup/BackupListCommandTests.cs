using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Backup;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Backup;

public class BackupListCommandTests
{
    private static List<BackupJob> CreateTestResult(int count = 2)
    {
        List<BackupJob> items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(new BackupJob(
                $"bkp-{i}",
                $"tenant-{i}",
                null,
                $"Backup {i}",
                BackupJobType.Backup,
                i % 2 == 0 ? BackupJobStatus.Completed : BackupJobStatus.Running,
                true,
                DateTimeOffset.UtcNow,
                i % 2 == 0 ? DateTimeOffset.UtcNow : null,
                (i + 1) * 1000L,
                (i + 1) * 5000L,
                i % 2 == 0,
                null));
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
    public void BackupListCommand_ReturnsTable()
    {
        // Arrange
        List<BackupJob> result = CreateTestResult();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(result, BackupListCommand.Columns);

        // Assert
        output.ShouldContain("Backup ID");
        output.ShouldContain("Tenant");
        output.ShouldContain("Type");
        output.ShouldContain("Status");
        output.ShouldContain("bkp-0");
        output.ShouldContain("tenant-1");
    }

    [Fact]
    public async Task BackupListCommand_EmptyResult_PrintsNoJobsFound()
    {
        // Arrange
        List<BackupJob> result = [];
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await BackupListCommand.ExecuteAsync(client, options, null, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public void BackupListCommand_JsonFormat_ReturnsValidJson()
    {
        // Arrange
        List<BackupJob> result = CreateTestResult();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(result);

        // Assert
        List<BackupJob>? deserialized = JsonSerializer.Deserialize<List<BackupJob>>(json, JsonDefaults.Options);
        deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(2);
    }

    [Fact]
    public void BackupListCommand_EnumsSerializeAsStrings()
    {
        // Arrange
        BackupJob completed = new("bkp-1", "t1", null, null, BackupJobType.Backup, BackupJobStatus.Completed, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 100L, 5000L, true, null);

        // Act
        string json = JsonSerializer.Serialize(completed, JsonDefaults.Options);

        // Assert
        json.ShouldContain("\"completed\"");
        json.ShouldContain("\"backup\"");
        json.ShouldNotContain("\"status\": 2");
        json.ShouldNotContain("\"jobType\": 0");
    }

    [Fact]
    public async Task BackupListCommand_TenantFilter_PassesQueryParam()
    {
        // Arrange
        List<BackupJob> result = CreateTestResult();
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await BackupListCommand.ExecuteAsync(client, options, "acme", CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("tenantId=acme");
    }
}
