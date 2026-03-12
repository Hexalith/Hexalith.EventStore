
using System.Diagnostics;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Logging;
/// <summary>
/// Verifies that payload data is never written to log output (SEC-5, NFR12).
/// Tests both direct log messages and ToString() redaction.
/// </summary>
public class PayloadProtectionTests : IDisposable {
    private readonly List<LogEntry> _logEntries = [];
    private readonly ActivityListener _activityListener;

    // Payload containing distinctive marker bytes that should never appear in logs.
    private static readonly byte[] SensitivePayload = [0xDE, 0xAD, 0xBE, 0xEF];

    public PayloadProtectionTests() {
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
    public async Task SubmitCommandHandler_NeverLogsPayloadData() {
        // Arrange
        var logger = new TestLogger<SubmitCommandHandler>(_logEntries);
        ICommandStatusStore statusStore = Substitute.For<ICommandStatusStore>();
        ICommandArchiveStore archiveStore = Substitute.For<ICommandArchiveStore>();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        var handler = new SubmitCommandHandler(statusStore, archiveStore, router, logger);
        var command = new SubmitCommand(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: SensitivePayload,
            CorrelationId: "corr-123",
            UserId: "test-user",
            Extensions: null);

        // Act
        _ = await handler.Handle(command, CancellationToken.None);

        // Assert - no log entry should contain raw payload bytes
        foreach (LogEntry entry in _logEntries) {
            entry.Message.ShouldNotContain("DEAD");
            entry.Message.ShouldNotContain("BEEF");
            entry.Message.ShouldNotContain("3735928559"); // 0xDEADBEEF as decimal
        }
    }

    [Fact]
    public async Task EventPersister_NeverLogsPayloadData() {
        // Arrange
        var logger = new TestLogger<EventPersister>(_logEntries);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        var persister = new EventPersister(stateManager, logger, new NoOpEventPayloadProtectionService());
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var command = new CommandEnvelope(
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: SensitivePayload,
            CorrelationId: "corr-123",
            CausationId: "corr-123",
            UserId: "test-user",
            Extensions: null);
        var domainResult = new DomainResult([new TestEvent()]);

        // Act
        _ = await persister.PersistEventsAsync(identity, command, domainResult, "v1");

        // Assert
        foreach (LogEntry entry in _logEntries) {
            entry.Message.ShouldNotContain("DEAD");
            entry.Message.ShouldNotContain("BEEF");
        }
    }

    [Fact]
    public async Task LoggingBehavior_NeverLogsPayloadData() {
        // Arrange
        var logger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(_logEntries);
        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "corr-123";
        _ = httpContextAccessor.HttpContext.Returns(httpContext);
        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, httpContextAccessor);
        var command = new SubmitCommand(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: SensitivePayload,
            CorrelationId: "corr-123",
            UserId: "test-user",
            Extensions: null);
        static Task<SubmitCommandResult> next(CancellationToken _ = default) => Task.FromResult(new SubmitCommandResult("corr-123"));

        // Act
        _ = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        foreach (LogEntry entry in _logEntries) {
            entry.Message.ShouldNotContain("DEAD");
            entry.Message.ShouldNotContain("BEEF");
        }
    }

    [Fact]
    public void CommandEnvelope_ToString_RedactsPayload() {
        var envelope = new CommandEnvelope(
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: SensitivePayload,
            CorrelationId: "corr-123",
            CausationId: "corr-123",
            UserId: "test-user",
            Extensions: null);

        string output = envelope.ToString();

        output.ShouldContain("[REDACTED]");
        output.ShouldNotContain("DEAD");
        output.ShouldNotContain("BEEF");
    }

    [Fact]
    public void EventEnvelope_ToString_RedactsPayload() {
        var envelope = new EventEnvelope(
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
            Payload: SensitivePayload,
            Extensions: null);

        string output = envelope.ToString();

        output.ShouldContain("[REDACTED]");
        output.ShouldNotContain("DEAD");
        output.ShouldNotContain("BEEF");
    }

    private sealed class TestEvent : IEventPayload;

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private record LogEntry(LogLevel Level, string Message);
}
