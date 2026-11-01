using Hexalith.EventStore.Client.Projections;
using Dapr.Client;
using MediatR;

using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

/// <summary>Exercises durable ownership through production DI, the owning outbox, and the actual worker.</summary>
public sealed partial class SubmitCommandHandlerProjectionActivationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerBackedResponseReturnsExactActorResultBeforeHeldReplayAndWorkerOwnsLaterAttempt(bool scanBeforeServer)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var clock = new FakeTimeProvider();
        var outbox = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), clock);
        SubmitCommand command = Command();
        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        var replayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeReplay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        replay.UpdateProjectionAsync(identity, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            replayEntered.TrySetResult();
            return resumeReplay.Task;
        });
        var statuses = new InMemoryCommandStatusStore();
        var archives = new InMemoryCommandArchiveStore();
        var actorResult = new CommandProcessingResult(true, EventCount: 1, CorrelationId: command.CorrelationId,
            ResultPayload: "{\"count\":7}");
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(command, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            // Actual persisted write-ahead work exists before actor routing starts.
            (await outbox.GetAsync(identity)).ShouldNotBeNull().Revision.ShouldBe(1);
            await statuses.WriteStatusAsync(command.Tenant, command.MessageId, Completed(command));
            return actorResult;
        });
        int releases = 0;
        using ServiceProvider provider = BuildProvider(backend, outbox, router, replay,
            Authority(() => { releases++; return Task.CompletedTask; }), statuses, archives, clock: clock, scanBeforeServer: scanBeforeServer);
        provider.GetRequiredService<IRequestHandler<SubmitCommand, SubmitCommandResult>>().ShouldBeOfType<SubmitCommandHandler>();
        IMediator mediator = provider.GetRequiredService<IMediator>();
        using var request = new CancellationTokenSource();

        SubmitCommandResult result;
        try { result = await mediator.Send(command, request.Token).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch { resumeReplay.TrySetResult(); throw; }

        result.ShouldBe(new SubmitCommandResult(command.CorrelationId, actorResult.ResultPayload, command.MessageId));
        replayEntered.Task.IsCompleted.ShouldBeFalse();
        releases.ShouldBe(1);
        (await archives.ReadCommandAsync(command.Tenant, command.MessageId)).ShouldNotBeNull();
        ProjectionActivationWorkItem pending = (await outbox.GetAsync(identity)).ShouldNotBeNull();
        pending.Revision.ShouldBe(2);
        pending.Attempt.ShouldBe(0);
        request.Cancel();
        ProjectionActivationWorker worker = provider.GetRequiredService<ProjectionActivationWorkerCapability>().Worker;
        using var workerLifetime = new CancellationTokenSource();
        Task delivery = worker.RunOnceAsync(workerLifetime.Token);
        try
        {
            await replayEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            delivery.IsCompleted.ShouldBeFalse();
            (await outbox.GetAsync(identity)).ShouldBe(pending);
            resumeReplay.SetResult();
            await delivery.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { resumeReplay.TrySetResult(); }
        await replay.Received(1).UpdateProjectionAsync(identity, workerLifetime.Token);
        ProjectionActivationWorkItem deferred = (await outbox.GetAsync(identity)).ShouldNotBeNull();
        deferred.Revision.ShouldBe(pending.Revision);
        deferred.Attempt.ShouldBe(1);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task MissingOwnershipKeepsReplayAndRequestLeasePendingAndPreservesCancellation(
        bool workersEnabled, bool daprPresent, bool outboxPresent, bool capabilityPresent)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var clock = new FakeTimeProvider();
        var outbox = new DaprProjectionActivationOutbox(backend, Options.Create(new ProjectionOptions()), clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        replay.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            entered.TrySetResult();
            return resume.Task;
        });
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(true, EventCount: 1));
        int releases = 0;
        using ServiceProvider provider = BuildProvider(daprPresent ? backend : null, outboxPresent ? outbox : null,
            router, replay, Authority(() => { releases++; return Task.CompletedTask; }),
            workersEnabled: workersEnabled, capabilityPresent: capabilityPresent, clock: clock);
        SubmitCommandHandler handler = provider.GetRequiredService<SubmitCommandHandler>();
        using var cancellation = new CancellationTokenSource();
        var original = new OperationCanceledException(cancellation.Token);
        provider.GetRequiredService<ICommandActivityTracker>().TrackAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CommandStatus>(), Arg.Any<DateTimeOffset>(), Arg.Any<int?>(), Arg.Any<string?>(), cancellation.Token)
            .Returns(_ => Task.FromException(original));
        Task<SubmitCommandResult> response = handler.Handle(Command(), cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            response.IsCompleted.ShouldBeFalse();
            releases.ShouldBe(0);
            cancellation.Cancel();
            response.IsCompleted.ShouldBeFalse();
            releases.ShouldBe(0);
            await replay.Received(1).UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), CancellationToken.None);
            resume.SetResult();
            OperationCanceledException? observed = null;
            try { await response; }
            catch (OperationCanceledException exception) { observed = exception; }
            observed.ShouldBeSameAs(original);
            observed.ShouldNotBeNull().CancellationToken.ShouldBe(cancellation.Token);
            releases.ShouldBe(1);
        }
        finally { resume.TrySetResult(); }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task RequiredActivationFailureOrCancellationNeverAcknowledgesAndPreservesOriginalException(int failingWrite, bool canceled)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        using var cancellation = new CancellationTokenSource();
        Exception original = canceled ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("outbox unavailable");
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        int writes = 0;
        outbox.EnsureAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++writes != failingWrite) return Task.CompletedTask;
            if (canceled) cancellation.Cancel();
            return Task.FromException(original);
        });
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), cancellation.Token).Returns(new CommandProcessingResult(true, EventCount: 1));
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        using ServiceProvider provider = BuildProvider(backend, outbox, router, replay);
        SubmitCommandHandler handler = provider.GetRequiredService<SubmitCommandHandler>();

        Exception? observed = null;
        try { await handler.Handle(Command(), cancellation.Token); }
        catch (Exception exception) { observed = exception; }

        observed.ShouldBeSameAs(original);
        writes.ShouldBe(failingWrite);
        await router.Received(failingWrite - 1).RouteCommandAsync(Arg.Any<SubmitCommand>(), cancellation.Token);
        await replay.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task NoEventOrRejectedResultDoesNotRearmOrReplay(bool accepted, int events)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var outbox = Substitute.For<IProjectionActivationOutbox>();
        var router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(accepted, EventCount: events, ErrorMessage: accepted ? null : "rejected"));
        var replay = Substitute.For<IProjectionUpdateOrchestrator>();
        using ServiceProvider provider = BuildProvider(backend, outbox, router, replay);
        SubmitCommandHandler handler = provider.GetRequiredService<SubmitCommandHandler>();

        if (accepted) (await handler.Handle(Command(), default)).MessageId.ShouldBe(Command().MessageId);
        else await Should.ThrowAsync<InvalidOperationException>(() => handler.Handle(Command(), default));

        await outbox.Received(1).EnsureAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>());
        await replay.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default);
    }

    private static ServiceProvider BuildProvider(DaprClient? dapr, IProjectionActivationOutbox? outbox,
        ICommandRouter router, IProjectionUpdateOrchestrator replay, IRouteCatalogAuthority? authority = null,
        ICommandStatusStore? statuses = null, ICommandArchiveStore? archives = null,
        bool workersEnabled = true, bool capabilityPresent = true, TimeProvider? clock = null,
        bool optIn = true, bool startWorker = true, bool removeHostedWorker = false,
        IIdempotencyAdmissionCoordinator? coordinator = null, bool scanBeforeServer = true)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:Projections:WorkersEnabled"] = workersEnabled.ToString(),
            ["EventStore:Projections:WorkerBackedResponsesEnabled"] = optIn.ToString(),
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        if (dapr is not null) services.AddSingleton(dapr);
        if (scanBeforeServer) services.AddMediatR(settings => settings.RegisterServicesFromAssemblyContaining<SubmitCommandHandler>());
        services.AddEventStoreServer(configuration);
        if (!scanBeforeServer) services.AddMediatR(settings => settings.RegisterServicesFromAssemblyContaining<SubmitCommandHandler>());
        services.AddSingleton(authority ?? Authority(() => Task.CompletedTask));
        services.AddSingleton(statuses ?? new InMemoryCommandStatusStore());
        services.AddSingleton(archives ?? new InMemoryCommandArchiveStore());
        if (coordinator is not null) services.AddSingleton(coordinator);
        services.Configure<ProjectionDispatchOptions>(settings => settings.RetryWorkerInterval = TimeSpan.FromHours(1));
        services.AddSingleton(router);
        services.AddSingleton(replay);
        services.AddSingleton(Substitute.For<ICommandActivityTracker>());
        services.AddSingleton(Substitute.For<IStreamActivityTracker>());
        services.AddSingleton(Substitute.For<ICommandCorrelationIndex>());
        var guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.TryDeliverAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectionSourceGuardDeliveryResult(false, false, null));
        services.AddSingleton(guard);
        services.AddSingleton(clock ?? TimeProvider.System);
        services.RemoveAll<IProjectionActivationOutbox>();
        if (outbox is not null) services.AddSingleton(outbox);
        if (!capabilityPresent) services.RemoveAll<ProjectionActivationWorkerCapability>();
        if (removeHostedWorker) services.RemoveAll<IHostedService>();
        ServiceProvider provider = services.BuildServiceProvider();
        if (startWorker)
        {
            ProjectionActivationWorker? hosted = provider.GetServices<IHostedService>().OfType<ProjectionActivationWorker>().SingleOrDefault();
            hosted?.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        return provider;
    }

    private static IRouteCatalogAuthority Authority(Func<Task> release)
    {
        var fixture = new RouteCatalogTestFixture();
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(fixture.Envelope), RouteCatalogCodec.ComputeRootDigest(fixture.Envelope), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, release));
        return authority;
    }

    private static SubmitCommand Command() => new("message-1", "tenant-a", "counter", "counter-1", "increment-counter",
        [1], "correlation-1", "user-1");

    private static CommandStatusRecord Completed(SubmitCommand command) => new(CommandStatus.Completed, DateTimeOffset.UtcNow,
        command.AggregateId, 1, null, null, null, command.MessageId, command.CorrelationId);
}
