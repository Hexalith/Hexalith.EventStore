using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;

using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogCommandAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandSubmissionValidatesCatalogBeforeStatusArchiveOrActorEffects(bool declared)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        ICommandStatusStore statuses = Substitute.For<ICommandStatusStore>();
        ICommandArchiveStore archives = Substitute.For<ICommandArchiveStore>();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(new CommandProcessingResult(true));
        var handler = new SubmitCommandHandler(fixture.Authorities[0], statuses, archives, router, null, null, new NoOpProjectionUpdateOrchestrator(), NullLogger<SubmitCommandHandler>.Instance);
        var command = Command(declared ? "increment-counter" : "not-declared");
        if (declared)
        {
            (await handler.Handle(command, default)).MessageId.ShouldBe(command.MessageId);
            await router.Received(1).RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>());
            statuses.ReceivedCalls().ShouldNotBeEmpty();
            archives.ReceivedCalls().ShouldNotBeEmpty();
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => handler.Handle(command, default));
            statuses.ReceivedCalls().ShouldBeEmpty(); archives.ReceivedCalls().ShouldBeEmpty(); router.ReceivedCalls().ShouldBeEmpty();
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrustedAdapterComparesCommittedAdmissionFacetBeforeCreatingIntent(bool drift)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var actual = SampleCatalog.CreateAdapters().Single(a => a.CommandType == "increment-counter");
        IDeclaredIdempotencyIntentAdapter adapter = drift ? Substitute.For<IDeclaredIdempotencyIntentAdapter>() : actual;
        if (drift)
        {
            adapter.CommandType.Returns(actual.CommandType); adapter.AdapterId.Returns(actual.AdapterId); adapter.OperationId.Returns(actual.OperationId);
            adapter.DescriptorVersion.Returns(actual.DescriptorVersion); adapter.RetentionTier.Returns(actual.RetentionTier);
            adapter.Declaration.Returns(actual.Declaration with { PolicyVersion = "changed-policy-v2" });
        }
        var registry = new IdempotencyIntentAdapterRegistry(fixture.Authorities[0], [adapter], new());
        if (drift)
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => registry.ResolveAsync(Command()));
            adapter.DidNotReceive().CreateIntent(Arg.Any<IdempotencyIntentCommand>());
        }
        else (await registry.ResolveAsync(Command())).AdapterId.ShouldBe(actual.AdapterId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmissionKeyPolicyMustMatchBeforeKeyOrDirectoryAccess(bool drift)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var entry = fixture.Envelope.Idempotency.Single(i => i.Domain == "counter" && i.CommandType == "increment-counter");
        IIdempotencyDigestKeyProvider keys = Substitute.For<IIdempotencyDigestKeyProvider>();
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        var options = new IdempotencyAdmissionOptions
        {
            Enabled = true, ActiveDigestKeyVersion = drift ? "not-committed" : entry.ActiveDigestKeyGeneration,
            ReaderDigestKeyVersions = entry.ReaderDigestKeyGenerations.Skip(1).ToList(),
        };
        var coordinator = new IdempotencyAdmissionCoordinator(fixture.Authorities[0], actors, new IdempotencyKeyProtector(keys), Substitute.For<IIdempotencyIntentAdapterRegistry>(), admissionOptions: Options.Create(options), catalogOptions: Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0] }));
        if (drift) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.AdmitAsync(Command() with { IdempotencyKey = "protected-key" }));
        else (await coordinator.AdmitAsync(Command())).ShouldBeNull(); // no-key control, after successful catalog policy validation
        keys.ReceivedCalls().ShouldBeEmpty(); actors.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandRpcAmbiguityRetainsDurableFenceAfterCallerTaskEnds(bool timeout)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        aggregate.ProcessCommandAsync(Arg.Any<CommandEnvelope>()).Returns(_ => Task.FromException<CommandProcessingResult>(
            timeout ? new TimeoutException("ambiguous command RPC") : new OperationCanceledException()));
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(aggregate);
        var router = new CommandRouter(fixture.Authorities[0], actors, Options.Create(new EventStoreActorOptions()), NullLogger<CommandRouter>.Instance);
        await Should.ThrowAsync<Exception>(() => router.RouteCommandAsync(Command()));
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].CommitAsync(second.AttemptId));
    }

    [Theory]
    [InlineData("healthy")]
    [InlineData("inventory")]
    [InlineData("register")]
    [InlineData("directory")]
    [InlineData("tenant-admission")]
    [InlineData("begin")]
    [InlineData("complete")]
    [InlineData("recovery")]
    [InlineData("authority-validation")]
    public async Task AdmissionAndLaterLifecycleRpcsRetainAmbiguousFencesUntilVerifiedQuiescence(string phase)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var entry = fixture.Envelope.Idempotency.Single(i => i.Domain == "counter" && i.CommandType == "increment-counter");
        IIdempotencyDigestKeyProvider keys = Substitute.For<IIdempotencyDigestKeyProvider>();
        keys.GetKeyRingAsync(Arg.Any<CancellationToken>()).Returns(_ => new ValueTask<IdempotencyDigestKeyRing>(new IdempotencyDigestKeyRing(
            entry.ActiveDigestKeyGeneration, entry.ReaderDigestKeyGenerations.ToDictionary(v => v, _ => new byte[32]), entry.ReaderDigestKeyGenerations.Skip(1).ToArray())));
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        IIdempotencyLegacyInventoryActor inventory = Substitute.For<IIdempotencyLegacyInventoryActor>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IIdempotencyAdmissionDirectoryActor directory = Substitute.For<IIdempotencyAdmissionDirectoryActor>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        actors.CreateActorProxy<IIdempotencyLegacyInventoryActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(inventory);
        actors.CreateActorProxy<IIdempotencyTenantLifecycleActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(lifecycle);
        actors.CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(directory);
        actors.CreateActorProxy<IIdempotencyAdmissionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(admission);
        inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>()).Returns(new IdempotencyLegacyInventoryInspection(IdempotencyLegacyInventoryDecision.NoLegacy));
        admission.InspectAsync().Returns(new IdempotencyAdmissionInspection(false));
        directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>()).Returns(call => new IdempotencyAdmissionDirectoryResult(call.Arg<IdempotencyAdmissionDirectoryRequest>().ActiveActorId, IdempotencyAdmissionPromotionPhase.Stable));
        var command = Command() with { IdempotencyKey = "protected-key" };
        lifecycle.AdmitAsync(Arg.Any<IdempotencyTenantLifecycleAdmissionRequest>()).Returns(new IdempotencyAdmissionResult(IdempotencyAdmissionDecision.Execute, 1,
            ExecutionMessageId: command.MessageId, ExecutionCorrelationId: command.CorrelationId));
        var registry = new IdempotencyIntentAdapterRegistry(fixture.Authorities[0], [SampleCatalog.CreateAdapters().Single(a => a.CommandType == command.CommandType)], new());
        var coordinator = new IdempotencyAdmissionCoordinator(actors, new IdempotencyKeyProtector(keys), registry,
            new IdempotencyExecutionContextProtector(keys, actors), fixture.Authorities[0], Options.Create(new IdempotencyAdmissionOptions
            {
                Enabled = true, ActiveDigestKeyVersion = entry.ActiveDigestKeyGeneration, ReaderDigestKeyVersions = entry.ReaderDigestKeyGenerations.Skip(1).ToList(),
            }), Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0] }));
        var remoteExecution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (phase == "inventory") inventory.InspectAsync(Arg.Any<IdempotencyAdmissionDirectoryAlias[]>()).Returns(_ => Task.FromException<IdempotencyLegacyInventoryInspection>(new TimeoutException()));
        if (phase == "register") lifecycle.RegisterAsync(Arg.Any<IdempotencyTenantLifecycleReference[]>()).Returns(_ => Task.FromException(new TimeoutException()));
        if (phase == "directory") directory.ResolveAsync(Arg.Any<IdempotencyAdmissionDirectoryRequest>()).Returns(_ => Task.FromException<IdempotencyAdmissionDirectoryResult>(new TimeoutException()));
        if (phase == "tenant-admission") lifecycle.AdmitAsync(Arg.Any<IdempotencyTenantLifecycleAdmissionRequest>()).Returns(_ => Task.FromException<IdempotencyAdmissionResult>(new TimeoutException()));
        if (phase is "inventory" or "register" or "directory" or "tenant-admission")
            await Should.ThrowAsync<TimeoutException>(() => coordinator.AdmitAsync(command));
        else
        {
            var session = (await coordinator.AdmitAsync(command))!;
            session.CatalogRootDigest.ShouldBe(first.RootDigest); session.CatalogEpoch.ShouldBe(1);
            await coordinator.ValidateExecutionCapabilityAsync(session, command);
            if (phase == "begin") admission.BeginAsync(Arg.Any<IdempotencyAdmissionTransitionRequest>()).Returns(_ => Task.FromException(new OperationCanceledException()));
            if (phase == "complete") admission.CompleteAsync(Arg.Any<IdempotencyAdmissionCompletionRequest>()).Returns(_ => Task.FromException(new TimeoutException()));
            if (phase == "recovery") admission.MarkRecoveryAsync(Arg.Any<IdempotencyAdmissionRecoveryRequest>()).Returns(_ => Task.FromException(new TimeoutException()));
            if (phase == "authority-validation") admission.ValidateAuthorityAsync(Arg.Any<IdempotencyAdmissionAuthorityRequest>()).Returns(_ => Task.FromException(new TimeoutException()));
            Task Invoke() => phase switch
            {
                "begin" => coordinator.BeginAsync(session),
                "complete" => coordinator.CompleteAsync(session, new CommandProcessingResult(true)),
                "recovery" => coordinator.MarkRecoveryAsync(session, IdempotencyAdmissionState.Recoverable),
                _ => coordinator.ValidateExecutionAsync(session, command),
            };
            if (phase == "healthy")
            {
                await coordinator.BeginAsync(session); await Invoke();
                await coordinator.CompleteAsync(session, new CommandProcessingResult(true));
                await coordinator.MarkRecoveryAsync(session, IdempotencyAdmissionState.Recoverable);
                (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
                return;
            }
            await Should.ThrowAsync<Exception>(Invoke);
        }
        var fence = (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
        var owner = new ObservedQuiescenceFixture(); owner.Observe(fence, remoteExecution.Task);
        Should.Throw<RouteCatalogUnavailableException>(() => owner.Issue(fence));
        remoteExecution.SetResult(); await remoteExecution.Task;
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].CommitAsync(second.AttemptId));
        var reconciler = fixture.CreateAuthority(fixture.Hosts[0], verifier: owner);
        await reconciler.ReconcileAsync(owner.Issue(fence));
        foreach (var host in fixture.Authorities) await host.ReadyAsync(second.AttemptId);
        await reconciler.CommitAsync(second.AttemptId);
    }

    [Theory]
    [InlineData("begin")]
    [InlineData("complete")]
    [InlineData("recovery")]
    [InlineData("validate")]
    public async Task AdmissionSessionCannotReviveAcrossCommitOrRollbackEpoch(string operation)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        IIdempotencyAdmissionActor admission = Substitute.For<IIdempotencyAdmissionActor>();
        actors.CreateActorProxy<IIdempotencyAdmissionActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(admission);
        var coordinator = new IdempotencyAdmissionCoordinator(fixture.Authorities[0], actors, new IdempotencyKeyProtector(Substitute.For<IIdempotencyDigestKeyProvider>()), Substitute.For<IIdempotencyIntentAdapterRegistry>());
        var session = new IdempotencyAdmissionSession("fixture-admission", 1, IdempotencyAdmissionDecision.Execute, CatalogRootDigest: first.RootDigest, CatalogEpoch: 1);
        await coordinator.BeginAsync(session); // Same-generation control.
        admission.ClearReceivedCalls();
        Task Invoke() => operation switch
        {
            "begin" => coordinator.BeginAsync(session),
            "complete" => coordinator.CompleteAsync(session, new CommandProcessingResult(true)),
            "recovery" => coordinator.MarkRecoveryAsync(session, IdempotencyAdmissionState.Recoverable),
            _ => coordinator.ValidateExecutionAsync(session, Command()),
        };
        var second = fixture.Candidate(2, first.RootDigest); await fixture.ActivateAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(Invoke);
        await fixture.Authorities[0].RollbackAsync(new(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await fixture.Authorities[0].ReadbackAsync()).Epoch, "committed"));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(Invoke); // Same root, different epoch still refuses.
        admission.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static SubmitCommand Command(string type = "increment-counter") => new("01M61800000000000000000001", "tenant-a", "counter", "counter-1", type,
        "{\"counterId\":\"counter-1\"}"u8.ToArray(), "01M61800000000000000000002", "fixture-user");
}
