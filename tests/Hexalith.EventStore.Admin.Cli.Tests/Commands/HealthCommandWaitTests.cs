using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Client;
using Hexalith.EventStore.Admin.Cli.Commands;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Cli.Tests.Commands;

[Collection("ConsoleTests")]
public class HealthCommandWaitTests {
    [Fact]
    public async Task HealthCommand_Quiet_SuppressesStdout() {
        // Arrange
        SystemHealthReport report = CreateTestReport(HealthStatus.Healthy);
        using AdminApiClient client = CreateMockClient(report);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalOut = Console.Out;
        try {
            using StringWriter sw = new();
            Console.SetOut(sw);

            // Act
            int exitCode = await HealthCommand.ExecuteAsync(
                client, options, strict: false, wait: false, timeout: 30, quiet: true, CancellationToken.None)
                ;

            // Assert
            exitCode.ShouldBe(ExitCodes.Success);
            sw.ToString().ShouldBeEmpty();
        }
        finally {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HealthCommand_Quiet_StillReturnsCorrectExitCode() {
        // Arrange — unhealthy
        SystemHealthReport report = CreateTestReport(HealthStatus.Unhealthy);
        using AdminApiClient client = CreateMockClient(report);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalOut = Console.Out;
        try {
            using StringWriter sw = new();
            Console.SetOut(sw);

            // Act
            int exitCode = await HealthCommand.ExecuteAsync(
                client, options, strict: false, wait: false, timeout: 30, quiet: true, CancellationToken.None)
                ;

            // Assert
            exitCode.ShouldBe(ExitCodes.Error);
            sw.ToString().ShouldBeEmpty();
        }
        finally {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HealthCommand_Wait_PollsUntilHealthy() {
        // Arrange — Unhealthy, Unhealthy, then Healthy
        HttpResponseMessage unhealthyResponse1 = CreateMockResponse(HealthStatus.Unhealthy);
        HttpResponseMessage unhealthyResponse2 = CreateMockResponse(HealthStatus.Unhealthy);
        HttpResponseMessage healthyResponse = CreateMockResponse(HealthStatus.Healthy);

        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(unhealthyResponse1)
            .EnqueueResponse(unhealthyResponse2)
            .EnqueueResponse(healthyResponse);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalErr = Console.Error;
        try {
            using StringWriter errWriter = new();
            Console.SetError(errWriter);

            // Act
            int exitCode = await HealthCommand.ExecuteAsync(
                client, options, strict: false, wait: true, timeout: 30, quiet: false, CancellationToken.None, pollIntervalMs: 1)
                ;

            // Assert
            exitCode.ShouldBe(ExitCodes.Success);
            string errOutput = errWriter.ToString();
            errOutput.ShouldContain("Service is healthy.");
        }
        finally {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task HealthCommand_Wait_Timeout_ReturnsError() {
        // Arrange — always unhealthy (repeat-last replays indefinitely), short timeout
        // Use factory to create fresh responses since GetAsync disposes each response
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueFactory(() => CreateMockResponse(HealthStatus.Unhealthy));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalErr = Console.Error;
        try {
            using StringWriter errWriter = new();
            Console.SetError(errWriter);

            // Act — very short timeout of 1 second
            int exitCode = await HealthCommand.ExecuteAsync(
                client, options, strict: false, wait: true, timeout: 1, quiet: false, CancellationToken.None, pollIntervalMs: 1)
                ;

            // Assert
            exitCode.ShouldBe(ExitCodes.Error);
            string errOutput = errWriter.ToString();
            errOutput.ShouldContain("Timed out waiting for healthy status after 1 seconds.");
        }
        finally {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task HealthCommand_Wait_ConnectionError_Retries() {
        // Arrange — connection error then healthy
        HttpResponseMessage healthyResponse = CreateMockResponse(HealthStatus.Healthy);

        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueException(new HttpRequestException("Connection refused"))
            .EnqueueResponse(healthyResponse);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalErr = Console.Error;
        try {
            using StringWriter errWriter = new();
            Console.SetError(errWriter);

            // Act
            int exitCode = await HealthCommand.ExecuteAsync(
                client, options, strict: false, wait: true, timeout: 30, quiet: false, CancellationToken.None, pollIntervalMs: 1)
                ;

            // Assert
            exitCode.ShouldBe(ExitCodes.Success);
            errWriter.ToString().ShouldContain("Service is healthy.");
        }
        finally {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task HealthCommand_Wait_Strict_OnlyAcceptsHealthy() {
        // Arrange — Degraded, Degraded, then Healthy
        HttpResponseMessage degradedResponse1 = CreateMockResponse(HealthStatus.Degraded);
        HttpResponseMessage degradedResponse2 = CreateMockResponse(HealthStatus.Degraded);
        HttpResponseMessage healthyResponse = CreateMockResponse(HealthStatus.Healthy);

        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(degradedResponse1)
            .EnqueueResponse(degradedResponse2)
            .EnqueueResponse(healthyResponse);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);
        GlobalOptions options = CreateOptions("json");

        // Act
        int exitCode = await HealthCommand.ExecuteAsync(
            client, options, strict: true, wait: true, timeout: 30, quiet: false, CancellationToken.None, pollIntervalMs: 1)
            ;

        // Assert
        exitCode.ShouldBe(ExitCodes.Success);
        handler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task HealthCommand_Wait_NoStrict_AcceptsDegradedAndStopsPolling() {
        // Arrange — Degraded response should be accepted without --strict
        HttpResponseMessage degradedResponse = CreateMockResponse(HealthStatus.Degraded);

        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueResponse(degradedResponse);
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);
        GlobalOptions options = CreateOptions("json");

        TextWriter originalErr = Console.Error;
        try {
            using StringWriter errWriter = new();
            Console.SetError(errWriter);

            // Act — without --strict, Degraded should be acceptable (poll stops immediately)
            int exitCode = await HealthCommand.ExecuteAsync(
                client, options, strict: false, wait: true, timeout: 30, quiet: false, CancellationToken.None, pollIntervalMs: 1)
                ;

            // Assert — should stop on first poll (Degraded is acceptable), return Degraded exit code
            exitCode.ShouldBe(ExitCodes.Degraded);
            handler.CallCount.ShouldBe(1);
            errWriter.ToString().ShouldContain("Service is healthy.");
        }
        finally {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public async Task HealthCommand_Wait_CancellationToken_StopsPolling() {
        // Arrange — always unhealthy (repeat-last replays indefinitely), cancel soon
        // Use factory to create fresh responses since GetAsync disposes each response
        QueuedMockHttpMessageHandler handler = new QueuedMockHttpMessageHandler()
            .EnqueueFactory(() => CreateMockResponse(HealthStatus.Unhealthy));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        using AdminApiClient client = new(httpClient);
        GlobalOptions options = CreateOptions("json");

        using CancellationTokenSource cts = new();

        TextWriter originalErr = Console.Error;
        try {
            using StringWriter errWriter = new();
            Console.SetError(errWriter);

            // Cancel after first poll attempt
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            // Act & Assert — should throw when cancelled.
            // May be OperationCanceledException (from Task.Delay) or AdminApiException (from HTTP call)
            // depending on timing. Either proves cancellation stops the loop.
            Exception ex = await Should.ThrowAsync<Exception>(
                () => HealthCommand.ExecuteAsync(
                    client, options, strict: false, wait: true, timeout: 60, quiet: false, cts.Token, pollIntervalMs: 1));
            (ex is OperationCanceledException or AdminApiException).ShouldBeTrue(
                $"Expected OperationCanceledException or AdminApiException, got {ex.GetType().Name}");
        }
        finally {
            Console.SetError(originalErr);
        }
    }

    private static SystemHealthReport CreateTestReport(HealthStatus overallStatus) => new(
            overallStatus,
            1000,
            42.5,
            0.12,
            [
                new DaprComponentHealth("state-redis", "state.redis", HealthStatus.Healthy, DateTimeOffset.UtcNow),
            ],
            new ObservabilityLinks(null, null, null));

    private static HttpResponseMessage CreateMockResponse(HealthStatus overallStatus) {
        SystemHealthReport report = CreateTestReport(overallStatus);
        string json = JsonSerializer.Serialize(report, JsonDefaults.Options);
        return new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static GlobalOptions CreateOptions(string format = "json")
        => new("http://localhost:5002", null, format, null);

    private static AdminApiClient CreateMockClient(SystemHealthReport report) {
        string json = JsonSerializer.Serialize(report, JsonDefaults.Options);
        MockHttpMessageHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5002") };
        return new AdminApiClient(httpClient);
    }
}
