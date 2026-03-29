using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands;

[Collection("ConsoleTests")]
public class HealthCommandStrictTests
{
    [Fact]
    public async Task HealthCommand_Strict_DegradedReturnsExitCode2()
    {
        // Arrange
        SystemHealthReport report = CreateTestReport(HealthStatus.Degraded);
        using AdminApiClient client = CreateMockClient(report);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthCommand.ExecuteAsync(
            client, options, strict: true, wait: false, timeout: 30, quiet: false, CancellationToken.None)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task HealthCommand_Strict_HealthyReturnsExitCode0()
    {
        // Arrange
        SystemHealthReport report = CreateTestReport(HealthStatus.Healthy);
        using AdminApiClient client = CreateMockClient(report);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthCommand.ExecuteAsync(
            client, options, strict: true, wait: false, timeout: 30, quiet: false, CancellationToken.None)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task HealthCommand_NoStrict_DegradedReturnsExitCode1()
    {
        // Arrange
        SystemHealthReport report = CreateTestReport(HealthStatus.Degraded);
        using AdminApiClient client = CreateMockClient(report);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthCommand.ExecuteAsync(
            client, options, strict: false, wait: false, timeout: 30, quiet: false, CancellationToken.None)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Degraded);
    }

    private static SystemHealthReport CreateTestReport(HealthStatus overallStatus)
    {
        return new SystemHealthReport(
            overallStatus,
            1000,
            42.5,
            0.12,
            [
                new DaprComponentHealth("state-redis", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow),
                new DaprComponentHealth("pubsub-redis", "pubsub.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow),
            ],
            new ObservabilityLinks(null, null, null));
    }

    private static GlobalOptions CreateOptions(string format = "json")
        => new("http://localhost:5002", null, format, null);

    private static AdminApiClient CreateMockClient(SystemHealthReport report)
    {
        string json = JsonSerializer.Serialize(report, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        return new AdminApiClient(httpClient);
    }
}
