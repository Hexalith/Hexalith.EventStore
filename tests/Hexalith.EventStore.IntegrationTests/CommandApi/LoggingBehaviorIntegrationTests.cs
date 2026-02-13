extern alias commandapi;

namespace Hexalith.EventStore.IntegrationTests.CommandApi;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using CommandApiProgram = commandapi::Program;

public class LoggingBehaviorIntegrationTests : IClassFixture<LoggingBehaviorIntegrationTests.LogCapturingFactory>
{
    private readonly LogCapturingFactory _factory;
    private readonly HttpClient _client;

    public LoggingBehaviorIntegrationTests(LogCapturingFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCommands_ValidRequest_LogsStructuredEntryAndExit()
    {
        // Arrange
        _factory.LogProvider.Clear();
        var request = new
        {
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

        List<CapturedLogEntry> pipelineLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Message.Contains("MediatR pipeline"))
            .ToList();

        pipelineLogs.ShouldContain(e => e.Message.Contains("pipeline entry") && e.Level == LogLevel.Information);
        pipelineLogs.ShouldContain(e => e.Message.Contains("pipeline exit") && e.Level == LogLevel.Information);

        CapturedLogEntry entryLog = pipelineLogs.First(e => e.Message.Contains("pipeline entry"));
        entryLog.Message.ShouldContain("test-tenant");
        entryLog.Message.ShouldContain("test-domain");
        entryLog.Message.ShouldContain("CreateOrder");

        CapturedLogEntry exitLog = pipelineLogs.First(e => e.Message.Contains("pipeline exit"));
        exitLog.Message.ShouldContain("DurationMs=");
    }

    [Fact]
    public async Task PostCommands_InvalidRequest_HttpValidationPreventsLoggingBehavior()
    {
        // Arrange - empty tenant triggers ValidateModelFilter BEFORE MediatR pipeline
        _factory.LogProvider.Clear();
        var request = new
        {
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

        List<CapturedLogEntry> pipelineLogs = _factory.LogProvider.GetEntries()
            .Where(e => e.Message.Contains("MediatR pipeline"))
            .ToList();

        // LoggingBehavior should NOT have executed because ValidateModelFilter
        // catches validation errors before MediatR pipeline runs
        pipelineLogs.ShouldBeEmpty("HTTP-level validation should prevent MediatR pipeline execution");
    }

    [Fact]
    public async Task PostCommands_ValidRequest_PipelineOrderCorrect()
    {
        // Arrange - use valid request to verify LoggingBehavior executes before handler
        _factory.LogProvider.Clear();
        var request = new
        {
            tenant = "test-tenant",
            domain = "test-domain",
            aggregateId = "agg-001",
            commandType = "CreateOrder",
            payload = new { amount = 100 },
        };

        // Act
        await _client.PostAsJsonAsync("/api/v1/commands", request);

        // Assert - LoggingBehavior entry should appear before handler log
        List<CapturedLogEntry> allLogs = _factory.LogProvider.GetEntries().ToList();

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
    /// </summary>
    public class LogCapturingFactory : WebApplicationFactory<CommandApiProgram>
    {
        public CapturedLogProvider LogProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.ConfigureServices(services =>
            {
                _ = services.AddLogging(logging => logging.AddProvider(LogProvider));
            });
        }
    }
}

public sealed class CapturedLogProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = [];

    public ILogger CreateLogger(string categoryName) => new CapturedLogger(_entries);

    public void Dispose()
    {
        // Nothing to dispose
    }

    public List<CapturedLogEntry> GetEntries() => [.. _entries];

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
            // Drain the queue
        }
    }

    private sealed class CapturedLogger(ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new CapturedLogEntry(logLevel, formatter(state, exception)));
        }
    }
}

public record CapturedLogEntry(LogLevel Level, string Message);
