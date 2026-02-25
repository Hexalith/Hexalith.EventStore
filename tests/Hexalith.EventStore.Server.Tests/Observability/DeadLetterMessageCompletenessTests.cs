
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Observability;
/// <summary>
/// Story 6.3 Task 5: Dead-letter message completeness verification tests.
/// Verifies that dead-letter messages contain all required correlation fields,
/// the full command envelope for replay, and proper failure context.
/// </summary>
public class DeadLetterMessageCompletenessTests {
    #region Shared Helpers

    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001",
        string commandType = "CreateOrder",
        string? correlationId = null,
        string? causationId = null) => new(
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: commandType,
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, IDomainServiceInvoker Invoker, FakeDeadLetterPublisher DeadLetterPublisher)
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

        return (actor, stateManager, invoker, fakeDeadLetter);
    }

    #endregion

    #region Task 5.2: DeadLetterMessage_ContainsAllRequiredCorrelationFields

    [Fact]
    public async Task DeadLetterMessage_ContainsAllRequiredCorrelationFields() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        string causationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter) =
            CreateActorWithFakeDeadLetter("completeness-tenant:completeness-domain:completeness-agg");

        CommandEnvelope envelope = CreateTestEnvelope(
            tenantId: "completeness-tenant",
            domain: "completeness-domain",
            aggregateId: "completeness-agg",
            commandType: "ProcessPayment",
            correlationId: correlationId,
            causationId: causationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Domain failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> dlMessages = fakeDeadLetter.GetDeadLetterMessages();
        dlMessages.Count.ShouldBe(1);
        DeadLetterMessage dl = dlMessages[0].Message;

        dl.CorrelationId.ShouldBe(correlationId);
        dl.CausationId.ShouldBe(causationId);
        dl.TenantId.ShouldBe("completeness-tenant");
        dl.Domain.ShouldBe("completeness-domain");
        dl.AggregateId.ShouldBe("completeness-agg");
        dl.CommandType.ShouldBe("ProcessPayment");
    }

    #endregion

    #region Task 5.3: DeadLetterMessage_ContainsFullCommandEnvelope

    [Fact]
    public async Task DeadLetterMessage_ContainsFullCommandEnvelope() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Command envelope in dead-letter is the original, unmodified envelope
        DeadLetterMessage dl = fakeDeadLetter.GetDeadLetterMessages()[0].Message;
        _ = dl.Command.ShouldNotBeNull("Dead-letter should contain full command envelope for replay");
        dl.Command.TenantId.ShouldBe(envelope.TenantId);
        dl.Command.Domain.ShouldBe(envelope.Domain);
        dl.Command.AggregateId.ShouldBe(envelope.AggregateId);
        dl.Command.CommandType.ShouldBe(envelope.CommandType);
        dl.Command.CorrelationId.ShouldBe(envelope.CorrelationId);
        dl.Command.Payload.ShouldBe(envelope.Payload);
    }

    #endregion

    #region Task 5.4: DeadLetterMessage_ContainsFailureContext

    [Fact]
    public async Task DeadLetterMessage_ContainsFailureContext() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Specific domain failure reason"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert
        DeadLetterMessage dl = fakeDeadLetter.GetDeadLetterMessages()[0].Message;

        dl.FailureStage.ShouldNotBeNullOrEmpty("FailureStage should be populated");
        dl.ExceptionType.ShouldBe("InvalidOperationException");
        dl.ErrorMessage.ShouldBe("Specific domain failure reason");
        dl.FailedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1), "FailedAt should be recent");
        dl.FailedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    #endregion

    #region Task 5.5: DeadLetterMessage_CorrelationIdMatchesOriginalCommand

    [Fact]
    public async Task DeadLetterMessage_CorrelationIdMatchesOriginalCommand() {
        // Arrange
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Dead-letter correlationId equals the submitted command's correlationId
        DeadLetterMessage dl = fakeDeadLetter.GetDeadLetterMessages()[0].Message;
        dl.CorrelationId.ShouldBe(envelope.CorrelationId);
        dl.Command.CorrelationId.ShouldBe(envelope.CorrelationId);
    }

    #endregion

    #region Task 5.6: DeadLetterMessage_NeverContainsStackTrace

    [Fact]
    public async Task DeadLetterMessage_NeverContainsStackTrace() {
        // Arrange: Use a real exception that has a stack trace
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        // Create exception with stack trace by throwing and catching
        Exception realException;
        try {
            throw new InvalidOperationException("Test failure with stack trace");
        }
        catch (Exception ex) {
            realException = ex;
        }

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(realException);

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Rule #13 -- no stack traces in dead-letter messages
        DeadLetterMessage dl = fakeDeadLetter.GetDeadLetterMessages()[0].Message;

        // ExceptionType should be just the type name, not a full stack trace
        dl.ExceptionType.ShouldBe("InvalidOperationException");
        dl.ExceptionType.Contains("at ").ShouldBeFalse("ExceptionType should not contain stack trace");
        dl.ExceptionType.Contains(Environment.NewLine).ShouldBeFalse("ExceptionType should not contain newlines");

        // ErrorMessage should be just the message, not the full ToString with stack trace
        dl.ErrorMessage.ShouldBe("Test failure with stack trace");
        dl.ErrorMessage.Contains("at ").ShouldBeFalse("ErrorMessage should not contain stack trace");
        dl.ErrorMessage.Contains("StackTrace").ShouldBeFalse("ErrorMessage should not reference stack trace");
    }

    #endregion

    #region Task 5.7: DeadLetterMessage_NullCausationId_HandledGracefully

    [Fact]
    public async Task DeadLetterMessage_NullCausationId_HandledGracefully() {
        // Arrange: CausationId is null (original submission, not a replay)
        string correlationId = Guid.NewGuid().ToString();
        (AggregateActor actor, _, IDomainServiceInvoker invoker, FakeDeadLetterPublisher fakeDeadLetter) =
            CreateActorWithFakeDeadLetter();

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId, causationId: null);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Dead-letter message is complete and valid even with null CausationId
        IReadOnlyList<(AggregateIdentity Identity, DeadLetterMessage Message)> dlMessages = fakeDeadLetter.GetDeadLetterMessages();
        dlMessages.Count.ShouldBe(1);
        DeadLetterMessage dl = dlMessages[0].Message;

        dl.CorrelationId.ShouldBe(correlationId);
        dl.CausationId.ShouldBeNull("Original submission should have null CausationId");
        _ = dl.Command.ShouldNotBeNull("Command envelope should still be present");
        dl.FailureStage.ShouldNotBeNullOrEmpty();
        dl.ExceptionType.ShouldNotBeNullOrEmpty();
        dl.ErrorMessage.ShouldNotBeNullOrEmpty();
        dl.TenantId.ShouldNotBeNullOrEmpty();
        dl.Domain.ShouldNotBeNullOrEmpty();
        dl.AggregateId.ShouldNotBeNullOrEmpty();
        dl.CommandType.ShouldNotBeNullOrEmpty();
    }

    #endregion
}
