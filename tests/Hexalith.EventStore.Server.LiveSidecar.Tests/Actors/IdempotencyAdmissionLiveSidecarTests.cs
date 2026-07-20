using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Exercises tenant/key admission against the real Dapr actor runtime and Redis state store.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public class IdempotencyAdmissionLiveSidecarTests(DaprTestContainerFixture fixture)
{
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
            IdempotencyReplayRetentionTier.Mutation);

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
            IdempotencyReplayRetentionTier.Mutation);
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
