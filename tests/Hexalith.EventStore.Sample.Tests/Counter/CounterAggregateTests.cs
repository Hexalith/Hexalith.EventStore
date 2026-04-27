
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Testing.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Sample.Tests.Counter;

public class CounterAggregateTests {
    private readonly IDomainProcessor _aggregate = new CounterAggregate();

    private static CommandEnvelope CreateCommand<T>(T command)
        where T : notnull
        => new(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: "sample-tenant",
            Domain: "counter",
            AggregateId: "counter-1",
            CommandType: typeof(T).Name,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "test-user",
            Extensions: null);

    [Fact]
    public async Task ProcessAsync_IncrementCounter_NullState_ProducesCounterIncrementedEvent() {
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new IncrementCounter()), currentState: null);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_IncrementCounter_ExistingState_ProducesCounterIncrementedEvent() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());

        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new IncrementCounter()), state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_DecrementCounter_CountIsZero_ProducesRejection() {
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new DecrementCounter()), currentState: null);

        Assert.True(result.IsRejection);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterCannotGoNegative>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_DecrementCounter_CountGreaterThanZero_ProducesCounterDecrementedEvent() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new DecrementCounter()), state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterDecremented>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_ResetCounter_CountIsZero_ProducesNoOp() {
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new ResetCounter()), currentState: null);

        Assert.True(result.IsNoOp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task ProcessAsync_ResetCounter_CountGreaterThanZero_ProducesCounterResetEvent() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new ResetCounter()), state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterReset>(result.Events[0]);
    }

    // --- Story 1.5: Tombstoning tests ---

    [Fact]
    public async Task ProcessAsync_CloseCounter_ActiveCounter_ProducesCounterClosedEvent() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new CloseCounter()), state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterClosed>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_AnyCommand_AfterCounterClosed_RejectsWithAggregateTerminated() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());
        state.Apply(new CounterClosed()); // Terminate

        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new IncrementCounter()), state);

        Assert.True(result.IsRejection);
        _ = Assert.Single(result.Events);
        AggregateTerminated terminated = Assert.IsType<AggregateTerminated>(result.Events[0]);
        Assert.Equal("CounterAggregate", terminated.AggregateType);
        Assert.Equal("counter-1", terminated.AggregateId);
    }

    [Fact]
    public void CounterClosed_EventStream_IsReplayable() {
        // Simulate full event stream replay: increment, close, then AggregateTerminated rejection
        var state = new CounterState();
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterClosed());
        state.Apply(new AggregateTerminated("CounterAggregate", "counter-1")); // Rejection event replayed

        Assert.Equal(2, state.Count);
        Assert.True(state.IsTerminated);
    }

    [Fact]
    public void AddEventStore_SampleAssembly_ResolvesCounterAggregateViaKeyedDomainProcessor() {
        ServiceCollection services = new();
        _ = services.AddEventStore(typeof(CounterAggregate).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        IDomainProcessor keyed = provider.GetRequiredKeyedService<IDomainProcessor>("counter");

        _ = Assert.IsType<CounterAggregate>(keyed);
    }

    [Fact]
    public void CounterState_IsTerminatableCompliant() {
        // R1-A2 sentinel — do not delete; protects ITerminatable replay safety. See _bmad-output/implementation-artifacts/post-epic-1-r1a2-terminatable-compliance-helper.md
        TerminatableComplianceAssertions.AssertTerminatableCompliance<CounterState>();
    }

    [Fact]
    public void UseEventStore_SampleAssembly_ActivatesCounterWithConventionDerivedResourceNames() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(s => s.AddEventStore(typeof(CounterAggregate).Assembly))
            .Build();

        _ = host.UseEventStore();

        EventStoreActivationContext context = host.Services.GetRequiredService<EventStoreActivationContext>();
        EventStoreDomainActivation counterActivation = Assert.Single(context.Activations, a => a.DomainName == "counter");

        Assert.Equal("counter-eventstore", counterActivation.StateStoreName);
        Assert.Equal("counter.events", counterActivation.TopicPattern);
        Assert.Equal("deadletter.counter.events", counterActivation.DeadLetterTopicPattern);
    }
}
