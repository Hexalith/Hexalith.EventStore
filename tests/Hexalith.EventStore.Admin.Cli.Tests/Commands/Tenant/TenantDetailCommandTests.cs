using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Tenant;

public class TenantDetailCommandTests
{
    private static TenantDetail CreateTestDetail(TenantQuotas? quotas = null)
    {
        return new TenantDetail(
            "acme-corp",
            "Acme Corporation",
            TenantStatusType.Active,
            5000L,
            3,
            1073741824L,
            DateTimeOffset.Parse("2025-01-15T10:30:00Z"),
            quotas ?? new TenantQuotas("acme-corp", 100000, 10737418240L, 536870912L),
            "Enterprise");
    }

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    [Fact]
    public async Task TenantDetailCommand_ReturnsTenantInfo()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail();
        string json = JsonSerializer.Serialize(detail, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantDetailCommand.ExecuteAsync(client, options, "acme-corp", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantDetailCommand_NotFound_ReturnsError()
    {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not found", System.Text.Encoding.UTF8, "text/plain"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantDetailCommand.ExecuteAsync(client, options, "xyz", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task TenantDetailCommand_JsonFormat_ReturnsFullDetail()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail();
        string json = JsonSerializer.Serialize(detail, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("json");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantDetailCommand.ExecuteAsync(client, options, "acme-corp", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantDetailCommand_SpecialCharsInTenantId_AreUrlEncoded()
    {
        // Arrange
        TenantDetail detail = CreateTestDetail();
        string json = JsonSerializer.Serialize(detail, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        _ = await TenantDetailCommand.ExecuteAsync(client, options, "acme corp/test", CancellationToken.None);

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("acme%20corp%2Ftest");
    }
}
