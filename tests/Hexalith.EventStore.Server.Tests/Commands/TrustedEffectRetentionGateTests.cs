using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

/// <summary>Source evidence and joint-retention admission tests.</summary>
public sealed class TrustedEffectRetentionGateTests
{
    /// <summary>A retained exact source envelope permits admission while the tenant is active.</summary>
    [Fact]
    public async Task RetainedSourceAndJointDecisionPermitAdmission()
    {
        (TrustedEffectRetentionGate gate, EffectIdentity identity, _, _, IAggregateActor source, _) = CreateGate();

        await gate.ValidateAsync(identity);

        _ = await source.Received(1).ReadEventsRangeAsync(6, 7, 1);
    }

    /// <summary>A restored source below its retained floor cannot redispatch an effect.</summary>
    [Fact]
    public async Task SourceBelowFloorDeniesBeforeSourceRead()
    {
        (TrustedEffectRetentionGate gate, EffectIdentity identity, ITrustedEffectSourceFloorProvider floors, _, IAggregateActor source, _) = CreateGate();
        _ = floors.GetRetainedFloorAsync(identity, Arg.Any<CancellationToken>()).Returns(8);

        await Should.ThrowAsync<InvalidOperationException>(() => gate.ValidateAsync(identity));

        _ = await source.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
    }

    /// <summary>Unknown floor is a denial, including when source bytes still exist.</summary>
    [Fact]
    public async Task UnknownFloorDeniesBeforeSourceRead()
    {
        (TrustedEffectRetentionGate gate, EffectIdentity identity, ITrustedEffectSourceFloorProvider floors, _, IAggregateActor source, _) = CreateGate();
        _ = floors.GetRetainedFloorAsync(identity, Arg.Any<CancellationToken>()).Returns((long?)null);

        await Should.ThrowAsync<InvalidOperationException>(() => gate.ValidateAsync(identity));

        _ = await source.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
    }

    /// <summary>Legal hold and offboarding close receipt disclosure before inspecting source state.</summary>
    [Theory]
    [InlineData(IdempotencyTenantLifecycleState.LegalHold)]
    [InlineData(IdempotencyTenantLifecycleState.Retaining)]
    [InlineData(IdempotencyTenantLifecycleState.PurgeEligible)]
    [InlineData(IdempotencyTenantLifecycleState.Purged)]
    public async Task NonActiveLifecycleDeniesBeforeSourceRead(IdempotencyTenantLifecycleState state)
    {
        (TrustedEffectRetentionGate gate, EffectIdentity identity, _, IIdempotencyTenantLifecycleActor lifecycle, IAggregateActor source, _) = CreateGate();
        _ = lifecycle.GetAsync().Returns(ActiveRecord() with { State = state });

        await Should.ThrowAsync<InvalidOperationException>(() => gate.ValidateAsync(identity));

        _ = await source.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
    }

    /// <summary>A floor alone cannot prove the source event survived a restore.</summary>
    [Fact]
    public async Task MissingSourceEnvelopeDeniesAdmission()
    {
        (TrustedEffectRetentionGate gate, EffectIdentity identity, _, _, IAggregateActor source, _) = CreateGate();
        _ = source.ReadEventsRangeAsync(6, 7, 1).Returns([]);

        await Should.ThrowAsync<InvalidOperationException>(() => gate.ValidateAsync(identity));
    }

    /// <summary>A source actor returning another tenant or stream is not evidence.</summary>
    [Fact]
    public async Task MismatchedSourceEnvelopeDeniesAdmission()
    {
        (TrustedEffectRetentionGate gate, EffectIdentity identity, _, _, IAggregateActor source, _) = CreateGate();
        _ = source.ReadEventsRangeAsync(6, 7, 1).Returns([SourceEnvelope() with { TenantId = "tenant-b" }]);

        await Should.ThrowAsync<InvalidOperationException>(() => gate.ValidateAsync(identity));
    }

    /// <summary>A failed joint retention decision prevents source inspection or receipt disclosure.</summary>
    [Fact]
    public async Task JointRetentionDenialStopsAdmission()
    {
        (TrustedEffectRetentionGate gate, EffectIdentity identity, _, _, IAggregateActor source,
            ITrustedEffectJointRetentionPolicy joint) = CreateGate();
        _ = joint.ValidateAsync(identity, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Joint decision unavailable."));

        await Should.ThrowAsync<InvalidOperationException>(() => gate.ValidateAsync(identity));

        _ = await source.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
    }

    private static (TrustedEffectRetentionGate Gate, EffectIdentity Identity,
        ITrustedEffectSourceFloorProvider Floors, IIdempotencyTenantLifecycleActor Lifecycle,
        IAggregateActor Source, ITrustedEffectJointRetentionPolicy Joint) CreateGate()
    {
        var identity = new EffectIdentity(
            "tenant-a", "works", "source-1", 7,
            EffectKindCatalog.DateResume, "works", "target-1", 0);
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        ITrustedEffectSourceFloorProvider floors = Substitute.For<ITrustedEffectSourceFloorProvider>();
        ITrustedEffectJointRetentionPolicy joint = Substitute.For<ITrustedEffectJointRetentionPolicy>();
        IIdempotencyTenantLifecycleActor lifecycle = Substitute.For<IIdempotencyTenantLifecycleActor>();
        IAggregateActor source = Substitute.For<IAggregateActor>();
        _ = factory.CreateActorProxy<IIdempotencyTenantLifecycleActor>(
            Arg.Any<ActorId>(), IdempotencyTenantLifecycleActor.ActorTypeName).Returns(lifecycle);
        _ = factory.CreateActorProxy<IAggregateActor>(
            Arg.Any<ActorId>(), nameof(AggregateActor)).Returns(source);
        _ = lifecycle.GetAsync().Returns(ActiveRecord());
        _ = floors.GetRetainedFloorAsync(identity, Arg.Any<CancellationToken>()).Returns(1);
        _ = source.ReadEventsRangeAsync(6, 7, 1).Returns([SourceEnvelope()]);
        var gate = new TrustedEffectRetentionGate(
            factory, floors, joint, Options.Create(new EventStoreActorOptions()));
        return (gate, identity, floors, lifecycle, source, joint);
    }

    private static IdempotencyTenantLifecycleRecord ActiveRecord()
        => new(1, "tenant-a", IdempotencyTenantLifecycleState.Active,
            DateTimeOffset.UnixEpoch, null, null, null, null, []);

    private static EventEnvelope SourceEnvelope()
        => new("event-1", "source-1", "work-item", "tenant-a", "works", 7, 7,
            DateTimeOffset.UnixEpoch, "correlation", "causation", "workload", "v1",
            "SourceOccurred", 1, "json", [], null);
}
