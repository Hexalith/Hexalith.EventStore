using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Tenant;

public class TenantListCommandTests {
    private static List<TenantSummary> CreateTestResult(int count = 2) {
        List<TenantSummary> items = [];
        for (int i = 0; i < count; i++) {
            items.Add(new TenantSummary(
                $"tenant-{i}",
                $"Tenant {i}",
                i % 2 == 0 ? TenantStatusType.Active : TenantStatusType.Disabled));
        }

        return items;
    }

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
    public void TenantListCommand_ReturnsTable() {
        // Arrange
        List<TenantSummary> result = CreateTestResult();
        IOutputFormatter formatter = new TableOutputFormatter();

        // Act
        string output = formatter.FormatCollection(result, TenantListCommand.Columns);

        // Assert
        output.ShouldContain("Tenant ID");
        output.ShouldContain("Name");
        output.ShouldContain("Status");
        output.ShouldContain("tenant-0");
        output.ShouldContain("Tenant 1");
    }

    [Fact]
    public async Task TenantListCommand_EmptyResult_PrintsNoTenantsFound() {
        // Arrange
        List<TenantSummary> result = [];
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await TenantListCommand.ExecuteAsync(client, options, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public void TenantListCommand_JsonFormat_ReturnsValidJson() {
        // Arrange
        List<TenantSummary> result = CreateTestResult();
        IOutputFormatter formatter = new JsonOutputFormatter();

        // Act
        string json = formatter.Format(result);

        // Assert
        List<TenantSummary>? deserialized = JsonSerializer.Deserialize<List<TenantSummary>>(json, JsonDefaults.Options);
        _ = deserialized.ShouldNotBeNull();
        deserialized.Count.ShouldBe(2);
    }

    [Fact]
    public void TenantListCommand_CsvFormat_ReturnsHeaderAndRows() {
        // Arrange
        List<TenantSummary> result = CreateTestResult();
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string csv = formatter.FormatCollection(result, TenantListCommand.Columns);

        // Assert
        string[] lines = csv.Split(Environment.NewLine);
        lines[0].ShouldContain("Tenant ID");
        lines[0].ShouldContain("Name");
        lines[0].ShouldContain("Status");
        lines.Length.ShouldBe(3); // header + 2 rows
        lines[1].ShouldContain("tenant-0");
    }

    [Fact]
    public void TenantListCommand_EnumsSerializeAsStrings() {
        // Arrange
        TenantSummary active = new("t1", "Tenant 1", TenantStatusType.Active);

        // Act
        string json = JsonSerializer.Serialize(active, JsonDefaults.Options);

        // Assert
        json.ShouldContain("\"active\"");
        json.ShouldNotContain("\"status\": 0");

        // Also verify Disabled
        TenantSummary disabled = new("t2", "Tenant 2", TenantStatusType.Disabled);
        string json2 = JsonSerializer.Serialize(disabled, JsonDefaults.Options);
        json2.ShouldContain("\"disabled\"");
    }

    [Fact]
    public async Task TenantListCommand_Unauthorized_ReturnsError() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Unauthorized) {
            Content = new StringContent("Unauthorized", System.Text.Encoding.UTF8, "text/plain"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantListCommand.ExecuteAsync(client, options, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
