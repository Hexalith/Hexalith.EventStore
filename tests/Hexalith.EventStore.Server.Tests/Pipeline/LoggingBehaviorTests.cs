namespace Hexalith.EventStore.Server.Tests.Pipeline;

using System.Diagnostics;

using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

public class LoggingBehaviorTests : IDisposable
{
    private readonly List<LogEntry> _logEntries = [];
    private readonly TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LoggingBehavior<SubmitCommand, SubmitCommandResult> _behavior;
    private readonly ActivityListener _activityListener;
    private readonly List<Activity> _capturedActivities = [];

    public LoggingBehaviorTests()
    {
        _logger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = "test-correlation-id";
        _httpContextAccessor.HttpContext.Returns(httpContext);

        _behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(_logger, _httpContextAccessor);

        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore.CommandApi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _capturedActivities.Add(activity),
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        GC.SuppressFinalize(this);
    }

    private static SubmitCommand CreateTestCommand(
        string tenant = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001",
        string commandType = "CreateOrder",
        string correlationId = "test-correlation-id") =>
        new(
            Tenant: tenant,
            Domain: domain,
            AggregateId: aggregateId,
            CommandType: commandType,
            Payload: [0x01, 0x02, 0x03],
            CorrelationId: correlationId,
            Extensions: new Dictionary<string, string> { ["key1"] = "value1", ["secret"] = "sensitive-data" });

    private static RequestHandlerDelegate<SubmitCommandResult> CreateSuccessDelegate() =>
        new((_) => Task.FromResult(new SubmitCommandResult("test-correlation-id")));

    private static RequestHandlerDelegate<SubmitCommandResult> CreateFailingDelegate() =>
        new((_) => throw new InvalidOperationException("Handler failed"));

    [Fact]
    public async Task LoggingBehavior_ValidRequest_LogsEntryAndExit()
    {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act
        await _behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _logEntries.Count.ShouldBeGreaterThanOrEqualTo(2);

        LogEntry entryLog = _logEntries.First(e => e.Message.Contains("pipeline entry"));
        entryLog.Level.ShouldBe(LogLevel.Information);
        entryLog.Message.ShouldContain("test-correlation-id");
        entryLog.Message.ShouldContain("CreateOrder");
        entryLog.Message.ShouldContain("test-tenant");
        entryLog.Message.ShouldContain("test-domain");

        LogEntry exitLog = _logEntries.First(e => e.Message.Contains("pipeline exit"));
        exitLog.Level.ShouldBe(LogLevel.Information);
        exitLog.Message.ShouldContain("test-correlation-id");
        exitLog.Message.ShouldContain("CreateOrder");
    }

    [Fact]
    public async Task LoggingBehavior_ValidRequest_IncludesDurationMs()
    {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act
        await _behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        LogEntry exitLog = _logEntries.First(e => e.Message.Contains("pipeline exit"));
        exitLog.Message.ShouldContain("DurationMs=");
    }

    [Fact]
    public async Task LoggingBehavior_HandlerThrows_LogsErrorAndRethrows()
    {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _behavior.Handle(command, CreateFailingDelegate(), CancellationToken.None));

        LogEntry errorLog = _logEntries.First(e => e.Level == LogLevel.Error);
        errorLog.Message.ShouldContain("test-correlation-id");
        errorLog.Message.ShouldContain("InvalidOperationException");
        errorLog.Message.ShouldContain("Handler failed");
    }

    [Fact]
    public async Task LoggingBehavior_NeverLogsPayload()
    {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act
        await _behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert - Payload bytes should never appear in any log entry
        foreach (LogEntry entry in _logEntries)
        {
            entry.Message.ShouldNotContain("Payload");
            entry.Message.ShouldNotContain("System.Byte[]");
        }
    }

    [Fact]
    public async Task LoggingBehavior_NeverLogsExtensions()
    {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act
        await _behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert - Extensions values should never appear in any log entry
        foreach (LogEntry entry in _logEntries)
        {
            entry.Message.ShouldNotContain("Extensions");
            entry.Message.ShouldNotContain("sensitive-data");
            entry.Message.ShouldNotContain("value1");
        }
    }

    [Fact]
    public async Task LoggingBehavior_CreatesOpenTelemetryActivity()
    {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act
        await _behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert
        _capturedActivities.Count.ShouldBeGreaterThanOrEqualTo(1);
        Activity activity = _capturedActivities.First();
        activity.OperationName.ShouldBe("EventStore.CommandApi.Submit");

        activity.GetTagItem("eventstore.correlation_id").ShouldBe("test-correlation-id");
        activity.GetTagItem("eventstore.tenant").ShouldBe("test-tenant");
        activity.GetTagItem("eventstore.domain").ShouldBe("test-domain");
        activity.GetTagItem("eventstore.command_type").ShouldBe("CreateOrder");
    }

    [Fact]
    public async Task LoggingBehavior_ExceptionSetsActivityStatusError()
    {
        // Arrange
        SubmitCommand command = CreateTestCommand();

        // Act
        await Should.ThrowAsync<InvalidOperationException>(
            () => _behavior.Handle(command, CreateFailingDelegate(), CancellationToken.None));

        // Assert
        _capturedActivities.Count.ShouldBeGreaterThanOrEqualTo(1);
        Activity activity = _capturedActivities.First();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task LoggingBehavior_NoHttpContext_GeneratesFallbackCorrelationId()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        SubmitCommand command = CreateTestCommand();

        // Act
        await _behavior.Handle(command, CreateSuccessDelegate(), CancellationToken.None);

        // Assert - should still log entry/exit without crashing
        _logEntries.Count.ShouldBeGreaterThanOrEqualTo(2);
        LogEntry entryLog = _logEntries.First(e => e.Message.Contains("pipeline entry"));
        entryLog.Message.ShouldContain("CorrelationId=");
    }

    /// <summary>
    /// Simple test logger that captures log entries for assertion.
    /// </summary>
    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }
}

internal record LogEntry(LogLevel Level, string Message);
