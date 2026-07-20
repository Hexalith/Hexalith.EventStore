using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Exercises tenant/key admission against the real Dapr actor runtime and Redis state store.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class IdempotencyAdmissionLiveSidecarTests(DaprTestContainerFixture fixture)
{
    [Fact]
    public async Task MultiHostAdmission_PrimaryHostRemovedBeforeExecution_ExecutesAndReplaysExactlyOnceOnReplica()
    {
        const string RawKey = "live-multi-host-idempotency-key";
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        await fixture.EnsureReplicaAsync();

        bool primaryStopped = false;
        try
        {
            IIdempotencyAdmissionCoordinator primaryCoordinator = fixture.Services
                .GetRequiredService<IIdempotencyAdmissionCoordinator>();
            IIdempotencyAdmissionCoordinator replicaCoordinator = fixture.ReplicaServices
                .GetRequiredService<IIdempotencyAdmissionCoordinator>();
            string aggregateId = $"multi-host-{Guid.NewGuid():N}";
            var firstRequest = new SubmitCommand(
                MessageId: "01J88888888888888888888888",
                Tenant: "tenant-multi-host",
                Domain: "counter",
                AggregateId: aggregateId,
                CommandType: "IncrementCounter",
                Payload: "{}"u8.ToArray(),
                CorrelationId: "multi-host-correlation-0",
                UserId: "live-test-user",
                IdempotencyKey: RawKey);

            IdempotencyAdmissionSession[] admissions = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(async index =>
                {
                    IIdempotencyAdmissionCoordinator coordinator = index % 2 == 0
                        ? primaryCoordinator
                        : replicaCoordinator;
                    return await coordinator.AdmitAsync(
                        firstRequest with
                        {
                            MessageId = $"01J8888888888888888888888{index}",
                            CorrelationId = $"multi-host-correlation-{index}",
                        }) ?? throw new InvalidOperationException("Idempotency admission returned no session.");
                }));

            admissions.Count(result => result.Decision == IdempotencyAdmissionDecision.Execute).ShouldBe(1);
            admissions.Count(result => result.Decision == IdempotencyAdmissionDecision.Pending).ShouldBe(7);
            IdempotencyAdmissionSession executable = admissions.Single(result =>
                result.Decision == IdempotencyAdmissionDecision.Execute);
            admissions.Select(result => result.FencingToken).Distinct().ShouldBe([executable.FencingToken]);
            admissions.Select(result => result.ExecutionMessageId).Distinct().ShouldBe([executable.ExecutionMessageId]);
            admissions.Select(result => result.ExecutionCorrelationId).Distinct().ShouldBe([executable.ExecutionCorrelationId]);

            // Remove one complete application host and sidecar after the durable reservation but
            // before Begin/domain execution. The replica must finish the same fenced workflow.
            await fixture.StopPrimaryHostAndSidecarAsync();
            primaryStopped = true;
            await Task.Delay(2000);

            var executionRequest = firstRequest with
            {
                MessageId = executable.ExecutionMessageId!,
                CorrelationId = executable.ExecutionCorrelationId!,
                IdempotencyKey = null,
            };
            await replicaCoordinator.BeginAsync(executable);
            await replicaCoordinator.ValidateExecutionAsync(executable, executionRequest);
            ICommandRouter replicaRouter = fixture.ReplicaServices.GetRequiredService<ICommandRouter>();
            CommandProcessingResult terminal = await replicaRouter.RouteFencedCommandAsync(
                executionRequest,
                executable.ExecutionContext!);
            await replicaCoordinator.CompleteAsync(executable, terminal);

            var retry = firstRequest with
            {
                MessageId = "01J99999999999999999999999",
                CorrelationId = "retry-after-primary-removal",
            };
            IdempotencyAdmissionSession replay = (await replicaCoordinator.AdmitAsync(retry))!;
            replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
            replay.ReplayResult.ShouldBe(terminal);
            replay.ExecutionMessageId.ShouldBe(executable.ExecutionMessageId);
            replay.ExecutionCorrelationId.ShouldBe(executable.ExecutionCorrelationId);

            fixture.DomainServiceInvoker.Invocations.Count.ShouldBe(1);
            fixture.EventPublisher.PublishCalls.Count.ShouldBe(1);
            fixture.EventPublisher.TotalEventsPublished.ShouldBe(1);
            terminal.Accepted.ShouldBeTrue();
            terminal.EventCount.ShouldBe(1);

            string persisted = await fixture.GetActorStateJsonAsync(
                IdempotencyAdmissionActor.ActorTypeName,
                executable.ActorId,
                IdempotencyAdmissionActor.StateName);
            persisted.ShouldContain("\"replayExpiresAt\"");
            persisted.ShouldContain("\"replayResult\"");
            persisted.ShouldContain(executable.ExecutionMessageId!);
            persisted.ShouldNotContain(RawKey);
        }
        finally
        {
            await fixture.StopReplicaHostAndSidecarAsync();
            if (primaryStopped)
            {
                await fixture.RestartHostAndSidecarAsync();
            }
        }
    }

    [Fact]
    public async Task ConcurrentEquivalentAdmissions_ExecuteOnceAndPersistReplayableTerminalState()
    {
        const string RawKeyLeakSentinel = "raw-idempotency-key-must-never-persist";
        string keyDigest = $"digest-{Guid.NewGuid():N}";
        string actorId = $"tenant-a:v1:{keyDigest}";
        var factory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        });
        IIdempotencyAdmissionActor proxy = factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(actorId),
            IdempotencyAdmissionActor.ActorTypeName);
        var request = new IdempotencyAdmissionRequest(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-a",
            "v1",
            keyDigest,
            $"tag-{Guid.NewGuid():N}",
            $"intent-{Guid.NewGuid():N}",
            IdempotencyReplayRetentionTier.Mutation,
            "01J00000000000000000000000",
            "trace-concurrent");

        IdempotencyAdmissionResult[] concurrent = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => proxy.AdmitAsync(request)));

        concurrent.Count(result => result.Decision == IdempotencyAdmissionDecision.Execute).ShouldBe(1);
        concurrent.Count(result => result.Decision == IdempotencyAdmissionDecision.Pending).ShouldBe(7);
        long fence = concurrent.Single(result => result.Decision == IdempotencyAdmissionDecision.Execute).FencingToken;
        await proxy.BeginAsync(new IdempotencyAdmissionTransitionRequest(fence));
        var completed = new CommandProcessingResult(
            true,
            CorrelationId: "original-correlation",
            EventCount: 1,
            ResultPayload: "{\"ok\":true}");
        await proxy.CompleteAsync(new IdempotencyAdmissionCompletionRequest(fence, completed));

        IdempotencyAdmissionResult replay = await proxy.AdmitAsync(request);
        replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        replay.ReplayResult.ShouldBe(completed);
        IdempotencyAdmissionResult conflict = await proxy.AdmitAsync(request with
        {
            IntentDigest = $"different-{Guid.NewGuid():N}",
        });
        conflict.Decision.ShouldBe(IdempotencyAdmissionDecision.Conflict);

        string persisted = await fixture.GetActorStateJsonAsync(
            IdempotencyAdmissionActor.ActorTypeName,
            actorId,
            IdempotencyAdmissionActor.StateName);
        persisted.ShouldContain(keyDigest);
        persisted.ShouldContain(request.VerificationTag);
        persisted.ShouldContain(request.IntentDigest);
        persisted.ShouldContain("original-correlation");
        persisted.ShouldNotContain(RawKeyLeakSentinel);
    }

    [Fact]
    public async Task TerminalAdmission_SurvivesApplicationAndSidecarRestart()
    {
        string keyDigest = $"restart-digest-{Guid.NewGuid():N}";
        string actorId = $"tenant-restart:v1:{keyDigest}";
        var request = new IdempotencyAdmissionRequest(
            IdempotencyAdmissionRecord.CurrentSchemaVersion,
            "tenant-restart",
            "v1",
            keyDigest,
            $"tag-{Guid.NewGuid():N}",
            $"intent-{Guid.NewGuid():N}",
            IdempotencyReplayRetentionTier.Mutation,
            "01J11111111111111111111111",
            "trace-restart");
        IIdempotencyAdmissionActor proxy = CreateProxy(actorId);

        IdempotencyAdmissionResult admitted = await proxy.AdmitAsync(request);
        admitted.Decision.ShouldBe(IdempotencyAdmissionDecision.Execute);
        await proxy.BeginAsync(new IdempotencyAdmissionTransitionRequest(admitted.FencingToken));
        var completed = new CommandProcessingResult(
            true,
            CorrelationId: "before-restart",
            EventCount: 1,
            ResultPayload: "{\"persisted\":true}");
        await proxy.CompleteAsync(new IdempotencyAdmissionCompletionRequest(admitted.FencingToken, completed));

        await fixture.RestartHostAndSidecarAsync();

        IdempotencyAdmissionResult replay = await CreateProxy(actorId).AdmitAsync(request);
        replay.Decision.ShouldBe(IdempotencyAdmissionDecision.Replay);
        replay.ReplayResult.ShouldBe(completed);

        string persisted = await fixture.GetActorStateJsonAsync(
            IdempotencyAdmissionActor.ActorTypeName,
            actorId,
            IdempotencyAdmissionActor.StateName);
        persisted.ShouldContain("before-restart");
    }

    private IIdempotencyAdmissionActor CreateProxy(string actorId)
    {
        var factory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = fixture.DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        });
        return factory.CreateActorProxy<IIdempotencyAdmissionActor>(
            new ActorId(actorId),
            IdempotencyAdmissionActor.ActorTypeName);
    }
}
