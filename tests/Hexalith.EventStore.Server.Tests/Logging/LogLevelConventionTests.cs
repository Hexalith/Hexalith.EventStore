namespace Hexalith.EventStore.Server.Tests.Logging;

using System.Diagnostics;

using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

/// <summary>
/// Verifies that log levels follow architecture convention:
/// Information = normal flow, Debug = internal mechanics,
/// Warning = retries/rejections, Error = infrastructure failures (AC #4).
/// </summary>
public class LogLevelConventionTests : IDisposable
{
    private readonly List<LogEntry> _logEntries = [];
    private readonly ActivityListener _activityListener;

    public LogLevelConventionTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task PipelineEntry_UsesInformationLevel()
    {
        // Arrange
        var logger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-123";
        httpContextAccessor.HttpContext.Returns(httpContext);
        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, httpContextAccessor);
        SubmitCommand command = CreateSubmitCommand();
        RequestHandlerDelegate<SubmitCommandResult> next = (_) => Task.FromResult(new SubmitCommandResult("corr-123"));

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("pipeline entry"));
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task PipelineExit_UsesInformationLevel()
    {
        // Arrange
        var logger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-123";
        httpContextAccessor.HttpContext.Returns(httpContext);
        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, httpContextAccessor);
        SubmitCommand command = CreateSubmitCommand();
        RequestHandlerDelegate<SubmitCommandResult> next = (_) => Task.FromResult(new SubmitCommandResult("corr-123"));

        // Act
        await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("pipeline exit"));
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task PipelineError_UsesErrorLevel()
    {
        // Arrange
        var logger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-123";
        httpContextAccessor.HttpContext.Returns(httpContext);
        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, httpContextAccessor);
        SubmitCommand command = CreateSubmitCommand();
        RequestHandlerDelegate<SubmitCommandResult> next = (_) => throw new InvalidOperationException("test error");

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            behavior.Handle(command, next, CancellationToken.None));

        LogEntry entry = _logEntries.First(e => e.Message.Contains("pipeline error"));
        entry.Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task CommandReceived_UsesInformationLevel()
    {
        // Arrange
        var logger = new TestLogger<SubmitCommandHandler>(_logEntries);
        var statusStore = Substitute.For<ICommandStatusStore>();
        var archiveStore = Substitute.For<ICommandArchiveStore>();
        var router = Substitute.For<ICommandRouter>();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, router, logger);
        SubmitCommand command = CreateSubmitCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Command received"));
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task EventsPersisted_UsesInformationLevel()
    {
        // Arrange
        var logger = new TestLogger<EventPersister>(_logEntries);
        var stateManager = Substitute.For<IActorStateManager>();
        stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        var persister = new EventPersister(stateManager, logger);
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        CommandEnvelope command = CreateCommandEnvelope();
        var domainResult = new DomainResult([new TestEvent()]);

        // Act
        await persister.PersistEventsAsync(identity, command, domainResult, "v1");

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Events persisted"));
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task EventsPublished_UsesInformationLevel()
    {
        // Arrange
        var logger = new TestLogger<EventPublisher>(_logEntries);
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new Server.Configuration.EventPublisherOptions());
        var publisher = new EventPublisher(daprClient, options, logger);
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var events = new List<EventEnvelope> { CreateEventEnvelope() };

        // Act
        await publisher.PublishEventsAsync(identity, events, "corr-123");

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Events published"));
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task EventPublicationFailed_UsesErrorLevel()
    {
        // Arrange
        var logger = new TestLogger<EventPublisher>(_logEntries);
        var daprClient = Substitute.For<DaprClient>();
        daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("pub/sub failure"));
        var options = Options.Create(new Server.Configuration.EventPublisherOptions());
        var publisher = new EventPublisher(daprClient, options, logger);
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var events = new List<EventEnvelope> { CreateEventEnvelope() };

        // Act
        await publisher.PublishEventsAsync(identity, events, "corr-123");

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("publication failed"));
        entry.Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task DeadLetterPublished_UsesWarningLevel()
    {
        // Arrange
        var logger = new TestLogger<DeadLetterPublisher>(_logEntries);
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new Server.Configuration.EventPublisherOptions());
        var publisher = new DeadLetterPublisher(daprClient, options, logger);
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var message = new DeadLetterMessage(
            CreateCommandEnvelope(), "Processing", "InvalidOperationException", "test error",
            "corr-123", "corr-123", "test-tenant", "test-domain", "agg-001", "CreateOrder",
            DateTimeOffset.UtcNow, null);

        // Act
        await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Dead-letter published"));
        entry.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public void TenantMismatch_UsesWarningLevel()
    {
        // Arrange
        var logger = new TestLogger<Server.Actors.TenantValidator>(_logEntries);
        var validator = new Server.Actors.TenantValidator(logger);

        // Act & Assert
        Should.Throw<Server.Actors.TenantMismatchException>(
            () => validator.Validate("wrong-tenant", "test-tenant:test-domain:agg-001"));

        LogEntry entry = _logEntries.First(e => e.Message.Contains("Tenant mismatch"));
        entry.Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public void TenantValidationPassed_UsesDebugLevel()
    {
        // Arrange
        var logger = new TestLogger<Server.Actors.TenantValidator>(_logEntries);
        var validator = new Server.Actors.TenantValidator(logger);

        // Act
        validator.Validate("test-tenant", "test-tenant:test-domain:agg-001");

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Tenant validation passed"));
        entry.Level.ShouldBe(LogLevel.Debug);
    }

    private static SubmitCommand CreateSubmitCommand() =>
        new(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [0x01],
            CorrelationId: "corr-123",
            UserId: "test-user",
            Extensions: null);

    private static CommandEnvelope CreateCommandEnvelope() =>
        new(
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [0x01],
            CorrelationId: "corr-123",
            CausationId: "corr-123",
            UserId: "test-user",
            Extensions: null);

    private static EventEnvelope CreateEventEnvelope() =>
        new(
            AggregateId: "agg-001",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-123",
            CausationId: "corr-123",
            UserId: "test-user",
            DomainServiceVersion: "v1",
            EventTypeName: "OrderCreated",
            SerializationFormat: "json",
            Payload: [0x01],
            Extensions: null);

    private sealed class TestEvent : IEventPayload;

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private record LogEntry(LogLevel Level, string Message);
}
