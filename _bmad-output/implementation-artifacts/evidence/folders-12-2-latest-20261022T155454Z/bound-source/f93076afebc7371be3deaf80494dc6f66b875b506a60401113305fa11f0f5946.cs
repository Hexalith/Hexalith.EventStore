using System.Collections.Concurrent;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

/// <summary>Trusted commits retain the same durable projection wake-up as ordinary commands.</summary>
public sealed class TrustedEffectRouterProjectionTests
{
    /// <summary>DI selects the additive constructor and activates the exact target before any actor effect.</summary>
    [Theory]
    [InlineData(TrustedEffectDisposition.Success, false)]
    [InlineData(TrustedEffectDisposition.Success, true)]
    [InlineData(TrustedEffectDisposition.Rejection, false)]
    [InlineData(TrustedEffectDisposition.Rejection, true)]
    [InlineData(TrustedEffectDisposition.NoOp, false)]
    [InlineData(TrustedEffectDisposition.NoOp, true)]
    public async Task EveryReceiptKeepsDurableTargetActivationAndPostTurnDelivery(TrustedEffectDisposition disposition, bool replayed)
    {
        using var fixture = new Fixture(disposition, replayed);
        TrustedEffectResult result = await fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof", TestContext.Current.CancellationToken);
        result.ShouldBe(fixture.Result);
        await fixture.ProjectionSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Steps.ToArray().ShouldBe(["activation", "actor", "retention", "projection"]);
        fixture.PendingActivation.ShouldBeTrue();
        await fixture.Outbox.Received(1).EnsureAsync(fixture.Target, TestContext.Current.CancellationToken);
        await fixture.Projection.Received(1).UpdateProjectionAsync(fixture.Target, CancellationToken.None);
    }

    /// <summary>An unavailable durable outbox denies before the target can commit.</summary>
    [Fact]
    public async Task ActivationFailureStopsBeforeActorAndRetention()
    {
        using var fixture = new Fixture();
        _ = fixture.Outbox.EnsureAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("synthetic activation unavailable"));
        await Should.ThrowAsync<IOException>(() => fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof"));
        _ = await fixture.Actor.DidNotReceiveWithAnyArgs().ProcessTrustedEffectAsync(default!, default!, default!);
        await fixture.Gate.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
        await fixture.Projection.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default);
    }

    /// <summary>Cancellation cannot leave an activation for a request which never started.</summary>
    [Fact]
    public async Task CancellationBeforeRoutingDoesNotTouchActorOrOutbox()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof", cancellation.Token));
        await fixture.Outbox.DidNotReceiveWithAnyArgs().EnsureAsync(default!, default);
        _ = await fixture.Actor.DidNotReceiveWithAnyArgs().ProcessTrustedEffectAsync(default!, default!, default!);
    }

    /// <summary>The existing retention admission remains ahead of projection or actor effects.</summary>
    [Fact]
    public async Task MissingRetentionPolicyFailsBeforeActivation()
    {
        using var fixture = new Fixture();
        var router = new TrustedEffectRouter(fixture.Factory, Options.Create(new EventStoreActorOptions()), null);
        await Should.ThrowAsync<InvalidOperationException>(() => router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof"));
        await fixture.Outbox.DidNotReceiveWithAnyArgs().EnsureAsync(default!, default);
        _ = await fixture.Actor.DidNotReceiveWithAnyArgs().ProcessTrustedEffectAsync(default!, default!, default!);
    }

    /// <summary>A failed actor turn leaves payload-free work for independent SDK recovery.</summary>
    [Fact]
    public async Task ActorFailurePreservesActivationWithoutClaimingCompletion()
    {
        using var fixture = new Fixture();
        _ = fixture.Actor.ProcessTrustedEffectAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<string>())
            .Returns<Task<TrustedEffectResult>>(_ => throw new IOException("synthetic actor failure"));
        await Should.ThrowAsync<IOException>(() => fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof"));
        fixture.PendingActivation.ShouldBeTrue();
        await fixture.Gate.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
        await fixture.Projection.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default);
    }

    /// <summary>A failure after the target commit retains work even when no opportunistic nudge occurs.</summary>
    [Fact]
    public async Task RetentionFailureAfterCommitKeepsDurableRecoveryWork()
    {
        using var fixture = new Fixture();
        _ = fixture.Gate.CompleteAsync(Arg.Any<EffectIdentity>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("synthetic completion interruption"));
        await Should.ThrowAsync<IOException>(() => fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof"));
        fixture.PendingActivation.ShouldBeTrue();
        _ = await fixture.Actor.Received(1).ProcessTrustedEffectAsync(fixture.Submission, fixture.Context, "synthetic-proof");
        await fixture.Projection.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!, default);
    }

    /// <summary>Projection delivery cannot reenter a target or retention actor which still owns its turn.</summary>
    [Fact]
    public async Task DeliveryStartsOnlyAfterTargetAndRetentionTurnsFinish()
    {
        using var fixture = new Fixture();
        var targetTurn = new TaskCompletionSource<TrustedEffectResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var targetEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retentionTurn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retentionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = fixture.Actor.ProcessTrustedEffectAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<string>())
            .Returns(_ => { fixture.PendingActivation.ShouldBeTrue(); targetEntered.SetResult(); return targetTurn.Task; });
        _ = fixture.Gate.CompleteAsync(Arg.Any<EffectIdentity>(), Arg.Any<CancellationToken>())
            .Returns(_ => { retentionEntered.SetResult(); return retentionTurn.Task; });
        Task<TrustedEffectResult> routed = fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof");
        await targetEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.ProjectionSeen.Task.IsCompleted.ShouldBeFalse();
        targetTurn.SetResult(fixture.Result);
        await retentionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.ProjectionSeen.Task.IsCompleted.ShouldBeFalse();
        routed.IsCompleted.ShouldBeFalse();
        retentionTurn.SetResult();
        (await routed.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(fixture.Result);
        await fixture.ProjectionSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>A slow projection cannot consume the trusted receipt response's operation budget.</summary>
    [Fact]
    public async Task SlowProjectionDoesNotHoldTheCommittedReceipt()
    {
        using var fixture = new Fixture();
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = fixture.Projection.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(_ => { fixture.ProjectionSeen.TrySetResult(); return blocked.Task; });
        try
        {
            TrustedEffectResult result = await fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof").WaitAsync(TimeSpan.FromSeconds(5));
            result.ShouldBe(fixture.Result);
            await fixture.ProjectionSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
            blocked.Task.IsCompleted.ShouldBeFalse();
            fixture.PendingActivation.ShouldBeTrue();
        }
        finally { blocked.TrySetResult(); }
    }

    /// <summary>Optional delivery failure cannot discard the durable activation or revoke an actor receipt.</summary>
    [Fact]
    public async Task FailedOpportunisticDeliveryRetainsDurableWork()
    {
        using var fixture = new Fixture();
        _ = fixture.Projection.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => { fixture.ProjectionSeen.TrySetResult(); throw new IOException("synthetic nudge failure"); });
        (await fixture.Router.RouteAsync(fixture.Submission, fixture.Context, "synthetic-proof")).ShouldBe(fixture.Result);
        await fixture.ProjectionSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.PendingActivation.ShouldBeTrue();
    }

    /// <summary>The published three-argument constructor remains callable by already compiled consumers.</summary>
    [Fact]
    public void ReleasedConstructorSignatureRemainsAvailable()
        => typeof(TrustedEffectRouter).GetConstructor([typeof(IActorProxyFactory), typeof(IOptions<EventStoreActorOptions>), typeof(ITrustedEffectRetentionGate)])
            .ShouldNotBeNull();

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        internal Fixture(TrustedEffectDisposition disposition = TrustedEffectDisposition.Success, bool replayed = false)
        {
            var identity = new EffectIdentity("tenant-a", "work", "source-1", 7, EffectKindCatalog.DateResume, "work", "target-1", 0);
            Target = new AggregateIdentity(identity.Tenant, identity.TargetDomain, identity.TargetAggregate);
            string effect = EffectIdentityCodec.ComputeEffectId(identity);
            Submission = new(identity, "ResumeWorkItem", "{}"u8.ToArray(), "wrk-" + effect, "wrk-" + effect);
            Result = new(effect, disposition, replayed, null);
            _ = Factory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(Actor);
            _ = Outbox.EnsureAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(_ => { PendingActivation = true; Steps.Enqueue("activation"); return Task.CompletedTask; });
            _ = Actor.ProcessTrustedEffectAsync(Arg.Any<TrustedEffectSubmission>(), Arg.Any<TrustedEffectContext>(), Arg.Any<string>())
                .Returns(_ => { PendingActivation.ShouldBeTrue("activation must precede a trusted target commit"); Steps.Enqueue("actor"); return Task.FromResult(Result); });
            _ = Gate.CompleteAsync(Arg.Any<EffectIdentity>(), Arg.Any<CancellationToken>())
                .Returns(_ => { Steps.Enqueue("retention"); return Task.CompletedTask; });
            _ = Projection.UpdateProjectionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
                .Returns(_ => { Steps.Enqueue("projection"); ProjectionSeen.TrySetResult(); return Task.CompletedTask; });
            var services = new ServiceCollection();
            _ = services.AddSingleton(Factory).AddSingleton(Options.Create(new EventStoreActorOptions()))
                .AddSingleton(Gate).AddSingleton(Outbox).AddSingleton(Projection)
                .AddSingleton<ITrustedEffectRouter, TrustedEffectRouter>();
            _provider = services.BuildServiceProvider();
            Router = _provider.GetRequiredService<ITrustedEffectRouter>();
        }
        internal IActorProxyFactory Factory { get; } = Substitute.For<IActorProxyFactory>();
        internal IAggregateActor Actor { get; } = Substitute.For<IAggregateActor>();
        internal ITrustedEffectRetentionGate Gate { get; } = Substitute.For<ITrustedEffectRetentionGate>();
        internal IProjectionActivationOutbox Outbox { get; } = Substitute.For<IProjectionActivationOutbox>();
        internal IProjectionUpdateOrchestrator Projection { get; } = Substitute.For<IProjectionUpdateOrchestrator>();
        internal ITrustedEffectRouter Router { get; }
        internal TrustedEffectSubmission Submission { get; }
        internal TrustedEffectContext Context { get; } = new("works", "works.date-resume", "source-causation", "synthetic-delegation");
        internal TrustedEffectResult Result { get; }
        internal AggregateIdentity Target { get; }
        internal ConcurrentQueue<string> Steps { get; } = new();
        internal TaskCompletionSource ProjectionSeen { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool PendingActivation { get; private set; }
        public void Dispose() => _provider.Dispose();
    }
}
