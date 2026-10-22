using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class IdempotencyTenantLifecyclePurgerTests
{
    [Fact]
    public async Task PurgeAsync_DelegatesOneReferencePerSerializedLifecycleTurn()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IdempotencyTenantLifecycleRecord initial = Record("key-a");
        IdempotencyTenantLifecycleRecord expected = Record() with
        {
            State = IdempotencyTenantLifecycleState.Purged,
        };
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = lifecycle.GetAsync().Returns(initial);
        _ = lifecycle.PurgeAsync(1).Returns(expected);
        var purger = new IdempotencyTenantLifecyclePurger(factory);

        IdempotencyTenantLifecycleRecord result = await purger.PurgeAsync("tenant-a", 5);

        result.ShouldBe(expected);
        await lifecycle.Received(1).PurgeAsync(1);
        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionActor>(
            default!,
            default!);
        _ = factory.DidNotReceiveWithAnyArgs().CreateActorProxy<IIdempotencyAdmissionDirectoryActor>(
            default!,
            default!);
    }

    [Fact]
    public async Task PurgeAsync_NoProgressStopsWithoutRepeatingTheSameReference()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IdempotencyTenantLifecycleRecord unchanged = Record("key-a", "key-b");
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = lifecycle.GetAsync().Returns(unchanged);
        _ = lifecycle.PurgeAsync(1).Returns(unchanged);
        var purger = new IdempotencyTenantLifecyclePurger(factory);

        IdempotencyTenantLifecycleRecord result = await purger.PurgeAsync("tenant-a", 10);

        result.ShouldBe(unchanged);
        await lifecycle.Received(1).PurgeAsync(1);
    }

    [Fact]
    public async Task PurgeAsync_CancellationBetweenReferencesStopsBeforeNextTurn()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IdempotencyTenantLifecycleRecord initial = Record("key-a", "key-b");
        IdempotencyTenantLifecycleRecord oneRemaining = Record("key-b");
        using var cancellation = new CancellationTokenSource();
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = lifecycle.GetAsync().Returns(initial);
        _ = lifecycle.PurgeAsync(1).Returns(_ =>
        {
            cancellation.Cancel();
            return oneRemaining;
        });
        var purger = new IdempotencyTenantLifecyclePurger(factory);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => purger.PurgeAsync("tenant-a", 10, cancellation.Token));

        await lifecycle.Received(1).PurgeAsync(1);
    }

    [Fact]
    public async Task PurgeAsync_OversizedBatchIsCappedToExplicitIterationLimit()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IdempotencyTenantLifecycleRecord state = Record(
            Enumerable.Range(0, IdempotencyTenantLifecyclePurger.MaximumIterationsPerCall + 2)
                .Select(static index => $"key-{index}")
                .ToArray());
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
                Arg.Any<ActorId>(),
                IdempotencyTenantLifecycleActor.ActorTypeName)
            .Returns(lifecycle);
        _ = lifecycle.GetAsync().Returns(state);
        _ = lifecycle.PurgeAsync(1).Returns(_ =>
        {
            state = state with { References = state.References.Skip(1).ToArray() };
            return state;
        });
        var purger = new IdempotencyTenantLifecyclePurger(factory);

        IdempotencyTenantLifecycleRecord result = await purger.PurgeAsync("tenant-a", int.MaxValue);

        result.References.Length.ShouldBe(2);
        await lifecycle.Received(IdempotencyTenantLifecyclePurger.MaximumIterationsPerCall).PurgeAsync(1);
    }

    private static IdempotencyTenantLifecycleRecord Record(params string[] keyDigests)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new IdempotencyTenantLifecycleRecord(
            IdempotencyTenantLifecycleRecord.CurrentSchemaVersion,
            "tenant-a",
            IdempotencyTenantLifecycleState.PurgeEligible,
            now,
            now.AddDays(-401),
            now.AddDays(-1),
            TimeSpan.Zero,
            null,
            keyDigests.Select(key =>
                new IdempotencyTenantLifecycleReference($"tenant-a:v1:{key}", "v1", key)).ToArray());
    }
}
