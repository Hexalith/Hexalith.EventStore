using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Tests.Client;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Tenant;

public class TenantQuotasCommandTests
{
    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    [Fact]
    public async Task TenantQuotasCommand_ReturnsQuotas()
    {
        // Arrange — usage at 50%
        TenantQuotas quotas = new("acme-corp", 100000, 10737418240L, 5368709120L);
        string json = JsonSerializer.Serialize(quotas, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantQuotasCommand.ExecuteAsync(client, options, "acme-corp", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantQuotasCommand_HighUsage_ReturnsWarning()
    {
        // Arrange — usage at 95%
        TenantQuotas quotas = new("acme-corp", 100000, 10737418240L, 10200547328L);
        string json = JsonSerializer.Serialize(quotas, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantQuotasCommand.ExecuteAsync(client, options, "acme-corp", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Degraded);
    }

    [Fact]
    public async Task TenantQuotasCommand_JsonFormat_ReturnsValidJson()
    {
        // Arrange
        TenantQuotas quotas = new("acme-corp", 100000, 10737418240L, 5368709120L);
        string json = JsonSerializer.Serialize(quotas, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("json");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantQuotasCommand.ExecuteAsync(client, options, "acme-corp", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public void TenantQuotasCommand_CsvFormat_ReturnsColumnData()
    {
        // Arrange
        TenantQuotas quotas = new("acme-corp", 100000, 10737418240L, 5368709120L);
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string csv = formatter.Format(quotas, TenantQuotasCommand.Columns);

        // Assert
        csv.ShouldContain("Tenant ID");
        csv.ShouldContain("Max Events/Day");
        csv.ShouldContain("acme-corp");
    }

    [Fact]
    public async Task TenantQuotasCommand_NotFound_ReturnsError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not found", System.Text.Encoding.UTF8, "text/plain"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantQuotasCommand.ExecuteAsync(client, options, "unknown", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
