
using System.Reflection;
using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.CommandApi.Controllers;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.Core;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Observability;
/// <summary>
/// Story 6.3: Dead-letter-to-origin tracing tests.
/// Verifies that a dead-letter message's correlation ID traces back through all pipeline stages
/// via structured logs, and validates multi-tenant isolation, causation chains, and replay correlation.
/// </summary>
public class DeadLetterOriginTracingTests {
    #region Shared Helpers

    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001",
        string? correlationId = null,
        string? causationId = null) => new(
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker, FakeDeadLetterPublisher DeadLetterPublisher, ICommandStatusStore StatusStore)
        CreateActorWithFakeDeadLetter(string actorId = "test-tenant:test-domain:agg-001") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var fakeDeadLetter = new FakeDeadLetterPublisher();

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });

        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager, commandStatusStore,
            eventPublisher, Options.Create(new EventDrainOptions()),
            fakeDeadLetter);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: no duplicates, no pipeline state, no metadata
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Default: domain service returns NoOp
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        // Default: snapshot not found
        _ = snapshotManager.LoadSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IActorStateManager>(), Arg.Any<string>())
            .Returns((SnapshotRecord?)null);

        // Default: event rehydration returns empty
        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 0, null));

        return (actor, stateManager, logger, invoker, fakeDeadLetter, commandStatusStore);
    }

    /// <summary>
    /// Extracts all log messages from an NSubstitute ILogger mock by inspecting received calls.
    /// </summary>
    private static IReadOnlyList<(LogLevel Level, string Message)> GetLogEntries(ILogger logger) {
        var entries = new List<(LogLevel Level, string Message)>();
        foreach (ICall call in logger.ReceivedCalls()) {
            if (call.GetMethodInfo().Name == "Log" && call.GetArguments().Length >= 5) {
                object?[] args = call.GetArguments();
                var level = (LogLevel)args[0]!;
                object? state = args[2];
                var exception = args[3] as Exception;
                object? formatter = args[4];
                string? message = null;
                if (formatter is not null && state is not null) {
                    try {
                        MethodInfo? invokeMethod = formatter.GetType().GetMethod("Invoke");
                        message = invokeMethod?.Invoke(formatter, [state, exception])?.ToString();
                    }
                    catch {
                        message = state.ToString();
                    }
                }

                message ??= state?.ToString() ?? string.Empty;
                entries.Add((level, message));
            }
        }

        return entries;
    }

    #endregion

    #region Task 3: Log-based dead-letter-to-origin tracing tests (AC #1, #2, #5, #10)

    [Fact]
    public async Task DomainServiceFailure_CorrelationIdTracesBackThroughAllStages() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulated domain service infra failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Dead-letter was published with correct correlation ID
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> dlMessages = fakeDeadLetter.GetDeadLetterMessages();
        dlMessages.Count.ShouldBe(1);
        dlMessages[0].Message.CorrelationId.ShouldBe(correlationId);

        // Assert: Verify logs contain the correlation ID at multiple stages
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);
        IReadOnlyList<(LogLevel Level, string Message)> correlatedLogs = logEntries
            .Where(e => e.Message.Contains(correlationId, StringComparison.Ordinal))
            .ToList();

        // Should have logs for: ActorActivated, Processing stage transition, InfrastructureFailure, Rejected stage transition
        correlatedLogs.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task DomainServiceFailure_OriginatingRequestIdentifiable() {
        // Arrange: build a realistic API->pipeline->actor flow sharing one correlation ID
        string correlationId = Guid.NewGuid().ToString();
        var behaviorLogs = new List<ObservabilityLogEntry>();
        var behaviorLogger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(behaviorLogs);
        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = correlationId;
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        httpContext.Request.Path = "/api/v1/commands";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "operator-user")], "TestAuth"));
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(behaviorLogger, httpContextAccessor);

        (AggregateActor actor, _, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, _, _) =
            CreateActorWithFakeDeadLetter();

        var submitCommand = new SubmitCommand(
            Tenant: "origin-tenant",
            Domain: "origin-domain",
            AggregateId: "origin-agg",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: correlationId,
            UserId: "operator-user",
            Extensions: null);

        CommandEnvelope envelope = CreateTestEnvelope(
            tenantId: submitCommand.Tenant,
            domain: submitCommand.Domain,
            aggregateId: submitCommand.AggregateId,
            correlationId: submitCommand.CorrelationId,
            causationId: submitCommand.CorrelationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulated failure"));

        // Act
        _ = await behavior.Handle(
            submitCommand,
            new RequestHandlerDelegate<SubmitCommandResult>(async (ct) => {
                _ = await actor.ProcessCommandAsync(envelope).ConfigureAwait(false);
                return new SubmitCommandResult(correlationId);
            }),
            CancellationToken.None);

        // Assert: API origin log includes source IP, timestamp, user identity, command type, and endpoint
        behaviorLogs.Any(e => e.Level == LogLevel.Information
                        && e.Message.Contains("PipelineEntry", StringComparison.Ordinal)
                        && e.Message.Contains("CorrelationId=" + correlationId, StringComparison.Ordinal)
                        && e.Message.Contains("SourceIp=192.168.1.100", StringComparison.Ordinal)
                        && e.Message.Contains("Endpoint=/api/v1/commands", StringComparison.Ordinal)
                        && e.Message.Contains("UserId=operator-user", StringComparison.Ordinal)
                        && e.Message.Contains("ReceivedAtUtc=", StringComparison.Ordinal)
                        && e.Message.Contains("CommandType=CreateOrder", StringComparison.Ordinal))
            .ShouldBeTrue("Origin tracing requires source IP, endpoint, user identity, timestamp, and command type in the API receipt log");

        // Assert: actor-side log trail still carries same correlation ID
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);
        IReadOnlyList<(LogLevel Level, string Message)> correlatedLogs = logEntries
            .Where(e => e.Message.Contains(correlationId, StringComparison.Ordinal))
            .ToList();

        // At least one log should contain the tenant ID
        correlatedLogs.Any(e => e.Message.Contains("origin-tenant", StringComparison.Ordinal))
            .ShouldBeTrue("Log trail should include tenant ID for origin identification");

        // At least one log should contain the command type
        correlatedLogs.Any(e => e.Message.Contains("CreateOrder", StringComparison.Ordinal))
            .ShouldBeTrue("Log trail should include command type for origin identification");
    }

    [Fact]
    public async Task StateRehydrationFailure_CorrelationIdTracesBackThroughAllStages() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, _, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        // Configure existing aggregate metadata so rehydration is attempted
        var metadata = new AggregateMetadata(1, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        // Event stream read throws
        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store unavailable"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Dead-letter published
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> dlMessages = fakeDeadLetter.GetDeadLetterMessages();
        dlMessages.Count.ShouldBe(1);
        dlMessages[0].Message.CorrelationId.ShouldBe(correlationId);

        // Assert: Log trail has correlation ID
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);
        int correlatedCount = logEntries.Count(e => e.Message.Contains(correlationId, StringComparison.Ordinal));
        correlatedCount.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task EventPersistenceFailure_CorrelationIdTracesBackThroughAllStages() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, IActorStateManager stateManager, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));

        // Configure SaveStateAsync to succeed first time (Processing checkpoint), fail second time (EventsStored)
        int saveCallCount = 0;
        _ = stateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ => {
                saveCallCount++;
                if (saveCallCount == 2) {
                    throw new IOException("State store write failed");
                }

                return Task.CompletedTask;
            });

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Dead-letter published
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> dlMessages = fakeDeadLetter.GetDeadLetterMessages();
        dlMessages.Count.ShouldBe(1);
        dlMessages[0].Message.CorrelationId.ShouldBe(correlationId);

        // Assert: Log trail has correlation ID
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);
        int correlatedCount = logEntries.Count(e => e.Message.Contains(correlationId, StringComparison.Ordinal));
        correlatedCount.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task DeadLetterLog_ContainsCorrelationIdMatchingOrigin() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Dead-letter published with matching correlation ID
        DeadLetterMessage dl = fakeDeadLetter.GetDeadLetterMessages()[0].Message;
        dl.CorrelationId.ShouldBe(correlationId);

        // Assert: InfrastructureFailure log contains correlation ID and Stage
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);
        logEntries.Any(e => e.Level == LogLevel.Error
                 && e.Message.Contains(correlationId, StringComparison.Ordinal)
                 && e.Message.Contains("InfrastructureFailure", StringComparison.Ordinal))
            .ShouldBeTrue("InfrastructureFailure Error log should contain correlation ID and Stage");
    }

    [Fact]
    public async Task AllLogsBetweenOriginAndDeadLetter_ContainConsistentCorrelationId() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, _, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: ALL logs that contain the CorrelationId field contain the CORRECT correlation ID
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);

        // Filter to logs that reference a CorrelationId field
        IReadOnlyList<(LogLevel Level, string Message)> logsWithCorrelation = logEntries
            .Where(e => e.Message.Contains("CorrelationId=", StringComparison.Ordinal))
            .ToList();

        logsWithCorrelation.ShouldNotBeEmpty();

        // Every log with CorrelationId= should have the correct value
        foreach ((_, string message) in logsWithCorrelation) {
            message.Contains(correlationId, StringComparison.Ordinal).ShouldBeTrue(
                $"Log message contains CorrelationId field but with wrong value: {message}");
        }
    }

    [Fact]
    public async Task InformationLevelOnly_TracingChainStillComplete() {
        // Arrange: Simulate production scenario where Debug logs are filtered
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, _, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Even with only Information+ logs, the tracing chain is complete
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);

        // Filter to Information level and above (production scenario)
        IReadOnlyList<(LogLevel Level, string Message)> infoAndAboveLogs = logEntries
            .Where(e => e.Level >= LogLevel.Information && e.Message.Contains(correlationId, StringComparison.Ordinal))
            .ToList();

        // Should have at minimum: Processing stage transition (Info), InfrastructureFailure (Error), Rejected stage transition (Warning)
        infoAndAboveLogs.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Should contain the Error-level InfrastructureFailure log
        infoAndAboveLogs.Any(e => e.Level == LogLevel.Error)
            .ShouldBeTrue("InfrastructureFailure Error log should be present at Information+ level");
    }

    [Fact]
    public async Task CommandReceived_LogIncludesSourceIP() {
        // Arrange: Test that LoggingBehavior's PipelineEntry log includes SourceIP
        var logEntries = new List<ObservabilityLogEntry>();
        var testLogger = new TestLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(logEntries);
        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.HttpContextKey] = "test-correlation";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        _ = httpContextAccessor.HttpContext.Returns(httpContext);

        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(testLogger, httpContextAccessor);

        var command = new SubmitCommand(
            Tenant: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: "test-correlation",
            UserId: "test-user",
            Extensions: null);

        // Act
        _ = await behavior.Handle(
            command,
            new RequestHandlerDelegate<SubmitCommandResult>((_) => Task.FromResult(new SubmitCommandResult("test-correlation"))),
            CancellationToken.None);

        // Assert: PipelineEntry log includes SourceIp
        logEntries.Any(e => e.Level == LogLevel.Information
                 && e.Message.Contains("SourceIp=192.168.1.100", StringComparison.Ordinal)
                 && e.Message.Contains("PipelineEntry", StringComparison.Ordinal))
            .ShouldBeTrue("PipelineEntry log should include SourceIp for origin identification");
    }

    #endregion

    #region Task 6: Multi-tenant isolation tracing tests (AC #7, #10)

    [Fact]
    public async Task MultiTenant_EachDeadLetterTracesBackToCorrectTenantOrigin() {
        // Arrange: Two different tenants with different commands
        string correlationA = Guid.NewGuid().ToString();
        string correlationB = Guid.NewGuid().ToString();

        // Tenant A
        (AggregateActor actorA, _, ILogger<AggregateActor> loggerA, IDomainServiceInvoker invokerA, FakeDeadLetterPublisher fakeDeadLetterA, _) =
            CreateActorWithFakeDeadLetter("tenant-a:orders:agg-001");

        CommandEnvelope envelopeA = CreateTestEnvelope(
            tenantId: "tenant-a", domain: "orders", aggregateId: "agg-001",
            correlationId: correlationA);

        _ = invokerA.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenant A failure"));

        // Tenant B
        (AggregateActor actorB, _, ILogger<AggregateActor> loggerB, IDomainServiceInvoker invokerB, FakeDeadLetterPublisher fakeDeadLetterB, _) =
            CreateActorWithFakeDeadLetter("tenant-b:inventory:agg-002");

        CommandEnvelope envelopeB = CreateTestEnvelope(
            tenantId: "tenant-b", domain: "inventory", aggregateId: "agg-002",
            correlationId: correlationB);

        _ = invokerB.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Tenant B failure"));

        // Act
        _ = await actorA.ProcessCommandAsync(envelopeA);
        _ = await actorB.ProcessCommandAsync(envelopeB);

        // Assert: Each dead-letter traces back to correct tenant
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> dlA = fakeDeadLetterA.GetDeadLetterMessages();
        dlA.Count.ShouldBe(1);
        dlA[0].Message.CorrelationId.ShouldBe(correlationA);
        dlA[0].Message.TenantId.ShouldBe("tenant-a");

        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> dlB = fakeDeadLetterB.GetDeadLetterMessages();
        dlB.Count.ShouldBe(1);
        dlB[0].Message.CorrelationId.ShouldBe(correlationB);
        dlB[0].Message.TenantId.ShouldBe("tenant-b");

        // Assert: Tenant A logs don't contain tenant B's correlation ID
        IReadOnlyList<(LogLevel Level, string Message)> logsA = GetLogEntries(loggerA);
        logsA.Any(e => e.Message.Contains(correlationB, StringComparison.Ordinal))
            .ShouldBeFalse("Tenant A logs should not contain Tenant B's correlation ID");

        // Assert: Tenant B logs don't contain tenant A's correlation ID
        IReadOnlyList<(LogLevel Level, string Message)> logsB = GetLogEntries(loggerB);
        logsB.Any(e => e.Message.Contains(correlationA, StringComparison.Ordinal))
            .ShouldBeFalse("Tenant B logs should not contain Tenant A's correlation ID");
    }

    [Fact]
    public async Task MultiTenant_NoCorrelationIdCrossTalk() {
        // Arrange
        string correlationA = Guid.NewGuid().ToString();
        string correlationB = Guid.NewGuid().ToString();

        (AggregateActor actorA, _, _, IDomainServiceInvoker invokerA, FakeDeadLetterPublisher fakeDeadLetterA, _) =
            CreateActorWithFakeDeadLetter("tenant-a:domain-x:agg-a");

        CommandEnvelope envelopeA = CreateTestEnvelope(
            tenantId: "tenant-a", domain: "domain-x", aggregateId: "agg-a",
            correlationId: correlationA);

        _ = invokerA.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Fail A"));

        (AggregateActor actorB, _, _, IDomainServiceInvoker invokerB, FakeDeadLetterPublisher fakeDeadLetterB, _) =
            CreateActorWithFakeDeadLetter("tenant-b:domain-y:agg-b");

        CommandEnvelope envelopeB = CreateTestEnvelope(
            tenantId: "tenant-b", domain: "domain-y", aggregateId: "agg-b",
            correlationId: correlationB);

        _ = invokerB.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Fail B"));

        // Act
        _ = await actorA.ProcessCommandAsync(envelopeA);
        _ = await actorB.ProcessCommandAsync(envelopeB);

        // Assert: Dead-letter messages have correct, non-overlapping correlation IDs
        _ = fakeDeadLetterA.GetDeadLetterMessageByCorrelationId(correlationA).ShouldNotBeNull();
        fakeDeadLetterA.GetDeadLetterMessageByCorrelationId(correlationA)!.Value.Message.TenantId.ShouldBe("tenant-a");

        _ = fakeDeadLetterB.GetDeadLetterMessageByCorrelationId(correlationB).ShouldNotBeNull();
        fakeDeadLetterB.GetDeadLetterMessageByCorrelationId(correlationB)!.Value.Message.TenantId.ShouldBe("tenant-b");

        // Cross-check: No cross-talk
        fakeDeadLetterB.GetDeadLetterMessageByCorrelationId(correlationA).ShouldBeNull();
        fakeDeadLetterA.GetDeadLetterMessageByCorrelationId(correlationB).ShouldBeNull();
    }

    #endregion

    #region Task 7: Causation chain verification tests (AC #9, #10)

    [Fact]
    public async Task OriginalSubmission_CausationIdMatchesCorrelationId() {
        // Arrange: Original submission has CausationId = CorrelationId
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        DeadLetterMessage dl = fakeDeadLetter.GetDeadLetterMessages()[0].Message;
        dl.CorrelationId.ShouldBe(correlationId);
        dl.CausationId.ShouldBe(correlationId, "Original submission should have matching CorrelationId/CausationId");
    }

    [Fact]
    public async Task ReplayedCommand_CausationIdDiffersFromCorrelationId() {
        // Arrange: Replayed command has a different CausationId
        string correlationId = Guid.NewGuid().ToString();
        string causationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: causationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Replay also failed"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        DeadLetterMessage dl = fakeDeadLetter.GetDeadLetterMessages()[0].Message;
        dl.CorrelationId.ShouldBe(correlationId);
        dl.CausationId.ShouldBe(causationId);
        dl.CausationId.ShouldNotBe(dl.CorrelationId);
    }

    [Fact]
    public async Task ReplayedCommand_CorrelationIdMatchesOriginalSubmission() {
        // Arrange
        string sharedCorrelationId = Guid.NewGuid().ToString();
        string replayCausationId = Guid.NewGuid().ToString();

        // First submission (original)
        (AggregateActor actor1, _, _, IDomainServiceInvoker invoker1, FakeDeadLetterPublisher fakeDeadLetter1, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope originalEnvelope = CreateTestEnvelope(correlationId: sharedCorrelationId, causationId: null);

        _ = invoker1.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Original failure"));

        _ = await actor1.ProcessCommandAsync(originalEnvelope);

        // Second submission (replay) with same correlation, different causation
        (AggregateActor actor2, _, _, IDomainServiceInvoker invoker2, FakeDeadLetterPublisher fakeDeadLetter2, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope replayEnvelope = CreateTestEnvelope(correlationId: sharedCorrelationId, causationId: replayCausationId);

        _ = invoker2.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Replay failure"));

        _ = await actor2.ProcessCommandAsync(replayEnvelope);

        // Assert: Both dead-letters share the same CorrelationId
        DeadLetterMessage dl1 = fakeDeadLetter1.GetDeadLetterMessages()[0].Message;
        DeadLetterMessage dl2 = fakeDeadLetter2.GetDeadLetterMessages()[0].Message;

        dl1.CorrelationId.ShouldBe(sharedCorrelationId);
        dl2.CorrelationId.ShouldBe(sharedCorrelationId);

        // But causation chains differ
        dl1.CausationId.ShouldBeNull();
        dl2.CausationId.ShouldBe(replayCausationId);
    }

    #endregion

    #region Task 8: Replay-via-dead-letter correlation tests (AC #8, #10)

    [Fact]
    public async Task DeadLetterCorrelationId_CanLocateOriginalCommandStatus() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, _, ICommandStatusStore statusStore) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Infra failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Command status store received a write for this correlation ID with Rejected status
        await statusStore.Received().WriteStatusAsync(
            Arg.Any<string>(),
            correlationId,
            Arg.Is<CommandStatusRecord>(r => r.Status == CommandStatus.Rejected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadLetterCorrelationId_StatusReflectsTerminalState() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        CommandStatusRecord? capturedStatus = null;
        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, _, ICommandStatusStore statusStore) =
            CreateActorWithFakeDeadLetter();

        // Capture the final status write
        _ = statusStore.WriteStatusAsync(
            Arg.Any<string>(),
            correlationId,
            Arg.Do<CommandStatusRecord>(r => capturedStatus = r),
            Arg.Any<CancellationToken>());

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Infra failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: The last status write is for Rejected terminal state
        _ = capturedStatus.ShouldNotBeNull("Status store should have received a status write");
        capturedStatus.Status.ShouldBe(CommandStatus.Rejected);
    }

    [Fact]
    public async Task DeadLetterCorrelationId_ReplayEndpointWithinTtl_ReplaysCommand() {
        // Arrange: create a dead-letter to obtain a real correlation ID
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Infra failure"));

        _ = await actor.ProcessCommandAsync(envelope);
        string deadLetterCorrelationId = fakeDeadLetter.GetDeadLetterMessages()[0].Message.CorrelationId;

        // Seed replay endpoint stores with within-TTL data for same correlation ID
        var archiveStore = new InMemoryCommandArchiveStore();
        var statusStore = new InMemoryCommandStatusStore();
        await archiveStore.WriteCommandAsync(
            envelope.TenantId,
            deadLetterCorrelationId,
            new ArchivedCommand(envelope.TenantId, envelope.Domain, envelope.AggregateId, envelope.CommandType, envelope.Payload, envelope.Extensions, DateTimeOffset.UtcNow),
            CancellationToken.None);
        await statusStore.WriteStatusAsync(
            envelope.TenantId,
            deadLetterCorrelationId,
            new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow, envelope.AggregateId, null, null, null, null),
            CancellationToken.None);

        IMediator mediator = Substitute.For<IMediator>();
        _ = mediator.Send(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SubmitCommandResult(callInfo.Arg<SubmitCommand>().CorrelationId));

        var replayController = new ReplayController(archiveStore, statusStore, mediator, new TestLogger<ReplayController>(new List<ObservabilityLogEntry>())) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", "operator-user"),
                        new Claim("eventstore:tenant", envelope.TenantId),
                    ], "TestAuth")),
                },
            },
        };
        replayController.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey] = deadLetterCorrelationId;

        // Act
        IActionResult result = await replayController.Replay(deadLetterCorrelationId, CancellationToken.None);

        // Assert — replay generates a new correlation ID for tracking
        _ = result.ShouldBeOfType<AcceptedResult>();
        _ = await mediator.Received(1).Send(
            Arg.Is<SubmitCommand>(c => c.CorrelationId != deadLetterCorrelationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadLetterCorrelationId_ReplayEndpointExpiredStatus_ReturnsConflict() {
        // Arrange: create a dead-letter to obtain a real correlation ID
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter, _) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Infra failure"));

        _ = await actor.ProcessCommandAsync(envelope);
        string deadLetterCorrelationId = fakeDeadLetter.GetDeadLetterMessages()[0].Message.CorrelationId;

        // Seed archive only; status intentionally missing to model expired TTL
        var archiveStore = new InMemoryCommandArchiveStore();
        var statusStore = new InMemoryCommandStatusStore();
        await archiveStore.WriteCommandAsync(
            envelope.TenantId,
            deadLetterCorrelationId,
            new ArchivedCommand(envelope.TenantId, envelope.Domain, envelope.AggregateId, envelope.CommandType, envelope.Payload, envelope.Extensions, DateTimeOffset.UtcNow),
            CancellationToken.None);

        IMediator mediator = Substitute.For<IMediator>();
        var replayController = new ReplayController(archiveStore, statusStore, mediator, new TestLogger<ReplayController>(new List<ObservabilityLogEntry>())) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", "operator-user"),
                        new Claim("eventstore:tenant", envelope.TenantId),
                    ], "TestAuth")),
                },
            },
        };
        replayController.HttpContext.Items[CorrelationIdMiddleware.HttpContextKey] = deadLetterCorrelationId;

        // Act
        IActionResult result = await replayController.Replay(deadLetterCorrelationId, CancellationToken.None);

        // Assert
        ObjectResult conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        ProblemDetails pd = conflict.Value.ShouldBeOfType<ProblemDetails>();
        _ = pd.Detail.ShouldNotBeNull();
        pd.Detail.ShouldContain("expired");
    }

    #endregion

    #region Test Helpers

    private sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;

    private sealed class TestLogger<T>(List<ObservabilityLogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => entries.Add(new ObservabilityLogEntry(logLevel, formatter(state, exception)));
    }

    #endregion
}

internal record ObservabilityLogEntry(LogLevel Level, string Message);
