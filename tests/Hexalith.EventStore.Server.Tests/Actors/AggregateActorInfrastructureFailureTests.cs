using System.Diagnostics;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorInfrastructureFailureTests
{
    private const string AttemptTriggerKey = "projection-trigger:failed-attempt";
    private const string PendingCountKey = "pending_command_count";

    [Fact]
    public async Task DomainFailureClearsConcreteAttemptStateBeforeRejectionCleanupAndSavesOnlyPermittedState()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-domain-failure");
        string pipelineKey = GetPipelineKey(command);
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        _ = context.DeadLetterPublisher.PublishDeadLetterAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<DeadLetterMessage>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stateManager.Trace.Add("DeadLetter");
                return true;
            });

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldNotContain("primary-secret");
        AssertSuccessfulRejectionEndState(stateManager, command);
        AssertClearBeforeRejectionOperations(stateManager, command, pipelineKey);
    }

    [Fact]
    public async Task RehydrationFailureClearsConcreteAttemptStateAndLeavesTheStreamUnchanged()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-rehydrate-failure");
        _ = context.SnapshotManager.LoadSnapshotAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<Dapr.Actors.Runtime.IActorStateManager>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<SnapshotRecord?>(
                stateManager,
                command,
                new IOException("rehydration-secret")));

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldNotContain("rehydration-secret");
        AssertSuccessfulRejectionEndState(stateManager, command);
    }

    [Fact]
    public async Task SnapshotFailureAfterEventStagingPreservesTheOriginalCommittedStream()
    {
        var stateManager = new FaultInjectingActorStateManager();
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-snapshot-failure");
        AggregateIdentity identity = command.AggregateIdentity;
        EventEnvelope winnerEvent = CreateEvent(identity, 1, "winner-message");
        var winnerMetadata = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "winner-etag");
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [identity.MetadataKey] = winnerMetadata,
            [$"{identity.EventStreamKeyPrefix}1"] = winnerEvent,
        });
        ActorTestContext context = CreateActor(stateManager: stateManager);
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        _ = context.SnapshotManager.ShouldCreateSnapshotAsync(
                command.TenantId,
                command.Domain,
                Arg.Any<string>(),
                2,
                0,
                Arg.Any<CancellationToken>())
            .Returns(true);
        _ = context.SnapshotManager.CreateSnapshotAsync(
                identity,
                1,
                Arg.Any<object>(),
                stateManager,
                command.CorrelationId,
                Arg.Any<CancellationToken>(),
                false)
            .Returns(_ => StageSnapshotAndThrowAsync(
                stateManager,
                identity,
                new IOException("snapshot-secret")));

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        durable[identity.MetadataKey].ShouldBe(winnerMetadata);
        durable[$"{identity.EventStreamKeyPrefix}1"].ShouldBe(winnerEvent);
        durable.ShouldNotContainKey($"{identity.EventStreamKeyPrefix}2");
        durable.ShouldNotContainKey(identity.SnapshotKey);
        AssertNoTerminalResidue(durable, command);
    }

    [Fact]
    public async Task PersistenceConflictRetryClearsBeforeRehydratingTheConcurrentWinner()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            concurrencyOptions: new CommandConcurrencyOptions { MaxPersistenceConflictRetries = 1 },
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-conflict-retry");
        AggregateIdentity identity = command.AggregateIdentity;
        EventEnvelope winnerEvent = CreateEvent(identity, 1, "winner-message");
        var winnerMetadata = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "winner-etag");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        stateManager.FaultOnCall(
            "SaveState",
            2,
            new InvalidOperationException("conflict-secret"),
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.MetadataKey] = winnerMetadata,
                [$"{identity.EventStreamKeyPrefix}1"] = winnerEvent,
            }));

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeTrue();
        _ = await context.Invoker.Received(2).InvokeAsync(
            command,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        durable[$"{identity.EventStreamKeyPrefix}1"].ShouldBe(winnerEvent);
        durable.ShouldContainKey($"{identity.EventStreamKeyPrefix}2");
        ((AggregateMetadata)durable[identity.MetadataKey]).CurrentSequence.ShouldBe(2);
        AssertNoTerminalResidue(durable, command);

        int winnerIndex = stateManager.Trace.IndexOf("ConcurrentWinner");
        int clearIndex = stateManager.Trace.FindIndex(winnerIndex + 1, operation => operation == "ClearCache");
        int rehydrateIndex = stateManager.Trace.FindIndex(
            clearIndex + 1,
            operation => operation == $"TryGetState:{identity.MetadataKey}");
        clearIndex.ShouldBeGreaterThan(winnerIndex);
        rehydrateIndex.ShouldBeGreaterThan(clearIndex);
    }

    [Fact]
    public async Task MultiplePersistenceConflictsClearEachFailedAttemptBeforeUsingTheLatestWinner()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            concurrencyOptions: new CommandConcurrencyOptions { MaxPersistenceConflictRetries = 2 },
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-multiple-conflicts");
        AggregateIdentity identity = command.AggregateIdentity;
        EventEnvelope firstWinner = CreateEvent(identity, 1, "winner-message-1");
        EventEnvelope secondWinner = CreateEvent(identity, 2, "winner-message-2");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        stateManager.FaultOnCall(
            "SaveState",
            2,
            new InvalidOperationException("first-conflict-secret"),
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.MetadataKey] = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "winner-etag-1"),
                [$"{identity.EventStreamKeyPrefix}1"] = firstWinner,
            }));
        stateManager.FaultOnCall(
            "SaveState",
            3,
            new InvalidOperationException("second-conflict-secret"),
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.MetadataKey] = new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "winner-etag-2"),
                [$"{identity.EventStreamKeyPrefix}2"] = secondWinner,
            }));

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeTrue();
        _ = await context.Invoker.Received(3).InvokeAsync(
            command,
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        durable[$"{identity.EventStreamKeyPrefix}1"].ShouldBe(firstWinner);
        durable[$"{identity.EventStreamKeyPrefix}2"].ShouldBe(secondWinner);
        durable.ShouldContainKey($"{identity.EventStreamKeyPrefix}3");
        ((AggregateMetadata)durable[identity.MetadataKey]).CurrentSequence.ShouldBe(3);

        int firstWinnerIndex = stateManager.Trace.IndexOf("ConcurrentWinner");
        int firstClearIndex = stateManager.Trace.FindIndex(firstWinnerIndex + 1, operation => operation == "ClearCache");
        int secondWinnerIndex = stateManager.Trace.FindIndex(firstClearIndex + 1, operation => operation == "ConcurrentWinner");
        int secondClearIndex = stateManager.Trace.FindIndex(secondWinnerIndex + 1, operation => operation == "ClearCache");
        firstClearIndex.ShouldBeGreaterThan(firstWinnerIndex);
        secondWinnerIndex.ShouldBeGreaterThan(firstClearIndex);
        secondClearIndex.ShouldBeGreaterThan(secondWinnerIndex);
    }

    [Fact]
    public async Task PersistenceConflictExhaustionPreservesOnlyTheConcurrentWinner()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            concurrencyOptions: new CommandConcurrencyOptions { MaxPersistenceConflictRetries = 0 },
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-conflict-exhausted");
        AggregateIdentity identity = command.AggregateIdentity;
        EventEnvelope winnerEvent = CreateEvent(identity, 1, "winner-message");
        var winnerMetadata = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "winner-etag");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        stateManager.FaultOnCall(
            "SaveState",
            2,
            new InvalidOperationException("conflict-secret"),
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.MetadataKey] = winnerMetadata,
                [$"{identity.EventStreamKeyPrefix}1"] = winnerEvent,
            }));

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        result.FailureReason.ShouldBe("ConcurrencyConflict");
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        durable[identity.MetadataKey].ShouldBe(winnerMetadata);
        durable[$"{identity.EventStreamKeyPrefix}1"].ShouldBe(winnerEvent);
        durable.ShouldNotContainKey($"{identity.EventStreamKeyPrefix}2");
        durable.ShouldNotContainKey($"idempotency:{command.MessageId}");
        AssertNoTerminalResidue(durable, command);
    }

    [Fact]
    public async Task ConflictRetryCallerCancellationPropagatesAfterNonCancelableDiscard()
    {
        using var cancellation = new CancellationTokenSource();
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            concurrencyOptions: new CommandConcurrencyOptions { MaxPersistenceConflictRetries = 1 },
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-conflict-cancel");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        stateManager.FaultOnCall("SaveState", 2, new InvalidOperationException("conflict-secret"));
        stateManager.FaultOnCall(
            "ClearCache",
            1,
            new OperationCanceledException("clear-secret", cancellation.Token),
            _ =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        await Should.ThrowAsync<OperationCanceledException>(
            () => context.Actor.ProcessCommandAsync(command, cancellation.Token));

        AssertAttemptStateAbsent(stateManager.CreateCommittedView(), command);
        stateManager.CreateCommittedView()[PendingCountKey].ShouldBe(0);
        stateManager.Trace.Count(operation => operation == "ClearCache").ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task DeadLetterCallerCancellationPropagatesAfterNonCancelableDiscard()
    {
        using var cancellation = new CancellationTokenSource();
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-dead-letter-cancel");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        _ = context.DeadLetterPublisher.PublishDeadLetterAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<DeadLetterMessage>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromException<bool>(
                    new OperationCanceledException("dead-letter-secret", cancellation.Token));
            });

        await Should.ThrowAsync<OperationCanceledException>(
            () => context.Actor.ProcessCommandAsync(command, cancellation.Token));

        AssertAttemptStateAbsent(stateManager.CreateCommittedView(), command);
        stateManager.CreateCommittedView()[PendingCountKey].ShouldBe(0);
        await context.DeadLetterPublisher.Received(1).PublishDeadLetterAsync(
            command.AggregateIdentity,
            Arg.Any<DeadLetterMessage>(),
            cancellation.Token);
    }

    [Fact]
    public async Task DeadLetterFalseResultRemainsAdvisoryAfterFailedAttemptIsDiscarded()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-dead-letter-false");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        _ = context.DeadLetterPublisher.PublishDeadLetterAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<DeadLetterMessage>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stateManager.Trace.Add("DeadLetter");
                return false;
            });

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        result.FailureReason.ShouldContain("Stage=Processing");
        result.FailureReason.ShouldNotContain("primary-secret");
        AssertSuccessfulRejectionEndState(stateManager, command);
        AssertClearBeforeRejectionOperations(stateManager, command, GetPipelineKey(command));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonCallerDeadLetterFailureRemainsAdvisoryAndPreservesPrimaryCause(
        bool cancellationShapedFailure)
    {
        var logs = new List<LogEntry>();
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            logger: new TestLogger<AggregateActor>(logs),
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-dead-letter-advisory");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        _ = context.DeadLetterPublisher.PublishDeadLetterAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<DeadLetterMessage>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(cancellationShapedFailure
                ? new OperationCanceledException("dead-letter-secret")
                : new IOException("dead-letter-secret"));

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        result.FailureReason.ShouldContain("Stage=Processing");
        result.FailureReason.ShouldNotContain("primary-secret");
        result.FailureReason.ShouldNotContain("dead-letter-secret");
        AssertSuccessfulRejectionEndState(stateManager, command);
        LogEntry advisory = logs.Where(entry => entry.EventId.Id == 2021).ShouldHaveSingleItem();
        advisory.Message.ShouldContain("PrimaryExceptionType=HttpRequestException");
        advisory.Message.ShouldContain(
            $"DeadLetterExceptionType={(cancellationShapedFailure ? nameof(OperationCanceledException) : nameof(IOException))}");
        advisory.Message.ShouldNotContain("dead-letter-secret");
    }

    [Theory]
    [InlineData("ClearCache", "ClearCache")]
    [InlineData("CheckpointRejected", "CheckpointRejected")]
    [InlineData("CleanupPipeline", "CleanupPipeline")]
    [InlineData("SaveRejection", "SaveRejection")]
    public async Task InfrastructureRemediationFailureReportsExactSafeFieldsAndNoAttemptLeak(
        string failurePoint,
        string expectedOperation)
    {
        var logs = new List<LogEntry>();
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            logger: new TestLogger<AggregateActor>(logs),
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: $"corr-remediation-{failurePoint}");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        ConfigureInfrastructureRemediationFault(stateManager, command, failurePoint);

        ActorStateRemediationException exception = await Should.ThrowAsync<ActorStateRemediationException>(
            () => context.Actor.ProcessCommandAsync(command));

        exception.PrimaryFailureStage.ShouldBe(nameof(CommandStatus.Processing));
        exception.PrimaryExceptionType.ShouldBe(nameof(HttpRequestException));
        exception.RemediationOperation.ShouldBe(expectedOperation);
        exception.Message.ShouldNotContain("primary-secret");
        exception.Message.ShouldNotContain("remediation-secret");
        AssertAttemptStateAbsent(stateManager.CreateCommittedView(), command);
        stateManager.CreateCommittedView()[PendingCountKey].ShouldBe(0);
        LogEntry remediation = logs.Last(entry => entry.EventId.Id == 2020);
        remediation.Message.ShouldContain($"RemediationOperation={expectedOperation}");
        remediation.Message.ShouldContain("PrimaryExceptionType=HttpRequestException");
        remediation.Message.ShouldContain("FailedBatchDiscarded=True");
        remediation.Message.ShouldNotContain("primary-secret");
        remediation.Message.ShouldNotContain("remediation-secret");
    }

    [Theory]
    [InlineData("ClearCache", "ClearCache")]
    [InlineData("CleanupPipeline", "CleanupPipeline")]
    [InlineData("SaveConflictRejection", "SaveConflictRejection")]
    public async Task ConflictExhaustionRemediationFailurePreservesWinnerAndPrimaryConflict(
        string failurePoint,
        string expectedOperation)
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            concurrencyOptions: new CommandConcurrencyOptions { MaxPersistenceConflictRetries = 0 },
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: $"corr-conflict-{failurePoint}");
        AggregateIdentity identity = command.AggregateIdentity;
        EventEnvelope winnerEvent = CreateEvent(identity, 1, "winner-message");
        var winnerMetadata = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "winner-etag");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        stateManager.FaultOnCall(
            "SaveState",
            2,
            new InvalidOperationException("conflict-secret"),
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.MetadataKey] = winnerMetadata,
                [$"{identity.EventStreamKeyPrefix}1"] = winnerEvent,
            }));
        ConfigureConflictRemediationFault(stateManager, command, failurePoint);

        ActorStateRemediationException exception = await Should.ThrowAsync<ActorStateRemediationException>(
            () => context.Actor.ProcessCommandAsync(command));

        exception.PrimaryFailureStage.ShouldBe("PersistenceConflict");
        exception.PrimaryExceptionType.ShouldBe(nameof(ConcurrencyConflictException));
        exception.RemediationOperation.ShouldBe(expectedOperation);
        exception.Message.ShouldNotContain("conflict-secret");
        exception.Message.ShouldNotContain("remediation-secret");
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        durable[identity.MetadataKey].ShouldBe(winnerMetadata);
        durable[$"{identity.EventStreamKeyPrefix}1"].ShouldBe(winnerEvent);
        durable.ShouldNotContainKey($"{identity.EventStreamKeyPrefix}2");
        durable.ShouldNotContainKey($"idempotency:{command.MessageId}");
        durable[PendingCountKey].ShouldBe(0);
    }

    [Theory]
    [InlineData(false, "CleanupNotCommitted")]
    [InlineData(true, "CleanupCommitted")]
    public async Task RejectionSaveFailureReportsObservedPreOrPostCommitConsequence(
        bool commitThenThrow,
        string expectedObservation)
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: $"corr-save-ambiguity-{commitThenThrow}");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        if (commitThenThrow)
        {
            stateManager.FaultAfterCall("SaveState", 2, new IOException("remediation-secret"));
        }
        else
        {
            stateManager.FaultOnCall("SaveState", 2, new IOException("remediation-secret"));
        }

        ActorStateRemediationException exception = await Should.ThrowAsync<ActorStateRemediationException>(
            () => context.Actor.ProcessCommandAsync(command));

        exception.RemediationOperation.ShouldBe("SaveRejection");
        exception.FailedBatchDiscarded.ShouldBeTrue();
        exception.DurableStateObservation.ShouldBe(expectedObservation);
        exception.Message.ShouldNotContain("primary-secret");
        exception.Message.ShouldNotContain("remediation-secret");
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        AssertAttemptStateAbsent(durable, command);
        durable[PendingCountKey].ShouldBe(0);
        if (commitThenThrow)
        {
            durable.ShouldNotContainKey(GetPipelineKey(command));
        }
        else
        {
            ((PipelineState)durable[GetPipelineKey(command)]).CurrentStage.ShouldBe(CommandStatus.Processing);
        }
    }

    [Theory]
    [InlineData("Clear", "PendingFinalizerClear")]
    [InlineData("Read", "PendingFinalizerRead")]
    [InlineData("Write", "PendingFinalizerWrite")]
    [InlineData("Save", "PendingFinalizerSave")]
    public async Task FinalizerOperationFailureRepairsPendingSlotFromCleanDurableState(
        string failurePoint,
        string expectedOperation)
    {
        var logs = new List<LogEntry>();
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            logger: new TestLogger<AggregateActor>(logs),
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: $"corr-finalizer-{failurePoint}");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        ConfigureFinalizerFault(stateManager, failurePoint, cancellation: false);

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        durable[PendingCountKey].ShouldBe(0);
        AssertAttemptStateAbsent(durable, command);
        LogEntry finalizer = logs.Where(entry => entry.EventId.Id == 2022).ShouldHaveSingleItem();
        finalizer.Message.ShouldContain($"Operation={expectedOperation}");
        finalizer.Message.ShouldContain("ExceptionType=IOException");
        finalizer.Message.ShouldContain("FailedBatchDiscarded=True");
        finalizer.Message.ShouldContain("DurableStateObservation=RecoveredPreCommitFailure");
        finalizer.Message.ShouldNotContain("finalizer-secret");
    }

    [Fact]
    public async Task FinalizerCommitThenThrowIsObservedWithoutDoubleDecrement()
    {
        var logs = new List<LogEntry>();
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            logger: new TestLogger<AggregateActor>(logs),
            stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-finalizer-postcommit");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                command,
                new HttpRequestException("primary-secret")));
        stateManager.FaultAfterCall("SaveState", 3, new IOException("finalizer-secret"));

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        stateManager.CreateCommittedView()[PendingCountKey].ShouldBe(0);
        // Initial pending tracking, the deliberately poisoned attempt, and one finalizer decrement.
        // A mistaken post-commit repair would add a fourth write and double-decrement the slot.
        stateManager.Trace.Count(operation => operation == $"SetState:{PendingCountKey}").ShouldBe(3);
        LogEntry finalizer = logs.Where(entry => entry.EventId.Id == 2022).ShouldHaveSingleItem();
        finalizer.Message.ShouldContain("DurableStateObservation=CommitObserved");
        finalizer.Message.ShouldContain("CommittedBefore=1");
        finalizer.Message.ShouldContain("ExpectedAfter=0");
        finalizer.Message.ShouldContain("ObservedPendingCount=0");
    }

    [Fact]
    public async Task FinalizerCancellationPropagatesAndNextTurnRepairsThePendingSlot()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(stateManager: stateManager);
        CommandEnvelope first = CreateTestEnvelope(correlationId: "corr-finalizer-cancel");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                first,
                new HttpRequestException("primary-secret")));
        ConfigureFinalizerFault(stateManager, "Read", cancellation: true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => context.Actor.ProcessCommandAsync(first));

        stateManager.CreateCommittedView()[PendingCountKey].ShouldBe(1);
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());
        CommandEnvelope second = CreateTestEnvelope(correlationId: "corr-finalizer-recovery");

        CommandProcessingResult result = await context.Actor.ProcessCommandAsync(second);

        result.Accepted.ShouldBeTrue();
        stateManager.CreateCommittedView()[PendingCountKey].ShouldBe(0);
        AssertAttemptStateAbsent(stateManager.CreateCommittedView(), first);
    }

    [Fact]
    public async Task DoubleDiscardFailurePoisonsActivationAndBlocksLaterMutationUntilRecovery()
    {
        var logs = new List<LogEntry>();
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(
            logger: new TestLogger<AggregateActor>(logs),
            stateManager: stateManager);
        CommandEnvelope first = CreateTestEnvelope(correlationId: "corr-poison-source");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StageAttemptAndThrowAsync<DomainResult>(
                stateManager,
                first,
                new HttpRequestException("primary-secret")));
        stateManager.FaultOnCall("ClearCache", 1, new IOException("clear-secret-1"));
        stateManager.FaultOnCall("ClearCache", 2, new InvalidOperationException("clear-secret-2"));
        for (int call = 3; call <= 5; call++)
        {
            stateManager.FaultOnCall("ClearCache", call, new IOException($"clear-secret-{call}"));
        }

        ActorStateRemediationException firstFailure = await Should.ThrowAsync<ActorStateRemediationException>(
            () => context.Actor.ProcessCommandAsync(first));
        firstFailure.PrimaryExceptionType.ShouldBe(nameof(HttpRequestException));
        firstFailure.RemediationExceptionType.ShouldBe(nameof(IOException));
        firstFailure.FailedBatchDiscarded.ShouldBeFalse();

        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());
        int mutationsBeforeBlockedTurn = CountMutations(stateManager.Trace);
        CommandEnvelope blocked = CreateTestEnvelope(correlationId: "corr-poison-blocked");

        ActorStateRemediationException barrierFailure = await Should.ThrowAsync<ActorStateRemediationException>(
            () => context.Actor.ProcessCommandAsync(blocked));

        barrierFailure.RemediationOperation.ShouldBe("StateCacheBarrierClear");
        CountMutations(stateManager.Trace).ShouldBe(mutationsBeforeBlockedTurn);
        IReadOnlyDictionary<string, object> blockedDurable = stateManager.CreateCommittedView();
        blockedDurable[PendingCountKey].ShouldBe(1);
        AssertAttemptStateAbsent(blockedDurable, first);

        CommandEnvelope recovered = CreateTestEnvelope(correlationId: "corr-poison-recovered");
        CommandProcessingResult recoveredResult = await context.Actor.ProcessCommandAsync(recovered);

        recoveredResult.Accepted.ShouldBeTrue();
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        durable[PendingCountKey].ShouldBe(0);
        AssertAttemptStateAbsent(durable, first);
        logs.ShouldContain(entry => entry.EventId.Id == 2023);
    }

    [Fact]
    public async Task EventPersistenceRemediationMarksChildAndProcessActivitiesFailedWithoutSecrets()
    {
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext context = CreateActor(stateManager: stateManager);
        CommandEnvelope command = CreateTestEnvelope(correlationId: "corr-activity-remediation");
        _ = context.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        stateManager.FaultOnCall(
            $"SetState:{command.AggregateIdentity.MetadataKey}",
            1,
            new IOException("persistence-secret"));
        stateManager.FaultOnCall("ClearCache", 1, new IOException("remediation-secret"));

        IReadOnlyList<Activity> activities = await CaptureActivitiesAsync(
            command.CorrelationId,
            () => Should.ThrowAsync<ActorStateRemediationException>(
                () => context.Actor.ProcessCommandAsync(command)));

        Activity persist = activities
            .Where(activity => activity.OperationName == EventStoreActivitySource.EventsPersist)
            .ShouldHaveSingleItem();
        Activity process = activities
            .Where(activity => activity.OperationName == EventStoreActivitySource.ProcessCommand)
            .ShouldHaveSingleItem();
        persist.Status.ShouldBe(ActivityStatusCode.Error);
        process.Status.ShouldBe(ActivityStatusCode.Error);
        persist.Events.Any(activityEvent =>
                activityEvent.Tags.Any(tag =>
                    tag.Key == "exception.type"
                    && tag.Value is not null
                    && tag.Value.ToString()!.Contains(nameof(ActorStateRemediationException), StringComparison.Ordinal)))
            .ShouldBeTrue();
        string activityText = string.Join(
            '|',
            activities.SelectMany(activity => activity.Events).SelectMany(activityEvent => activityEvent.Tags)
                .Select(tag => $"{tag.Key}={tag.Value}"));
        activityText.ShouldNotContain("persistence-secret");
        activityText.ShouldNotContain("remediation-secret");
    }

    private static void ConfigureInfrastructureRemediationFault(
        FaultInjectingActorStateManager stateManager,
        CommandEnvelope command,
        string failurePoint)
    {
        switch (failurePoint)
        {
            case "ClearCache":
                stateManager.FaultOnCall("ClearCache", 1, new IOException("remediation-secret"));
                break;
            case "CheckpointRejected":
                stateManager.FaultOnCall(
                    $"SetState:{GetPipelineKey(command)}",
                    2,
                    new IOException("remediation-secret"));
                break;
            case "CleanupPipeline":
                stateManager.FaultOnCall(
                    $"TryRemoveState:{GetPipelineKey(command)}",
                    1,
                    new IOException("remediation-secret"));
                break;
            case "SaveRejection":
                stateManager.FaultOnCall("SaveState", 2, new IOException("remediation-secret"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failurePoint), failurePoint, "Unknown failure point.");
        }
    }

    private static void ConfigureConflictRemediationFault(
        FaultInjectingActorStateManager stateManager,
        CommandEnvelope command,
        string failurePoint)
    {
        switch (failurePoint)
        {
            case "ClearCache":
                stateManager.FaultOnCall("ClearCache", 1, new IOException("remediation-secret"));
                break;
            case "CleanupPipeline":
                stateManager.FaultOnCall(
                    $"TryRemoveState:{GetPipelineKey(command)}",
                    1,
                    new IOException("remediation-secret"));
                break;
            case "SaveConflictRejection":
                stateManager.FaultOnCall("SaveState", 3, new IOException("remediation-secret"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failurePoint), failurePoint, "Unknown failure point.");
        }
    }

    private static void ConfigureFinalizerFault(
        FaultInjectingActorStateManager stateManager,
        string failurePoint,
        bool cancellation)
    {
        Exception exception = cancellation
            ? new OperationCanceledException("finalizer-secret")
            : new IOException("finalizer-secret");
        switch (failurePoint)
        {
            case "Clear":
                stateManager.FaultOnCall("ClearCache", 2, exception);
                break;
            case "Read":
                stateManager.FaultOnCall($"TryGetState:{PendingCountKey}", 2, exception);
                break;
            case "Write":
                stateManager.FaultOnCall($"SetState:{PendingCountKey}", 3, exception);
                break;
            case "Save":
                stateManager.FaultOnCall("SaveState", 3, exception);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failurePoint), failurePoint, "Unknown failure point.");
        }
    }

    private static int CountMutations(IEnumerable<string> trace)
        => trace.Count(operation =>
            operation.StartsWith("AddState:", StringComparison.Ordinal)
            || operation.StartsWith("AddOrUpdateState:", StringComparison.Ordinal)
            || operation.StartsWith("GetOrAddState:", StringComparison.Ordinal)
            || operation.StartsWith("RemoveState:", StringComparison.Ordinal)
            || operation.StartsWith("SetState:", StringComparison.Ordinal)
            || operation.StartsWith("TryAddState:", StringComparison.Ordinal)
            || operation.StartsWith("TryRemoveState:", StringComparison.Ordinal)
            || operation == "SaveState");

    private static async Task<T> StageAttemptAndThrowAsync<T>(
        FaultInjectingActorStateManager stateManager,
        CommandEnvelope command,
        Exception exception)
    {
        await StageAttemptStateAsync(stateManager, command);
        throw exception;
    }

    private static async Task StageSnapshotAndThrowAsync(
        FaultInjectingActorStateManager stateManager,
        AggregateIdentity identity,
        Exception exception)
    {
        await stateManager.SetStateAsync(
            identity.SnapshotKey,
            new SnapshotRecord(
                1,
                "failed-snapshot",
                DateTimeOffset.UnixEpoch,
                identity.Domain,
                identity.AggregateId,
                identity.TenantId));
        throw exception;
    }

    private static async Task StageAttemptStateAsync(
        FaultInjectingActorStateManager stateManager,
        CommandEnvelope command)
    {
        AggregateIdentity identity = command.AggregateIdentity;
        await stateManager.SetStateAsync($"{identity.EventStreamKeyPrefix}1", CreateEvent(identity, 1, "failed-message"));
        await stateManager.SetStateAsync(identity.MetadataKey, new AggregateMetadata(99, DateTimeOffset.UnixEpoch, "failed-etag"));
        await stateManager.SetStateAsync(
            identity.SnapshotKey,
            new SnapshotRecord(
                99,
                "failed-snapshot",
                DateTimeOffset.UnixEpoch,
                identity.Domain,
                identity.AggregateId,
                identity.TenantId));
        await stateManager.SetStateAsync(
            UnpublishedPublicationIndex.StateKey,
            new UnpublishedPublicationIndex([
                new UnpublishedPublicationEntry(
                    command.MessageId,
                    command.CorrelationId,
                    DateTimeOffset.UnixEpoch),
            ]));
        await stateManager.SetStateAsync(
            UnpublishedEventsRecord.GetStateKey(command.MessageId),
            new UnpublishedEventsRecord(
                command.CorrelationId,
                1,
                1,
                1,
                command.CommandType,
                false,
                DateTimeOffset.UnixEpoch,
                0,
                "failed-attempt",
                command.MessageId));
        await stateManager.SetStateAsync($"idempotency:{command.MessageId}", "failed-idempotency");
        await stateManager.SetStateAsync(AttemptTriggerKey, "failed-trigger");
        await stateManager.SetStateAsync(PendingCountKey, 99);
    }

    private static void AssertClearBeforeRejectionOperations(
        FaultInjectingActorStateManager stateManager,
        CommandEnvelope command,
        string pipelineKey)
    {
        int stageIndex = stateManager.Trace.IndexOf($"SetState:{command.AggregateIdentity.MetadataKey}");
        int clearIndex = stateManager.Trace.FindIndex(stageIndex + 1, operation => operation == "ClearCache");
        int deadLetterIndex = stateManager.Trace.FindIndex(clearIndex + 1, operation => operation == "DeadLetter");
        int checkpointIndex = stateManager.Trace.FindIndex(
            deadLetterIndex + 1,
            operation => operation == $"SetState:{pipelineKey}");
        int cleanupIndex = stateManager.Trace.FindIndex(
            checkpointIndex + 1,
            operation => operation == $"TryRemoveState:{pipelineKey}");
        int saveIndex = stateManager.Trace.FindIndex(cleanupIndex + 1, operation => operation == "SaveState");

        stageIndex.ShouldBeGreaterThanOrEqualTo(0);
        clearIndex.ShouldBeGreaterThan(stageIndex);
        deadLetterIndex.ShouldBeGreaterThan(clearIndex);
        checkpointIndex.ShouldBeGreaterThan(deadLetterIndex);
        cleanupIndex.ShouldBeGreaterThan(checkpointIndex);
        saveIndex.ShouldBeGreaterThan(cleanupIndex);
    }

    private static void AssertSuccessfulRejectionEndState(
        FaultInjectingActorStateManager stateManager,
        CommandEnvelope command)
    {
        IReadOnlyDictionary<string, object> durable = stateManager.CreateCommittedView();
        AssertAttemptStateAbsent(durable, command);
        durable.ShouldNotContainKey(GetPipelineKey(command));
        durable[PendingCountKey].ShouldBe(0);
        stateManager.CommittedSnapshots.ShouldAllBe(snapshot =>
            !snapshot.ContainsKey(command.AggregateIdentity.MetadataKey));
    }

    private static void AssertNoTerminalResidue(
        IReadOnlyDictionary<string, object> durable,
        CommandEnvelope command)
    {
        durable.ShouldNotContainKey(GetPipelineKey(command));
        durable.ShouldNotContainKey(UnpublishedEventsRecord.GetStateKey(command.MessageId));
        durable.ShouldNotContainKey(AttemptTriggerKey);
        durable[PendingCountKey].ShouldBe(0);
        if (durable.TryGetValue(UnpublishedPublicationIndex.StateKey, out object? publicationState))
        {
            ((UnpublishedPublicationIndex)publicationState).Entries.ShouldBeEmpty();
        }
    }

    private static void AssertAttemptStateAbsent(
        IReadOnlyDictionary<string, object> durable,
        CommandEnvelope command)
    {
        AggregateIdentity identity = command.AggregateIdentity;
        durable.ShouldNotContainKey($"{identity.EventStreamKeyPrefix}1");
        durable.ShouldNotContainKey(identity.MetadataKey);
        durable.ShouldNotContainKey(identity.SnapshotKey);
        durable.ShouldNotContainKey(UnpublishedPublicationIndex.StateKey);
        durable.ShouldNotContainKey(UnpublishedEventsRecord.GetStateKey(command.MessageId));
        durable.ShouldNotContainKey($"idempotency:{command.MessageId}");
        durable.ShouldNotContainKey(AttemptTriggerKey);
    }

    private static string GetPipelineKey(CommandEnvelope command)
        => $"{command.AggregateIdentity.PipelineKeyPrefix}{command.CorrelationId}";

    private static EventEnvelope CreateEvent(AggregateIdentity identity, long sequence, string messageId)
        => new(
            messageId,
            identity.AggregateId,
            "test-aggregate",
            identity.TenantId,
            identity.Domain,
            sequence,
            sequence,
            DateTimeOffset.UnixEpoch,
            "winner-correlation",
            "winner-causation",
            "winner-user",
            "1.0.0",
            "WinnerEvent",
            1,
            "json",
            [7, 8, 9],
            null);

    private static async Task<IReadOnlyList<Activity>> CaptureActivitiesAsync(
        string correlationId,
        Func<Task> action)
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (string.Equals(
                    activity.GetTagItem(EventStoreActivitySource.TagCorrelationId)?.ToString(),
                    correlationId,
                    StringComparison.Ordinal))
                {
                    stopped.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        await action().ConfigureAwait(false);
        return stopped;
    }
}
