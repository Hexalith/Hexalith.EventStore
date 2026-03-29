using System.CommandLine;
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Snapshot;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Snapshot;

public class SnapshotPoliciesCommandTests
{
    private static List<SnapshotPolicy> CreateTestResult(int count = 2)
    {
        List<SnapshotPolicy> items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(new SnapshotPolicy(
                $"tenant-{i}",
                $"domain-{i}",
                $"aggregate-{i}",
                (i + 1) * 50,
                DateTimeOffset.UtcNow));
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
    public void SnapshotPoliciesCommand_ReturnsTable()
    {
        // Arrange
        List<SnapshotPolicy> result = CreateTestResult();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(result, SnapshotPoliciesCommand.Columns);

        // Assert
        output.ShouldContain("Tenant ID");
        output.ShouldContain("Domain");
        output.ShouldContain("Aggregate Type");
        output.ShouldContain("Interval");
        output.ShouldContain("tenant-0");
        output.ShouldContain("domain-1");
    }

    [Fact]
    public async Task SnapshotPoliciesCommand_EmptyResult_PrintsNoPoliciesFound()
    {
        // Arrange
        List<SnapshotPolicy> result = [];
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client)
        {
            exitCode = await SnapshotPoliciesCommand.ExecuteAsync(client, options, null, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public void SnapshotPoliciesCommand_JsonFormat_ReturnsValidJson()
    {
        // Arrange
        List<SnapshotPolicy> result = CreateTestResult();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(result);

        // Assert
        List<SnapshotPolicy>? deserialized = JsonSerializer.Deserialize<List<SnapshotPolicy>>(json, JsonDefaults.Options);
        deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SnapshotPoliciesCommand_TenantFilter_PassesQueryParam()
    {
        // Arrange
        List<SnapshotPolicy> result = CreateTestResult();
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client)
        {
            _ = await SnapshotPoliciesCommand.ExecuteAsync(client, options, "acme", CancellationToken.None);
        }

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("tenantId=acme");
    }
}
