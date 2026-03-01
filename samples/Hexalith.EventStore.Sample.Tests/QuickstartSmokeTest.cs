using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

using Shouldly;

namespace Hexalith.EventStore.Sample.QuickstartTests;

public class QuickstartSmokeTest
{
    private readonly IDomainProcessor _aggregate = new CounterAggregate();

    private static CommandEnvelope CreateCommand<T>(T command)
        where T : notnull
        => new(
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
    public async Task Quickstart_IncrementCounter_ProducesCounterIncrementedEvent()
    {
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new IncrementCounter()), currentState: null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CounterIncremented>();
    }

    [Fact]
    public async Task Quickstart_IncrementThenDecrement_ProducesCounterDecrementedEvent()
    {
        // Execute increment through the documented command path
        DomainResult incrementResult = await _aggregate.ProcessAsync(CreateCommand(new IncrementCounter()), currentState: null);
        incrementResult.IsSuccess.ShouldBeTrue();
        incrementResult.Events.Count.ShouldBe(1);
        CounterIncremented incrementEvent = incrementResult.Events[0].ShouldBeOfType<CounterIncremented>();

        // Rehydrate state from the resulting event (simulates event sourcing replay)
        CounterState state = new();
        state.Apply(incrementEvent);
        state.Count.ShouldBe(1);

        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new DecrementCounter()), state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CounterDecremented>();
    }

    [Fact]
    public async Task Quickstart_DecrementOnZero_ProducesRejection()
    {
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new DecrementCounter()), currentState: null);

        result.IsRejection.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CounterCannotGoNegative>();
    }

    [Fact]
    public async Task Quickstart_ResetAfterIncrements_ProducesCounterResetEvent()
    {
        // First increment
        DomainResult firstIncrement = await _aggregate.ProcessAsync(CreateCommand(new IncrementCounter()), currentState: null);
        firstIncrement.IsSuccess.ShouldBeTrue();
        firstIncrement.Events.Count.ShouldBe(1);
        CounterIncremented firstEvent = firstIncrement.Events[0].ShouldBeOfType<CounterIncremented>();

        CounterState state = new();
        state.Apply(firstEvent);
        state.Count.ShouldBe(1);

        // Second increment (validates plural "Increments" in method name)
        DomainResult secondIncrement = await _aggregate.ProcessAsync(CreateCommand(new IncrementCounter()), state);
        secondIncrement.IsSuccess.ShouldBeTrue();
        secondIncrement.Events.Count.ShouldBe(1);
        CounterIncremented secondEvent = secondIncrement.Events[0].ShouldBeOfType<CounterIncremented>();
        state.Apply(secondEvent);
        state.Count.ShouldBe(2);

        // Reset after multiple increments
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new ResetCounter()), state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events[0].ShouldBeOfType<CounterReset>();
    }
}
