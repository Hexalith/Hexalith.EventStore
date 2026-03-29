using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Tenant;

public class TenantCompareCommandTests
{
    private static TenantComparison CreateTestComparison()
    {
        return new TenantComparison(
            [
                new TenantSummary("acme-corp", "Acme Corporation", TenantStatusType.Active, 5000, 3),
                new TenantSummary("beta-inc", "Beta Inc", TenantStatusType.Active, 3000, 2),
            ],
            DateTimeOffset.Parse("2025-06-15T12:00:00Z"));
    }

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    [Fact]
    public async Task TenantCompareCommand_ReturnsComparisonTable()
    {
        // Arrange
        TenantComparison comparison = CreateTestComparison();
        string json = JsonSerializer.Serialize(comparison, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantCompareCommand.ExecuteAsync(client, options, ["acme-corp", "beta-inc"], CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantCompareCommand_JsonFormat_ReturnsFullComparison()
    {
        // Arrange
        TenantComparison comparison = CreateTestComparison();
        string json = JsonSerializer.Serialize(comparison, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("json");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantCompareCommand.ExecuteAsync(client, options, ["acme-corp", "beta-inc"], CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantCompareCommand_SendsCorrectPostBody()
    {
        // Arrange
        TenantComparison comparison = CreateTestComparison();
        string responseJson = JsonSerializer.Serialize(comparison, JsonDefaults.Options);
        string? capturedBody = null;
        MockHttpMessageHandler handler = new(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json"),
            };
        });
        GlobalOptions options = CreateOptions("json");
        using AdminApiClient client = new(options, handler);

        // Act
        _ = await TenantCompareCommand.ExecuteAsync(client, options, ["acme-corp", "beta-inc"], CancellationToken.None);

        // Assert
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("tenantIds");
        capturedBody.ShouldContain("acme-corp");
        capturedBody.ShouldContain("beta-inc");
    }

    [Fact]
    public async Task TenantCompareCommand_CsvFormat_ReturnsHeaderAndRows()
    {
        // Arrange
        TenantComparison comparison = CreateTestComparison();
        string json = JsonSerializer.Serialize(comparison, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("csv");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantCompareCommand.ExecuteAsync(client, options, ["acme-corp", "beta-inc"], CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantCompareCommand_ApiError_ReturnsError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server error", System.Text.Encoding.UTF8, "text/plain"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantCompareCommand.ExecuteAsync(client, options, ["acme-corp", "beta-inc"], CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
