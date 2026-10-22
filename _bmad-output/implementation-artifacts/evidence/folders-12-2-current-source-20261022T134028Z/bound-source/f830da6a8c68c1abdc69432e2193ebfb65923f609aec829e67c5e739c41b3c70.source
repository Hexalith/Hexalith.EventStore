using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands;

[Collection("ConsoleTests")]
public class HealthDaprCommandTests {
    private static readonly DateTimeOffset _testTime = new(2026, 3, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HealthDaprCommand_ReturnsComponentTable() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
            new("pubsub-redis", "pubsub.redis", HealthStatus.Healthy, _testTime),
            new("binding-cron", "bindings.cron", HealthStatus.Degraded, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("table");

        TextWriter originalOut = Console.Out;
        try {
            using StringWriter sw = new();
            Console.SetOut(sw);

            // Act
            int exitCode = await HealthDaprCommand.ExecuteAsync(
                client, options, component: null, CancellationToken.None)
                ;

            // Assert
            string output = sw.ToString();
            output.ShouldContain("state-redis");
            output.ShouldContain("pubsub-redis");
            output.ShouldContain("binding-cron");
            output.ShouldContain("Component Name");
        }
        finally {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HealthDaprCommand_JsonFormat_ReturnsValidJson() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
            new("pubsub-redis", "pubsub.redis", HealthStatus.Healthy, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalOut = Console.Out;
        try {
            using StringWriter sw = new();
            Console.SetOut(sw);

            // Act
            _ = await HealthDaprCommand.ExecuteAsync(
                client, options, component: null, CancellationToken.None)
                ;

            // Assert
            string json = sw.ToString().Trim();
            List<DaprComponentHealth>? deserialized = JsonSerializer.Deserialize<List<DaprComponentHealth>>(json, JsonDefaults.Options);
            _ = deserialized.ShouldNotBeNull();
            deserialized.Count.ShouldBe(2);
        }
        finally {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HealthDaprCommand_EmptyResult_PrintsError() {
        // Arrange
        List<DaprComponentHealth> components = [];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("table");

        TextWriter originalErr = Console.Error;
        try {
            using StringWriter errWriter = new();
            Console.SetError(errWriter);

            // Act
            int exitCode = await HealthDaprCommand.ExecuteAsync(
                client, options, component: null, CancellationToken.None)
                ;

            // Assert
            exitCode.ShouldBe(ExitCodes.Error);
            errWriter.ToString().ShouldContain("No DAPR components found.");
        }
        finally {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task HealthDaprCommand_ExitCode_AllHealthy_Returns0() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
            new("pubsub-redis", "pubsub.redis", HealthStatus.Healthy, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthDaprCommand.ExecuteAsync(
            client, options, component: null, CancellationToken.None)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task HealthDaprCommand_ExitCode_AnyDegraded_Returns1() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
            new("pubsub-redis", "pubsub.redis", HealthStatus.Degraded, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthDaprCommand.ExecuteAsync(
            client, options, component: null, CancellationToken.None)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Degraded);
    }

    [Fact]
    public async Task HealthDaprCommand_ExitCode_AnyUnhealthy_Returns2() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
            new("pubsub-redis", "pubsub.redis", HealthStatus.Unhealthy, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthDaprCommand.ExecuteAsync(
            client, options, component: null, CancellationToken.None)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }

    [Fact]
    public async Task HealthDaprCommand_ComponentFilter_Found_ReturnsComponent() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
            new("pubsub-redis", "pubsub.redis", HealthStatus.Degraded, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthDaprCommand.ExecuteAsync(
            client, options, component: "state-redis", CancellationToken.None)
            ;

        // Assert — exit code matches the found component's status
        exitCode.ShouldBe(ExitCodes.Success);
    }

    [Fact]
    public async Task HealthDaprCommand_ComponentFilter_NotFound_ReturnsError() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalErr = Console.Error;
        try {
            using StringWriter errWriter = new();
            Console.SetError(errWriter);

            // Act
            int exitCode = await HealthDaprCommand.ExecuteAsync(
                client, options, component: "nonexistent", CancellationToken.None)
                ;

            // Assert
            exitCode.ShouldBe(ExitCodes.Error);
            errWriter.ToString().ShouldContain("DAPR component 'nonexistent' not found.");
        }
        finally {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task HealthDaprCommand_ComponentFilter_CaseInsensitive() {
        // Arrange
        List<DaprComponentHealth> components =
        [
            new("state-redis", "state.redis", HealthStatus.Healthy, _testTime),
        ];
        using AdminApiClient client = CreateMockClient(components);
        GlobalOptions options = CreateOptions("json");

        // Act — uppercase filter should match lowercase component
        int exitCode = await HealthDaprCommand.ExecuteAsync(
            client, options, component: "STATE-REDIS", CancellationToken.None)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
    }

    private static GlobalOptions CreateOptions(string format = "json")
        => new("http://localhost:5002", null, format, null);

    private static AdminApiClient CreateMockClient(List<DaprComponentHealth> components) {
        string json = JsonSerializer.Serialize(components, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        return new AdminApiClient(httpClient);
    }
}
