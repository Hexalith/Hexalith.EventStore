using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands.Snapshot;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands.Snapshot;

public class SnapshotSetPolicyCommandTests {
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
    public async Task SnapshotSetPolicyCommand_Success_ReturnsOperationResult() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "Policy set", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await SnapshotSetPolicyCommand.ExecuteAsync(client, options, "acme", "counter", "Counter", 50, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
        _ = handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Method.ShouldBe(HttpMethod.Put);
    }

    [Fact]
    public async Task SnapshotSetPolicyCommand_PassesIntervalInQueryString() {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, MockHttpMessageHandler handler) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        using (client) {
            _ = await SnapshotSetPolicyCommand.ExecuteAsync(client, options, "acme", "counter", "Counter", 100, CancellationToken.None);
        }

        // Assert
        _ = handler.LastRequest.ShouldNotBeNull();
        string requestUri = handler.LastRequest.RequestUri!.AbsoluteUri;
        requestUri.ShouldContain("intervalEvents=100");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SnapshotSetPolicyCommand_InvalidInterval_ReturnsError(int intervalEvents) {
        // Arrange
        AdminOperationResult result = new(true, "op-1", "OK", null);
        (AdminApiClient client, _) = CreateMockClientWithHandler(result);
        GlobalOptions options = CreateOptions("table");

        // Act
        int exitCode;
        using (client) {
            exitCode = await SnapshotSetPolicyCommand.ExecuteAsync(client, options, "acme", "counter", "Counter", intervalEvents, CancellationToken.None);
        }

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task SnapshotSetPolicyCommand_403_ReturnsError() {
        // Arrange
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.Forbidden));
        GlobalOptions options = CreateOptions("table");
        using AdminApiClient client = new(options, handler);

        // Act
        int exitCode = await SnapshotSetPolicyCommand.ExecuteAsync(client, options, "acme", "counter", "Counter", 50, CancellationToken.None);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
