using System.Diagnostics;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Diagnostics;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Production router/handler awaits remain observable without changing results, tokens, or fence lifetimes.</summary>
public sealed class CommandDispatchDiagnosticsTests
{
    [Fact]
    public async Task ActorResponseAndLeaseReleaseAreDistinctInFlightStages()
    {
        using var activity = new Activity("dispatch-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var actorResult = new TaskCompletionSource<CommandProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actor = Substitute.For<IAggregateActor>();
        actor.ProcessCommandAsync(Arg.Any<CommandEnvelope>()).Returns(actorResult.Task);
        var router = Router(actor, logging, () => { releaseStarted.SetResult(); return release.Task; });
        var expected = new CommandProcessingResult(true, ResultPayload: "private-result");
        using var cancellation = new CancellationTokenSource();

        Task<CommandProcessingResult> pending = router.RouteCommandAsync(Command(), cancellation.Token);
        AssertOpen(log, "ActorRpc");
        log.Records.ShouldNotContain(record => Equals(record["Stage"], "RouterRelease"));
        pending.IsCompleted.ShouldBeFalse();
        cancellation.Cancel();
        actorResult.SetResult(expected);
        await releaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        AssertClosed(log, "ActorRpc", "Completed");
        AssertOpen(log, "RouterRelease");
        pending.IsCompleted.ShouldBeFalse();
        release.SetResult();
        (await pending).ShouldBeSameAs(expected);
        AssertClosed(log, "RouterRelease", "Completed");
        AssertIndependentOperation(log, "ActorRpc");
        AssertIndependentOperation(log, "RouterRelease");
        log.Records.Select(record => record["AttemptId"]).Distinct().ShouldHaveSingleItem();
        log.Records.ShouldAllBe(record => Equals(record["TraceId"], activity.TraceId.ToHexString())
            && Equals(record["SpanId"], activity.SpanId.ToHexString()));
        AssertPrivate(log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActorFailurePreservesOriginalExceptionAndRetainedFence(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        Exception original = cancel ? new OperationCanceledException("private-token", cancellation.Token) : new TimeoutException("private-url-and-key");
        var actor = Substitute.For<IAggregateActor>();
        actor.ProcessCommandAsync(Arg.Any<CommandEnvelope>()).Returns(_ =>
        {
            if (cancel) cancellation.Cancel();
            return Task.FromException<CommandProcessingResult>(original);
        });
        int releases = 0;
        var router = Router(actor, logging, () => { releases++; return Task.CompletedTask; });

        Exception? observed = null;
        try { await router.RouteCommandAsync(Command(), cancellation.Token); }
        catch (Exception exception) { observed = exception; }
        observed.ShouldBeSameAs(original);
        AssertClosed(log, "ActorRpc", "Failed");
        releases.ShouldBe(0);
        AssertClosed(log, "RouterRelease", "Completed");
        log.Records.Last(record => Equals(record["Stage"], "ActorRpc"))["RequestCanceled"].ShouldBe(cancel);
        log.Records.Last(record => Equals(record["Stage"], "ActorRpc"))["OperationCanBeCanceled"].ShouldBe(false);
        log.Records.Last(record => Equals(record["Stage"], "ActorRpc"))["OperationCanceled"].ShouldBe(false);
        AssertPrivate(log);
    }

    [Fact]
    public async Task LeaseFailureIsAttributedAndPreservesOriginalException()
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var original = new InvalidOperationException("private-release-state");
        var actor = Substitute.For<IAggregateActor>();
        actor.ProcessCommandAsync(Arg.Any<CommandEnvelope>()).Returns(new CommandProcessingResult(true));
        var router = Router(actor, logging, () => Task.FromException(original));

        (await Should.ThrowAsync<InvalidOperationException>(() => router.RouteCommandAsync(Command()))).ShouldBeSameAs(original);
        AssertClosed(log, "ActorRpc", "Completed");
        AssertClosed(log, "RouterRelease", "Failed");
        AssertPrivate(log);
    }

    [Theory]
    [InlineData("ProjectionActivation")]
    [InlineData("Routing")]
    [InlineData("StatusRead")]
    [InlineData("StatusWrite")]
    [InlineData("ArchiveRead")]
    [InlineData("ArchiveWrite")]
    [InlineData("CorrelationWrite")]
    [InlineData("ProjectionDelivery")]
    [InlineData("ActivityTracking")]
    [InlineData("StreamTracking")]
    [InlineData("SubmitRelease")]
    public async Task HandlerAttributesEachPendingPostActorAwaitAndReleasesLast(string heldStage)
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<T> HoldAsync<T>(string stage, T result)
        {
            if (stage == heldStage) { entered.SetResult(); await resume.Task; }
            return result;
        }
        Task HoldVoidAsync(string stage) => HoldAsync(stage, true);
        var command = Command();
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(command, Arg.Any<CancellationToken>()).Returns(_ => HoldAsync("Routing", new CommandProcessingResult(true, EventCount: 1)));
        var statuses = Substitute.For<ICommandStatusStore>();
        statuses.ReadStatusAsync(command.Tenant, command.MessageId, Arg.Any<CancellationToken>()).Returns(_ => HoldAsync<CommandStatusRecord?>("StatusRead",
            heldStage == "StatusWrite" ? null : new(CommandStatus.Completed, DateTimeOffset.UtcNow, command.AggregateId, 1, null, null, null, command.MessageId)));
        statuses.WriteStatusAsync(command.Tenant, command.MessageId, Arg.Any<CommandStatusRecord>(), Arg.Any<CancellationToken>()).Returns(_ => HoldVoidAsync("StatusWrite"));
        var archive = Substitute.For<ICommandArchiveStore>();
        archive.ReadCommandAsync(command.Tenant, command.MessageId, Arg.Any<CancellationToken>()).Returns(_ => HoldAsync<ArchivedCommand?>("ArchiveRead", null));
        archive.WriteCommandAsync(command.Tenant, command.MessageId, Arg.Any<ArchivedCommand>(), Arg.Any<CancellationToken>()).Returns(_ => HoldVoidAsync("ArchiveWrite"));
        var index = Substitute.For<ICommandCorrelationIndex>();
        index.AddAsync(command.Tenant, command.CorrelationId, command.MessageId, Arg.Any<CancellationToken>()).Returns(_ => HoldAsync("CorrelationWrite", CommandCorrelationIndexAddOutcome.Added));
        var projections = Substitute.For<IProjectionUpdateOrchestrator>();
        projections.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(_ => HoldVoidAsync("ProjectionDelivery"));
        var activity = Substitute.For<ICommandActivityTracker>();
        activity.TrackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CommandStatus>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_ => HoldVoidAsync("ActivityTracking"));
        var streams = Substitute.For<IStreamActivityTracker>();
        streams.TrackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(_ => HoldVoidAsync("StreamTracking"));
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.EnsureAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(_ => HoldVoidAsync("ProjectionActivation"));
        var handler = new SubmitCommandHandler(Authority(() => HoldVoidAsync("SubmitRelease")), statuses, archive, router, activity, streams, projections,
            logging.CreateLogger<SubmitCommandHandler>(), index, outbox);
        using var cancellation = new CancellationTokenSource();

        Task<SubmitCommandResult> pending = handler.Handle(command, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        AssertOpen(log, heldStage);
        pending.IsCompleted.ShouldBeFalse();
        if (heldStage != "SubmitRelease") log.Records.ShouldNotContain(record => Equals(record["Stage"], "SubmitRelease"));
        if (heldStage is "ProjectionDelivery" or "SubmitRelease") cancellation.Cancel();
        resume.SetResult();
        (await pending.WaitAsync(TimeSpan.FromSeconds(5))).MessageId.ShouldBe(command.MessageId);
        AssertClosed(log, heldStage, "Completed");
        if (heldStage is "ProjectionDelivery" or "SubmitRelease") AssertIndependentOperation(log, heldStage);
        log.Records.Last()["Stage"].ShouldBe("SubmitRelease");
        log.Records.Last()["Status"].ShouldBe("Completed");
        await router.Received(1).RouteCommandAsync(command, cancellation.Token);
        await projections.Received(1).UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), CancellationToken.None);
        AssertPrivate(log);
    }

    [Fact]
    public async Task AdvisoryFailureIsClosedBeforeHandlerContinues()
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(new CommandProcessingResult(true, EventCount: 1));
        var projections = Substitute.For<IProjectionUpdateOrchestrator>();
        projections.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("private-projection")));
        var handler = new SubmitCommandHandler(Substitute.For<ICommandStatusStore>(), Substitute.For<ICommandArchiveStore>(), router, null, null, projections,
            logging.CreateLogger<SubmitCommandHandler>());

        (await handler.Handle(Command(), default)).MessageId.ShouldBe(Command().MessageId);
        AssertClosed(log, "ProjectionDelivery", "Failed");
        AssertClosed(log, "SubmitRelease", "Completed");
        AssertPrivate(log);
    }

    [Fact]
    public async Task HandlerCancellationPreservesTheOriginalTokenAndReleasesAfterTheFailedAwait()
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        using var cancellation = new CancellationTokenSource();
        var original = new OperationCanceledException("private-cancellation", cancellation.Token);
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), cancellation.Token).Returns(new CommandProcessingResult(true));
        var statuses = Substitute.For<ICommandStatusStore>();
        statuses.ReadStatusAsync(Arg.Any<string>(), Arg.Any<string>(), cancellation.Token).Returns(_ =>
        {
            cancellation.Cancel();
            return Task.FromException<CommandStatusRecord?>(original);
        });
        int releases = 0;
        var handler = new SubmitCommandHandler(Authority(() => { releases++; return Task.CompletedTask; }), statuses,
            Substitute.For<ICommandArchiveStore>(), router, null, null, new NoOpProjectionUpdateOrchestrator(), logging.CreateLogger<SubmitCommandHandler>());

        OperationCanceledException? observed = null;
        try { await handler.Handle(Command(), cancellation.Token); }
        catch (OperationCanceledException exception) { observed = exception; }
        observed.ShouldBeSameAs(original);
        observed!.CancellationToken.ShouldBe(cancellation.Token);
        AssertClosed(log, "StatusRead", "Failed");
        AssertClosed(log, "SubmitRelease", "Completed");
        releases.ShouldBe(1);
        AssertPrivate(log);
    }

    [Fact]
    public async Task ProtectedAdmissionDoesNotPutKeysOrExecutionIdentitiesInDiagnosticRecords()
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var command = Command() with { IdempotencyKey = "private-idempotency-key" };
        var coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        coordinator.AdmitAsync(command, Arg.Any<CancellationToken>()).Returns(new IdempotencyAdmissionSession(
            "private-protected-actor", 1, IdempotencyAdmissionDecision.Pending, ExecutionMessageId: "private-execution-message"));
        var handler = new SubmitCommandHandler(Substitute.For<ICommandStatusStore>(), Substitute.For<ICommandArchiveStore>(),
            Substitute.For<ICommandRouter>(), logging.CreateLogger<SubmitCommandHandler>(), coordinator);

        (await handler.Handle(command, default)).MessageId.ShouldBe("private-execution-message");
        AssertClosed(log, "SubmitRelease", "Completed");
        AssertPrivate(log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledOrThrowingDiagnosticProviderDoesNotChangeSuccessfulDispatch(bool throwing)
    {
        var log = new DispatchDiagnosticLog(enabled: throwing, throwOnWrite: throwing);
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var actor = Substitute.For<IAggregateActor>();
        var result = new CommandProcessingResult(true);
        actor.ProcessCommandAsync(Arg.Any<CommandEnvelope>()).Returns(result);
        int releases = 0;
        var router = Router(actor, logging, () => { releases++; return Task.CompletedTask; });

        (await router.RouteCommandAsync(Command())).ShouldBeSameAs(result);
        releases.ShouldBe(1);
        log.Records.ShouldBeEmpty();
        using var minimal = new ServiceCollection().BuildServiceProvider();
        DispatchDiagnostics.Create<TechnicalHttpRouteCatalogReceiver>(minimal, default).ShouldBeNull();
    }

    [Theory]
    [InlineData("ordinary", "ActorRpc")]
    [InlineData("fenced", "FencedActorRpc")]
    [InlineData("reconcile", "ReconciliationRpc")]
    public async Task EveryRouterPathClosesAcquisitionBeforeRpcAndRpcBeforeRelease(string mode, string rpcStage)
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        using var cancellation = new CancellationTokenSource();
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rpcEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rpcResult = new TaskCompletionSource<CommandProcessingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconciliation = new TaskCompletionSource<IdempotencyCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SubmitCommand command = mode == "ordinary" ? Command() : ProtectedExecution();
        IdempotencyExecutionContext execution = ExecutionContext(command);
        var actor = Substitute.For<IAggregateActor>();
        actor.ProcessCommandAsync(Arg.Any<CommandEnvelope>()).Returns(_ => { rpcEntered.SetResult(); return rpcResult.Task; });
        actor.ProcessFencedCommandAsync(Arg.Any<FencedCommandEnvelope>()).Returns(call =>
        {
            call.Arg<FencedCommandEnvelope>().ExecutionContext.ShouldBeSameAs(execution);
            rpcEntered.SetResult();
            return rpcResult.Task;
        });
        actor.ReconcileFencedCommandAsync(Arg.Any<FencedCommandEnvelope>()).Returns(call =>
        {
            call.Arg<FencedCommandEnvelope>().Command.MessageId.ShouldBe(command.MessageId);
            rpcEntered.SetResult();
            return reconciliation.Task;
        });
        var actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(actor);
        var router = new CommandRouter(Authority(() => { releasing.SetResult(); return released.Task; }, acquired.Task),
            actors, Options.Create(new EventStoreActorOptions()), logging.CreateLogger<CommandRouter>());
        var result = new CommandProcessingResult(true, ResultPayload: "protected-result");
        var reconciled = new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, result);
        try
        {
            Task<CommandProcessingResult>? routed = mode == "ordinary" ? router.RouteCommandAsync(command, cancellation.Token)
                : mode == "fenced" ? router.RouteFencedCommandAsync(command, execution, cancellation.Token) : null;
            Task<IdempotencyCheckResult>? reconciledTask = mode == "reconcile" ? router.ReconcileFencedCommandAsync(command, execution, cancellation.Token) : null;
            Task pending = (Task?)routed ?? reconciledTask!;
            AssertOpen(log, "RouterAcquire");
            actor.ReceivedCalls().ShouldBeEmpty();
            log.Records.ShouldNotContain(record => Equals(record["Stage"], rpcStage));
            acquired.SetResult();
            await rpcEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            AssertClosed(log, "RouterAcquire", "Completed");
            AssertOpen(log, rpcStage);
            cancellation.Cancel();
            rpcResult.SetResult(result);
            reconciliation.SetResult(reconciled);
            await releasing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            AssertClosed(log, rpcStage, "Completed");
            AssertOpen(log, "RouterRelease");
            pending.IsCompleted.ShouldBeFalse();
            released.SetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (routed is not null) (await routed).ShouldBeSameAs(result);
            else (await reconciledTask!).ShouldBeSameAs(reconciled);
            AssertIndependentOperation(log, rpcStage);
            AssertIndependentOperation(log, "RouterRelease");
            if (mode != "ordinary") AssertProtectedOutput(log);
            else
            {
                log.Output.ShouldContain(command.MessageId);
                log.Output.ShouldContain(command.CorrelationId);
            }
        }
        finally
        {
            acquired.TrySetResult();
            rpcResult.TrySetResult(result);
            reconciliation.TrySetResult(reconciled);
            released.TrySetResult();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedRouterFailuresPreserveOriginalExceptionsWithoutLoggingTheirIdentities(bool reconcile)
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var original = new InvalidOperationException("protected-execution-message protected-execution-correlation protected-admission-actor protected-key protected-proof");
        var actor = Substitute.For<IAggregateActor>();
        actor.ProcessFencedCommandAsync(Arg.Any<FencedCommandEnvelope>()).Returns(Task.FromException<CommandProcessingResult>(original));
        actor.ReconcileFencedCommandAsync(Arg.Any<FencedCommandEnvelope>()).Returns(Task.FromException<IdempotencyCheckResult>(original));
        var router = Router(actor, logging, () => throw new InvalidOperationException("Retained lease must not be released."));
        SubmitCommand command = ProtectedExecution();
        Exception? observed = null;
        try
        {
            if (reconcile) await router.ReconcileFencedCommandAsync(command, ExecutionContext(command));
            else await router.RouteFencedCommandAsync(command, ExecutionContext(command));
        }
        catch (Exception exception) { observed = exception; }
        observed.ShouldBeSameAs(original);
        AssertClosed(log, reconcile ? "ReconciliationRpc" : "FencedActorRpc", "Failed");
        AssertProtectedOutput(log);
    }

    [Fact]
    public async Task HandlerAcquisitionStaysOpenBeforeAdmissionOrRoutingCanStart()
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        var session = new IdempotencyAdmissionSession("protected-admission-actor", 1, IdempotencyAdmissionDecision.Pending,
            ExecutionMessageId: "protected-execution-message");
        coordinator.AdmitAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(session);
        var router = Substitute.For<ICommandRouter>();
        var handler = new SubmitCommandHandler(Authority(() => Task.CompletedTask, acquired.Task), Substitute.For<ICommandStatusStore>(),
            Substitute.For<ICommandArchiveStore>(), router, null, null, new NoOpProjectionUpdateOrchestrator(), logging.CreateLogger<SubmitCommandHandler>(),
            idempotencyAdmissionCoordinator: coordinator);
        try
        {
            Task<SubmitCommandResult> pending = handler.Handle(Command() with { IdempotencyKey = "protected-key" }, default);
            AssertOpen(log, "SubmitAcquire");
            coordinator.ReceivedCalls().ShouldBeEmpty();
            router.ReceivedCalls().ShouldBeEmpty();
            acquired.SetResult();
            (await pending.WaitAsync(TimeSpan.FromSeconds(5))).MessageId.ShouldBe(session.ExecutionMessageId);
            AssertClosed(log, "SubmitAcquire", "Completed");
            AssertClosed(log, "Admission", "Completed");
            AssertClosed(log, "SubmitRelease", "Completed");
            AssertProtectedOutput(log);
        }
        finally { acquired.TrySetResult(); }
    }

    [Theory]
    [InlineData(false, "Admission", "Admission")]
    [InlineData(false, "ExecutionCapability", "ExecutionCapability")]
    [InlineData(false, "ExecutionBegin", "ExecutionBegin")]
    [InlineData(false, "InitialFence", "ExecutionFence")]
    [InlineData(false, "CompletionFence", "ExecutionFence")]
    [InlineData(false, "AdmissionCompletion", "AdmissionCompletion")]
    [InlineData(true, "Admission", "Admission")]
    [InlineData(true, "ReconciliationFence", "ExecutionFence")]
    [InlineData(true, "Reconciliation", "Reconciliation")]
    [InlineData(true, "ReconciledFence", "ExecutionFence")]
    [InlineData(true, "AdmissionCompletion", "AdmissionCompletion")]
    public async Task ProtectedHandlerAwaitsRemainOpenUntilTheActualOperationCompletes(bool unknown, string heldStep, string stageName)
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var steps = new List<string>();
        async Task HoldAsync(string step, CancellationToken token)
        {
            token.ShouldBe(cancellation.Token);
            steps.Add(step);
            if (step == heldStep) { entered.SetResult(); await resume.Task; }
        }
        SubmitCommand command = Command() with { IdempotencyKey = "protected-key" };
        SubmitCommand execution = ProtectedExecution();
        var session = new IdempotencyAdmissionSession("protected-admission-actor", 1,
            unknown ? IdempotencyAdmissionDecision.UnknownProviderOutcome : IdempotencyAdmissionDecision.Execute,
            ExecutionContext: ExecutionContext(execution), ExecutionMessageId: execution.MessageId, ExecutionCorrelationId: execution.CorrelationId);
        var coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        coordinator.AdmitAsync(command, Arg.Any<CancellationToken>()).Returns(async call => { await HoldAsync("Admission", call.Arg<CancellationToken>()); return (IdempotencyAdmissionSession?)session; });
        coordinator.ValidateExecutionCapabilityAsync(session, Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(call => HoldAsync("ExecutionCapability", call.Arg<CancellationToken>()));
        coordinator.BeginAsync(session, Arg.Any<CancellationToken>()).Returns(call => HoldAsync("ExecutionBegin", call.Arg<CancellationToken>()));
        int fences = 0;
        coordinator.ValidateExecutionAsync(session, Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<SubmitCommand>().MessageId.ShouldBe(execution.MessageId);
            string step = ++fences == 1 ? unknown ? "ReconciliationFence" : "InitialFence" : unknown ? "ReconciledFence" : "CompletionFence";
            return HoldAsync(step, call.Arg<CancellationToken>());
        });
        var result = new CommandProcessingResult(true);
        coordinator.CompleteAsync(session, result, Arg.Any<CancellationToken>()).Returns(call => HoldAsync("AdmissionCompletion", call.Arg<CancellationToken>()));
        var router = Substitute.For<ICommandRouter>();
        router.RouteFencedCommandAsync(Arg.Any<SubmitCommand>(), session.ExecutionContext!, Arg.Any<CancellationToken>()).Returns(result);
        router.ReconcileFencedCommandAsync(Arg.Any<SubmitCommand>(), session.ExecutionContext!, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await HoldAsync("Reconciliation", call.Arg<CancellationToken>());
            return new IdempotencyCheckResult(IdempotencyCheckOutcome.ExactTerminalDuplicate, result);
        });
        var statuses = Substitute.For<ICommandStatusStore>();
        statuses.ReadStatusAsync(command.Tenant, execution.MessageId, Arg.Any<CancellationToken>()).Returns(
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, command.AggregateId, 0, null, null, null, execution.MessageId));
        var archives = Substitute.For<ICommandArchiveStore>();
        archives.ReadCommandAsync(command.Tenant, execution.MessageId, Arg.Any<CancellationToken>()).Returns(execution.ToArchivedCommand());
        var handler = new SubmitCommandHandler(statuses, archives, router, null, null, new NoOpProjectionUpdateOrchestrator(),
            logging.CreateLogger<SubmitCommandHandler>(), idempotencyAdmissionCoordinator: coordinator);
        try
        {
            Task<SubmitCommandResult> pending = handler.Handle(command, cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            steps.Last().ShouldBe(heldStep);
            var records = log.Records.Where(record => Equals(record["Stage"], stageName)).ToArray();
            records.Last()["Status"].ShouldBe("Started");
            records.Count(record => Equals(record["Status"], "Started")).ShouldBe(records.Count(record => Equals(record["Status"], "Completed")) + 1);
            pending.IsCompleted.ShouldBeFalse();
            log.Records.ShouldNotContain(record => Equals(record["Stage"], "SubmitRelease"));
            resume.SetResult();
            SubmitCommandResult returned = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            returned.MessageId.ShouldBe(execution.MessageId);
            returned.CorrelationId.ShouldBe(command.CorrelationId);
            log.Records.Last(record => Equals(record["Stage"], stageName))["Status"].ShouldBe("Completed");
            await coordinator.Received(1).CompleteAsync(session, result, cancellation.Token);
            await coordinator.DidNotReceiveWithAnyArgs().MarkRecoveryAsync(default!, default, default);
            AssertProtectedOutput(log);
        }
        finally { resume.TrySetResult(); }
    }

    [Theory]
    [InlineData(IdempotencyAdmissionDecision.Pending)]
    [InlineData(IdempotencyAdmissionDecision.Replay)]
    public async Task EarlyProtectedReturnsCloseTheRealAdmissionAwait(IdempotencyAdmissionDecision decision)
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        var admitted = new TaskCompletionSource<IdempotencyAdmissionSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        coordinator.AdmitAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(admitted.Task);
        var router = Substitute.For<ICommandRouter>();
        var handler = new SubmitCommandHandler(Substitute.For<ICommandStatusStore>(), Substitute.For<ICommandArchiveStore>(), router,
            logging.CreateLogger<SubmitCommandHandler>(), coordinator);
        var session = new IdempotencyAdmissionSession("protected-admission-actor", 1, decision,
            ReplayResult: new CommandProcessingResult(true), ExecutionMessageId: "protected-execution-message");
        try
        {
            Task<SubmitCommandResult> pending = handler.Handle(Command() with { IdempotencyKey = "protected-key" }, default);
            AssertOpen(log, "Admission");
            router.ReceivedCalls().ShouldBeEmpty();
            admitted.SetResult(session);
            (await pending.WaitAsync(TimeSpan.FromSeconds(5))).MessageId.ShouldBe(session.ExecutionMessageId);
            AssertClosed(log, "Admission", "Completed");
            AssertClosed(log, "SubmitRelease", "Completed");
            log.Records.ShouldNotContain(record => Equals(record["Stage"], "ExecutionCapability"));
            AssertProtectedOutput(log);
        }
        finally { admitted.TrySetResult(session); }
    }

    [Theory]
    [InlineData("begin", false)]
    [InlineData("route", false)]
    [InlineData("status", false)]
    [InlineData("validation", false)]
    [InlineData("completion", false)]
    [InlineData("begin", true)]
    [InlineData("route", true)]
    [InlineData("completion", true)]
    public async Task RecoveryPersistenceIsAnIndependentAwaitAndPreservesTheOriginalFailure(string failureAt, bool recoveryFails)
    {
        var log = new DispatchDiagnosticLog();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        using var cancellation = new CancellationTokenSource();
        var recovering = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recover = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SubmitCommand execution = ProtectedExecution();
        var session = new IdempotencyAdmissionSession("protected-admission-actor", 1, IdempotencyAdmissionDecision.Execute,
            ExecutionContext: ExecutionContext(execution), ExecutionMessageId: execution.MessageId, ExecutionCorrelationId: execution.CorrelationId);
        Exception original = failureAt == "status" ? new CommandIdentityConflictException(execution.MessageId, Command().CorrelationId, execution.Tenant)
            : new InvalidOperationException("protected-execution-message protected-execution-correlation protected-admission-actor protected-key protected-proof");
        var coordinator = Substitute.For<IIdempotencyAdmissionCoordinator>();
        coordinator.AdmitAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(session);
        if (failureAt == "begin") coordinator.BeginAsync(session, Arg.Any<CancellationToken>()).Returns(Task.FromException(original));
        int fences = 0;
        coordinator.ValidateExecutionAsync(session, Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(_ =>
            ++fences == 2 && failureAt == "validation" ? Task.FromException(original) : Task.CompletedTask);
        if (failureAt == "completion") coordinator.CompleteAsync(session, Arg.Any<CommandProcessingResult>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(original));
        IdempotencyAdmissionState? state = null;
        coordinator.MarkRecoveryAsync(session, Arg.Any<IdempotencyAdmissionState>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            call.Arg<CancellationToken>().ShouldBe(CancellationToken.None);
            state = call.Arg<IdempotencyAdmissionState>();
            recovering.SetResult();
            await recover.Task;
            if (recoveryFails) throw new InvalidOperationException("protected-execution-message protected-key");
        });
        var router = Substitute.For<ICommandRouter>();
        router.RouteFencedCommandAsync(Arg.Any<SubmitCommand>(), session.ExecutionContext!, Arg.Any<CancellationToken>()).Returns(
            failureAt == "route" ? Task.FromException<CommandProcessingResult>(original) : Task.FromResult(new CommandProcessingResult(true)));
        var statuses = Substitute.For<ICommandStatusStore>();
        statuses.ReadStatusAsync(execution.Tenant, execution.MessageId, Arg.Any<CancellationToken>()).Returns(failureAt == "status"
            ? Task.FromException<CommandStatusRecord?>(original)
            : Task.FromResult<CommandStatusRecord?>(new(CommandStatus.Completed, DateTimeOffset.UtcNow, execution.AggregateId, 0, null, null, null, execution.MessageId)));
        var archives = Substitute.For<ICommandArchiveStore>();
        archives.ReadCommandAsync(execution.Tenant, execution.MessageId, Arg.Any<CancellationToken>()).Returns(execution.ToArchivedCommand());
        var handler = new SubmitCommandHandler(statuses, archives, router, null, null, new NoOpProjectionUpdateOrchestrator(),
            logging.CreateLogger<SubmitCommandHandler>(), idempotencyAdmissionCoordinator: coordinator);
        try
        {
            Task<SubmitCommandResult> pending = handler.Handle(Command() with { IdempotencyKey = "protected-key" }, cancellation.Token);
            await recovering.Task.WaitAsync(TimeSpan.FromSeconds(5));
            AssertOpen(log, "RecoveryPersistence");
            log.Records.ShouldNotContain(record => Equals(record["Stage"], "SubmitRelease"));
            state.ShouldBe(failureAt == "begin" ? IdempotencyAdmissionState.Recoverable : IdempotencyAdmissionState.UnknownProviderOutcome);
            cancellation.Cancel();
            recover.SetResult();
            Exception? observed = null;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception exception) { observed = exception; }
            observed.ShouldBeSameAs(original);
            AssertClosed(log, "RecoveryPersistence", recoveryFails ? "Failed" : "Completed");
            AssertIndependentOperation(log, "RecoveryPersistence");
            AssertIndependentOperation(log, "SubmitRelease");
            AssertProtectedOutput(log);
        }
        finally { recover.TrySetResult(); }
    }

    private static SubmitCommand ProtectedExecution() => Command() with
    {
        MessageId = "protected-execution-message",
        CorrelationId = "protected-execution-correlation",
    };

    private static IdempotencyExecutionContext ExecutionContext(SubmitCommand command) => new(1, "protected-admission-actor", 1, "v1",
        command.MessageId, command.CorrelationId, command.Tenant, command.Domain, command.AggregateId, command.CommandType, "protected-proof");

    private static void AssertProtectedOutput(DispatchDiagnosticLog log)
    {
        foreach (string secret in new[] { "protected-execution-message", "protected-execution-correlation", "protected-admission-actor", "protected-key", "protected-proof" })
            log.Output.ShouldNotContain(secret);
        AssertPrivate(log);
    }

    private static CommandRouter Router(IAggregateActor actor, ILoggerFactory logging, Func<Task> release)
    {
        var actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(actor);
        return new(Authority(release), actors, Options.Create(new EventStoreActorOptions()), logging.CreateLogger<CommandRouter>());
    }

    private static IRouteCatalogAuthority Authority(Func<Task> release, Task? acquisition = null)
    {
        var fixture = new RouteCatalogTestFixture();
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(fixture.Envelope), RouteCatalogCodec.ComputeRootDigest(fixture.Envelope), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (acquisition is not null) await acquisition;
            return new RouteCatalogDispatchLease(snapshot, release);
        });
        return authority;
    }

    private static SubmitCommand Command() => new("private-message", "tenant-a", "counter", "private-aggregate", "increment-counter",
        "private-body"u8.ToArray(), "private-correlation", "private-user", new() { ["private-metadata"] = "private-value" });

    internal static void AssertOpen(DispatchDiagnosticLog log, string stage)
        => log.Records.Where(record => Equals(record["Stage"], stage)).Select(record => record["Status"]).ShouldBe(["Started"]);

    internal static void AssertClosed(DispatchDiagnosticLog log, string stage, string status)
        => log.Records.Where(record => Equals(record["Stage"], stage)).Select(record => record["Status"]).ShouldBe(["Started", status]);

    internal static void AssertIndependentOperation(DispatchDiagnosticLog log, string stage)
    {
        var record = log.Records.Last(record => Equals(record["Stage"], stage));
        record["RequestCanceled"].ShouldBe(true);
        record["OperationCanBeCanceled"].ShouldBe(false);
        record["OperationCanceled"].ShouldBe(false);
    }

    internal static void AssertPrivate(DispatchDiagnosticLog log)
    {
        foreach (var record in log.Records)
        {
            record.Keys.Order().ShouldBe(new[] { "Stage", "Status", "ElapsedMilliseconds", "RequestCanceled", "OperationCanBeCanceled", "OperationCanceled", "TraceId", "SpanId", "AttemptId", "{OriginalFormat}", "Exception" }.Order());
            record["Exception"].ShouldBeNull();
            record["ElapsedMilliseconds"].ShouldBeOfType<long>().ShouldBeInRange(0, int.MaxValue);
            record["RequestCanceled"].ShouldBeOfType<bool>();
            record["OperationCanBeCanceled"].ShouldBeOfType<bool>();
            record["OperationCanceled"].ShouldBeOfType<bool>();
            record["Stage"]!.ToString().ShouldBeOneOf(Enum.GetNames<DispatchStageName>());
            record["Status"]!.ToString().ShouldBeOneOf("Started", "Completed", "Failed");
            record["TraceId"]!.ToString().ShouldMatch("^[a-f0-9]{32}$");
            record["SpanId"]!.ToString().ShouldMatch("^[a-f0-9]{16}$");
            record["AttemptId"]!.ToString().ShouldMatch("^[a-f0-9]{16}$");
            string.Join(" ", record.Values).ShouldNotContain("private-");
        }
    }
}
