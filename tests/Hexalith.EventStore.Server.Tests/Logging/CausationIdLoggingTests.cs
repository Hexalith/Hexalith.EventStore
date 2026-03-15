
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
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Logging;
/// <summary>
/// Verifies that CausationId is present in all log messages that carry CorrelationId (AC #4).
/// CausationId = MessageId for original submissions (set by ToCommandEnvelope); different for replays.
/// </summary>
public class CausationIdLoggingTests : IDisposable {
    private readonly List<LogEntry> _logEntries = [];
    private readonly ActivityListener _activityListener;

    public CausationIdLoggingTests() {
        _activityListener = new ActivityListener {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose() {
        _activityListener.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SubmitCommandHandler_IncludesCausationId() {
        // Arrange
        var logger = new TestLogger<SubmitCommandHandler>(_logEntries);
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Server.Actors.CommandProcessingResult(true, CorrelationId: "corr-123"));
        var handler = new SubmitCommandHandler(statusStore, archiveStore, router, logger);
        SubmitCommand command = CreateSubmitCommand();

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert - the "Command received" log must include CausationId (= MessageId per SubmitCommandHandler)
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Command received"));
        entry.Message.ShouldContain("CausationId=msg-causation-submit-1");
    }

    [Fact]
    public async Task LoggingBehavior_IncludesCausationId() {
        // Arrange
        var logger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);
        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-123";
        _ = httpContextAccessor.HttpContext.Returns(httpContext);
        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, httpContextAccessor);
        SubmitCommand command = CreateSubmitCommand();
        static Task<SubmitCommandResult> next(CancellationToken _ = default) => Task.FromResult(new SubmitCommandResult("corr-123"));

        // Act
        _ = await behavior.Handle(command, next, CancellationToken.None);

        // Assert - both entry and exit logs must include CausationId
        LogEntry entryLog = _logEntries.First(e => e.Message.Contains("pipeline entry"));
        entryLog.Message.ShouldContain("CausationId=");

        LogEntry exitLog = _logEntries.First(e => e.Message.Contains("pipeline exit"));
        exitLog.Message.ShouldContain("CausationId=");
    }

    [Fact]
    public async Task EventPersister_IncludesCausationId() {
        // Arrange
        var logger = new TestLogger<EventPersister>(_logEntries);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        var persister = new EventPersister(stateManager, logger, new NoOpEventPayloadProtectionService());
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        // Use a distinct CausationId to verify it propagates (not just CorrelationId)
        var command = new CommandEnvelope(
            MessageId: "msg-causation-persist-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [0x01],
            CorrelationId: "corr-123",
            CausationId: "cause-456",
            UserId: "test-user",
            Extensions: null);
        var domainResult = new DomainResult([new TestEvent()]);

        // Act
        _ = await persister.PersistEventsAsync(identity, command, domainResult, "v1");

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Events persisted"));
        entry.Message.ShouldContain("CausationId=cause-456");
        entry.Message.ShouldContain("CorrelationId=corr-123");
    }

    [Fact]
    public async Task EventPublisher_IncludesCausationId() {
        // Arrange
        var logger = new TestLogger<EventPublisher>(_logEntries);
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new Server.Configuration.EventPublisherOptions());
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService());
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var events = new List<EventEnvelope>
        {
            new(
                MessageId: "msg-1",
                AggregateId: "agg-001",
                AggregateType: "test-aggregate",
                TenantId: "test-tenant",
                Domain: "test-domain",
                SequenceNumber: 1,
                GlobalPosition: 0,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: "corr-123",
                CausationId: "cause-789",
                UserId: "test-user",
                DomainServiceVersion: "v1",
                EventTypeName: "OrderCreated",
                MetadataVersion: 1,
                SerializationFormat: "json",
                Payload: [0x01],
                Extensions: null)
        };

        // Act
        _ = await publisher.PublishEventsAsync(identity, events, "corr-123");

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Events published"));
        entry.Message.ShouldContain("CausationId=cause-789");
    }

    [Fact]
    public async Task DeadLetterPublisher_IncludesCausationId() {
        // Arrange
        var logger = new TestLogger<DeadLetterPublisher>(_logEntries);
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new Server.Configuration.EventPublisherOptions());
        var publisher = new DeadLetterPublisher(daprClient, options, logger);
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var command = new CommandEnvelope(
            MessageId: "msg-causation-dlq-1",
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [0x01],
            CorrelationId: "corr-123",
            CausationId: "cause-dlq",
            UserId: "test-user",
            Extensions: null);
        var message = new DeadLetterMessage(
            command, "Processing", "InvalidOperationException", "test error",
            "corr-123", "cause-dlq", "test-tenant", "test-domain", "agg-001", "CreateOrder",
            DateTimeOffset.UtcNow, null);

        // Act
        _ = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        LogEntry entry = _logEntries.First(e => e.Message.Contains("Dead-letter published"));
        entry.Message.ShouldContain("CausationId=cause-dlq");
    }

    private static SubmitCommand CreateSubmitCommand() =>
        new(
            MessageId: "msg-causation-submit-1",
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [0x01],
            CorrelationId: "corr-123",
            UserId: "test-user",
            Extensions: null);

    private sealed class TestEvent : IEventPayload;

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private record LogEntry(LogLevel Level, string Message);
}
