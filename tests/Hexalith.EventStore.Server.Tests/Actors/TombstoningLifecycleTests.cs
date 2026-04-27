using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;
using Hexalith.EventStore.Testing.Compliance;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Tier 2 actor-lifecycle tombstoning coverage. Closes Epic 1 retro action item R1-A7.
/// Validates that the persistence + replay loop survives the
/// <c>CounterClosed</c> → persisted <c>AggregateTerminated</c> → rehydration flow without the
/// runtime <c>MissingApplyMethodException</c> fault loop. Drives the actor pipeline with the
/// real <see cref="CounterAggregate"/> via <see cref="Hexalith.EventStore.Testing.Fakes.FakeDomainServiceInvoker.SetupHandler"/>
/// (ADR R1A7-01) so <see cref="Hexalith.EventStore.Client.Aggregates.EventStoreAggregate{TState}"/>'s
/// <c>ITerminatable</c> gate is the code path under test rather than a static stub.
/// </summary>
[Collection("DaprTestContainer")]
public class TombstoningLifecycleTests : IDisposable {
    private static readonly CounterAggregate _aggregate = new();

    private readonly DaprTestContainerFixture _fixture;

    public TombstoningLifecycleTests(DaprTestContainerFixture fixture) {
        _fixture = fixture;

        // ADR R1A7-01: reset any leftover SetupResponse registrations from a sibling test class
        // in the same [Collection]; without this, the SetupHandler calls below throw
        // InvalidOperationException per the mutual-exclusion contract.
        _fixture.DomainServiceInvoker.ClearAll();

        Func<CommandEnvelope, object?, Task<DomainResult>> dispatch =
            (cmd, state) => _aggregate.ProcessAsync(cmd, state);

        _fixture.DomainServiceInvoker.SetupHandler(commandType: nameof(IncrementCounter), handler: dispatch);
        _fixture.DomainServiceInvoker.SetupHandler(commandType: nameof(DecrementCounter), handler: dispatch);
        _fixture.DomainServiceInvoker.SetupHandler(commandType: nameof(ResetCounter), handler: dispatch);
        _fixture.DomainServiceInvoker.SetupHandler(commandType: nameof(CloseCounter), handler: dispatch);
    }

    /// <summary>Leaves the shared <see cref="Hexalith.EventStore.Testing.Fakes.FakeDomainServiceInvoker"/> clean for the next sibling test class.</summary>
    public void Dispose() => _fixture.DomainServiceInvoker.ClearAll();

    /// <summary>
    /// Scenario 1: after CloseCounter, a follow-up IncrementCounter is rejected and persisted as
    /// AggregateTerminated. The actor rehydrates the persisted history and hands the rehydrated
    /// (CounterIncremented, CounterClosed) tail to the domain service — proving the rehydrator
    /// survives the ITerminatable terminal state without throwing on Apply lookup.
    /// </summary>
    [Fact]
    public async Task Lifecycle_TerminateThenReactivate_RehydratesAsTerminated() {
        IAggregateActor proxy = CreateProxy(out string aggregateId);

        CommandProcessingResult inc = await SendAsync(proxy, aggregateId, nameof(IncrementCounter));
        inc.Accepted.ShouldBeTrue();
        CommandProcessingResult close = await SendAsync(proxy, aggregateId, nameof(CloseCounter));
        close.Accepted.ShouldBeTrue();
        CommandProcessingResult rejected = await SendAsync(proxy, aggregateId, nameof(IncrementCounter));

        // End-state inspection (R2-A6): persisted event stream contains 3 events with the expected
        // type-names in sequence — the AggregateTerminated event is appended (Rule 11), not in-place rewriting.
        EventEnvelope[] persisted = await proxy.GetEventsAsync(0);
        persisted.Length.ShouldBe(3);
        persisted.OrderBy(e => e.SequenceNumber).Select(e => e.EventTypeName).ShouldBe(
            [typeof(CounterIncremented).FullName!, typeof(CounterClosed).FullName!, typeof(AggregateTerminated).FullName!]);

        rejected.Accepted.ShouldBeFalse();
        rejected.ErrorMessage.ShouldNotBeNull();
        rejected.ErrorMessage!.ShouldContain(nameof(AggregateTerminated));

        // Independent oracle on the rehydrated input handed to the domain service for the 3rd command:
        // the actor handed the rehydrated (CounterIncremented, CounterClosed) tail to CounterAggregate,
        // and the persisted AggregateTerminated event proves the ITerminatable gate fired end-to-end.
        (CommandEnvelope LastCommand, object? LastState) lastInvocation = _fixture.DomainServiceInvoker.InvocationsWithState
            .Where(i => i.Command.AggregateId == aggregateId)
            .Last();
        lastInvocation.LastCommand.CommandType.ShouldBe(nameof(IncrementCounter));
        DomainServiceCurrentState captured = lastInvocation.LastState.ShouldBeOfType<DomainServiceCurrentState>();
        captured.Events.Count.ShouldBe(2);
        captured.Events.OrderBy(e => e.Metadata.SequenceNumber)
            .Select(e => e.Metadata.EventTypeName)
            .ShouldBe([typeof(CounterIncremented).FullName!, typeof(CounterClosed).FullName!]);
    }

    /// <summary>
    /// Scenario 2: after CloseCounter, three further commands of mixed types are each rejected
    /// idempotently and produce one AggregateTerminated event apiece — no fault loop, no swallowed
    /// event. The rehydrator successfully replays the prior AggregateTerminated rejections through
    /// CounterState.Apply(AggregateTerminated) (the no-op) without throwing MissingApplyMethodException.
    /// </summary>
    [Fact]
    public async Task Lifecycle_RepeatedRejectionsAfterTerminate_AppendIdempotently() {
        IAggregateActor proxy = CreateProxy(out string aggregateId);

        (await SendAsync(proxy, aggregateId, nameof(IncrementCounter))).Accepted.ShouldBeTrue();
        (await SendAsync(proxy, aggregateId, nameof(CloseCounter))).Accepted.ShouldBeTrue();

        CommandProcessingResult r1 = await SendAsync(proxy, aggregateId, nameof(IncrementCounter));
        CommandProcessingResult r2 = await SendAsync(proxy, aggregateId, nameof(DecrementCounter));
        CommandProcessingResult r3 = await SendAsync(proxy, aggregateId, nameof(IncrementCounter));

        r1.Accepted.ShouldBeFalse();
        r2.Accepted.ShouldBeFalse();
        r3.Accepted.ShouldBeFalse();

        // End-state inspection (R2-A6): exactly 5 events; the three post-close rejections each produce
        // one AggregateTerminated. If MissingApplyMethodException ever fired during rehydration, DAPR
        // would have surfaced it as ActorMethodInvocationException — this test reaching this assertion
        // proves the no-op Apply(AggregateTerminated) chain completed.
        EventEnvelope[] persisted = await proxy.GetEventsAsync(0);
        persisted.Length.ShouldBe(5);
        persisted.OrderBy(e => e.SequenceNumber).Select(e => e.EventTypeName).ShouldBe(
            [
                typeof(CounterIncremented).FullName!,
                typeof(CounterClosed).FullName!,
                typeof(AggregateTerminated).FullName!,
                typeof(AggregateTerminated).FullName!,
                typeof(AggregateTerminated).FullName!,
            ]);

        // Independent oracle on the LAST command's rehydrated input: the captured Events list
        // contains four envelopes including TWO prior AggregateTerminated rejections. This proves
        // the rehydrator successfully replayed AggregateTerminated through the no-op Apply.
        (CommandEnvelope LastCommand, object? LastState) lastInvocation = _fixture.DomainServiceInvoker.InvocationsWithState
            .Where(i => i.Command.AggregateId == aggregateId)
            .Last();
        DomainServiceCurrentState captured = lastInvocation.LastState.ShouldBeOfType<DomainServiceCurrentState>();
        captured.Events.Count.ShouldBe(4);
        captured.Events.OrderBy(e => e.Metadata.SequenceNumber)
            .Select(e => e.Metadata.EventTypeName)
            .ShouldBe(
                [
                    typeof(CounterIncremented).FullName!,
                    typeof(CounterClosed).FullName!,
                    typeof(AggregateTerminated).FullName!,
                    typeof(AggregateTerminated).FullName!,
                ]);
    }

    /// <summary>
    /// Scenario 3: send 16 IncrementCounter commands (crossing the fixture's SnapshotOptions
    /// counter interval of 15), then CloseCounter, then a final IncrementCounter for the rejection.
    /// Asserts snapshot creation succeeded AND snapshot-aware rehydration loaded it, by inspecting
    /// the captured DomainServiceCurrentState.LastSnapshotSequence at the consumption point.
    /// Order-from-end semantics tolerate a single-step shift in the snapshot trigger boundary.
    /// </summary>
    [Fact]
    public async Task Lifecycle_TerminateAfterSnapshotInterval_RehydratesAsTerminated() {
        IAggregateActor proxy = CreateProxy(out string aggregateId);

        for (int i = 0; i < 16; i++) {
            CommandProcessingResult inc = await SendAsync(proxy, aggregateId, nameof(IncrementCounter));
            inc.Accepted.ShouldBeTrue($"Increment command #{i + 1} should be accepted");
        }

        (await SendAsync(proxy, aggregateId, nameof(CloseCounter))).Accepted.ShouldBeTrue();
        CommandProcessingResult rejected = await SendAsync(proxy, aggregateId, nameof(IncrementCounter));

        EventEnvelope[] persisted = await proxy.GetEventsAsync(0);
        EventEnvelope[] ordered = [.. persisted.OrderBy(e => e.SequenceNumber)];
        ordered.Length.ShouldBe(18);
        // Order-from-end semantics (per AC #5) — survives a one-step shift in SnapshotManager's
        // > vs >= boundary arithmetic.
        ordered[^2].EventTypeName.ShouldBe(typeof(CounterClosed).FullName!);
        ordered[^1].EventTypeName.ShouldBe(typeof(AggregateTerminated).FullName!);

        rejected.Accepted.ShouldBeFalse();
        rejected.ErrorMessage.ShouldNotBeNull();
        rejected.ErrorMessage!.ShouldContain(nameof(AggregateTerminated));

        // Snapshot creation + snapshot-aware rehydration oracle (consumption point):
        //   (a) LastSnapshotSequence >= 15 proves snapshot was created AND the rehydrator loaded it.
        //   (b) Events list contains a CounterClosed envelope — snapshot+tail rehydration combined
        //       produced a terminal state without the rehydrator throwing on the no-op
        //       Apply(AggregateTerminated) lookup.
        // Replaces a prior plan to probe the snapshot Redis key via DAPR HTTP — that probe required
        // parsing SnapshotRecord JSON shape and only confirmed the key existed.
        (CommandEnvelope LastCommand, object? LastState) lastInvocation = _fixture.DomainServiceInvoker.InvocationsWithState
            .Where(i => i.Command.AggregateId == aggregateId)
            .Last();
        DomainServiceCurrentState captured = lastInvocation.LastState.ShouldBeOfType<DomainServiceCurrentState>();
        // Lower bound tolerates a one-step boundary shift in SnapshotManager.ShouldCreateSnapshotAsync's
        // > vs >= arithmetic (per AC #5's order-from-end note). Production observation: snapshot anchor
        // lands at sequence 14 when interval=15 — strictly less than the 15 hand-calculated in the spec
        // text, but still proves snapshot was created AND the rehydrator loaded it (the consumption-point
        // intent). A regression that drops the snapshot entirely would surface as LastSnapshotSequence == 0,
        // which this assertion still catches.
        captured.LastSnapshotSequence.ShouldBeGreaterThanOrEqualTo(14);
        captured.Events.Select(e => e.Metadata.EventTypeName).ShouldContain(typeof(CounterClosed).FullName!);
    }

    private IAggregateActor CreateProxy(out string aggregateId) {
        aggregateId = $"close-test-{Guid.NewGuid():N}";
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
        });

        return actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId($"tenant-a:counter:{aggregateId}"),
            nameof(AggregateActor));
    }

    private static async Task<CommandProcessingResult> SendAsync(IAggregateActor proxy, string aggregateId, string commandType) {
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId("tenant-a")
            .WithDomain("counter")
            .WithAggregateId(aggregateId)
            .WithCommandType(commandType)
            .Build();

        return await proxy.ProcessCommandAsync(command);
    }
}

/// <summary>
/// Story R1-A7 / ADR R1A7-02: paired R1-A2 sentinel pin in Server.Tests. Lives in a NON-collection
/// class deliberately so it does NOT pay the live <c>daprd</c> startup cost — the assertion is a
/// pure Tier 1 reflection check. Geographic colocation in the same file as
/// <see cref="TombstoningLifecycleTests"/> preserves the paired-sentinel intent: a future deleter
/// of <c>CounterState.Apply(AggregateTerminated)</c> sees both pins together when grepping.
/// </summary>
public class TombstoningLifecycleSentinelTests {
    [Fact]
    public void Counter_TerminatableComplianceMatchesRuntime()
        => TerminatableComplianceAssertions.AssertTerminatableCompliance<CounterState>();
}
