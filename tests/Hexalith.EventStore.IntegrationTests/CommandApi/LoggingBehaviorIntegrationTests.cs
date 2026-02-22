extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using CommandApiProgram = commandapi::Program;

public class LoggingBehaviorIntegrationTests : IClassFixture<LoggingBehaviorIntegrationTests.LogCapturingFactory> {
    private readonly LogCapturingFactory _factory;
    private readonly HttpClient _client;

    public LoggingBehaviorIntegrationTests(LogCapturingFactory factory) {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _client = CreateAuthenticatedClient(factory);
    }

    [Fact]
    public async Task PostCommands_ValidRequest_LogsStructuredEntryAndExit() {
        // Arrange
        _factory.LogProvider.Clear();
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        List<TestLogEntry> pipelineLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Message.Contains("MediatR pipeline"))
            .ToList();

        pipelineLogs.ShouldContain(e => e.Message.Contains("pipeline entry") && e.Level == LogLevel.Information);
        pipelineLogs.ShouldContain(e => e.Message.Contains("pipeline exit") && e.Level == LogLevel.Information);

        TestLogEntry entryLog = pipelineLogs.First(e => e.Message.Contains("pipeline entry"));
        entryLog.Message.ShouldContain("test-tenant");
        entryLog.Message.ShouldContain("test-domain");
        entryLog.Message.ShouldContain("CreateOrder");

        TestLogEntry exitLog = pipelineLogs.First(e => e.Message.Contains("pipeline exit"));
        exitLog.Message.ShouldContain("DurationMs=");
    }

    [Fact]
    public async Task PostCommands_InvalidRequest_HttpValidationPreventsLoggingBehavior() {
        // Arrange - empty tenant triggers ValidateModelFilter BEFORE MediatR pipeline
        _factory.LogProvider.Clear();
        var request = new {
            tenant = "",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - request is rejected at HTTP level, so MediatR pipeline is never reached
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        List<TestLogEntry> pipelineLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Message.Contains("MediatR pipeline"))
            .ToList();

        // LoggingBehavior should NOT have executed because ValidateModelFilter
        // catches validation errors before MediatR pipeline runs
        pipelineLogs.ShouldBeEmpty("HTTP-level validation should prevent MediatR pipeline execution");
    }

    [Fact]
    public async Task PostCommands_ValidRequest_PipelineOrderCorrect() {
        // Arrange - use valid request to verify LoggingBehavior executes before handler
        _factory.LogProvider.Clear();
        var request = new {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - LoggingBehavior entry should appear before handler log
        List<TestLogEntry> allLogs = _factory.LogProvider.GetEntries().ToList();

        int loggingEntryIndex = allLogs.FindIndex(e => e.Message.Contains("MediatR pipeline entry"));
        int handlerIndex = allLogs.FindIndex(e => e.Message.Contains("Command received:"));
        int loggingExitIndex = allLogs.FindIndex(e => e.Message.Contains("MediatR pipeline exit"));

        loggingEntryIndex.ShouldBeGreaterThanOrEqualTo(0, "LoggingBehavior entry log should exist");
        handlerIndex.ShouldBeGreaterThanOrEqualTo(0, "Handler log should exist");
        loggingExitIndex.ShouldBeGreaterThanOrEqualTo(0, "LoggingBehavior exit log should exist");

        // Pipeline order: LoggingBehavior entry -> Handler -> LoggingBehavior exit
        loggingEntryIndex.ShouldBeLessThan(handlerIndex, "LoggingBehavior entry should appear before handler");
        handlerIndex.ShouldBeLessThan(loggingExitIndex, "Handler should appear before LoggingBehavior exit");
    }

    /// <summary>
    /// Custom WebApplicationFactory that captures log entries for assertion.
    /// Configured with test JWT authentication.
    /// </summary>
    public class LogCapturingFactory : WebApplicationFactory<CommandApiProgram> {
        public TestLogProvider LogProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ConfigureAppConfiguration(config => {
                config.AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Authentication:JwtBearer:Issuer"] = TestJwtTokenGenerator.Issuer,
                    ["Authentication:JwtBearer:Audience"] = TestJwtTokenGenerator.Audience,
                    ["Authentication:JwtBearer:SigningKey"] = TestJwtTokenGenerator.SigningKey,
                    ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
                });
            });
            builder.ConfigureServices(services => {
                // Replace Dapr stores with InMemory for tests
                ServiceDescriptor? statusDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ICommandStatusStore));
                if (statusDescriptor is not null) {
                    services.Remove(statusDescriptor);
                }

                services.AddSingleton<ICommandStatusStore>(new InMemoryCommandStatusStore());

                ServiceDescriptor? archiveDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ICommandArchiveStore));
                if (archiveDescriptor is not null) {
                    services.Remove(archiveDescriptor);
                }

                services.AddSingleton<ICommandArchiveStore>(new InMemoryCommandArchiveStore());

                TestServiceOverrides.ReplaceCommandRouter(services);
                TestServiceOverrides.RemoveDaprHealthChecks(services);

                _ = services.AddLogging(logging => logging.AddProvider(LogProvider));
            });
        }
    }

    private static HttpClient CreateAuthenticatedClient(LogCapturingFactory factory) {
        HttpClient client = factory.CreateClient();
        string token = TestJwtTokenGenerator.GenerateToken(tenants: ["test-tenant"], domains: ["test-domain"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
