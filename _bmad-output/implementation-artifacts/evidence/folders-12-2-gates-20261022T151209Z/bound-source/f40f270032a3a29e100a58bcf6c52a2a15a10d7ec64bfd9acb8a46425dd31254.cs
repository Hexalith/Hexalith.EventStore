using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Tenant;

public class TenantUsersCommandTests {
    private static List<TenantUser> CreateTestUsers() => [
            new TenantUser("alice-001", "Admin"),
            new TenantUser("bob-002", "Viewer"),
        ];

    private static GlobalOptions CreateOptions(string format = "table")
        => new("http://localhost:5002", null, format, null);

    [Fact]
    public async Task TenantUsersCommand_ReturnsUserTable() {
        // Arrange
        List<TenantUser> users = CreateTestUsers();
        string json = JsonSerializer.Serialize(users, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantUsersCommand.ExecuteAsync(client, options, "acme-corp", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantUsersCommand_EmptyResult_PrintsNoUsersFound() {
        // Arrange
        List<TenantUser> users = [];
        string json = JsonSerializer.Serialize(users, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantUsersCommand.ExecuteAsync(client, options, "acme-corp", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task TenantUsersCommand_NotFound_ReturnsError() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound) {
            Content = new StringContent("Not found", System.Text.Encoding.UTF8, "text/plain"),
        });
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await TenantUsersCommand.ExecuteAsync(client, options, "unknown", CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public void TenantUsersCommand_CsvFormat_ReturnsHeaderAndRows() {
        // Arrange
        List<TenantUser> users = CreateTestUsers();
        IOutputFormatter formatter = new CsvOutputFormatter();

        // Act
        string csv = formatter.FormatCollection(users, TenantUsersCommand.Columns);

        // Assert
        string[] lines = csv.Split(Environment.NewLine);
        lines[0].ShouldContain("User ID");
        lines[0].ShouldContain("Role");
        lines.Length.ShouldBe(3); // header + 2 rows
        lines[1].ShouldContain("alice-001");
        lines[2].ShouldContain("bob-002");
    }
}
