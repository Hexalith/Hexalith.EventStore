namespace Hexalith.EventStore.Contracts.Tests.Counter;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

public class CounterProcessorTests
{
    private readonly CounterProcessor _processor = new();

    private static CommandEnvelope CreateCommand(string commandType)
        => new(
            TenantId: "sample-tenant",
            Domain: "counter",
            AggregateId: "counter-1",
            CommandType: commandType,
            Payload: [],
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "test-user",
            Extensions: null);

    [Fact]
    public async Task IncrementCounter_NullState_ProducesCounterIncrementedEvent()
    {
        DomainResult result = await _processor.ProcessAsync(CreateCommand("IncrementCounter"), currentState: null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Events);
        Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    [Fact]
    public async Task IncrementCounter_ExistingState_ProducesCounterIncrementedEvent()
    {
        var state = new CounterState();
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());

        DomainResult result = await _processor.ProcessAsync(CreateCommand("IncrementCounter"), state);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Events);
        Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    [Fact]
    public async Task DecrementCounter_CountIsZero_ProducesCounterCannotGoNegativeRejection()
    {
        DomainResult result = await _processor.ProcessAsync(CreateCommand("DecrementCounter"), currentState: null);

        Assert.True(result.IsRejection);
        Assert.Single(result.Events);
        Assert.IsType<CounterCannotGoNegative>(result.Events[0]);
    }

    [Fact]
    public async Task DecrementCounter_CountGreaterThanZero_ProducesCounterDecrementedEvent()
    {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _processor.ProcessAsync(CreateCommand("DecrementCounter"), state);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Events);
        Assert.IsType<CounterDecremented>(result.Events[0]);
    }

    [Fact]
    public async Task ResetCounter_CountIsZero_ProducesNoOp()
    {
        DomainResult result = await _processor.ProcessAsync(CreateCommand("ResetCounter"), currentState: null);

        Assert.True(result.IsNoOp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task ResetCounter_CountGreaterThanZero_ProducesCounterResetEvent()
    {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _processor.ProcessAsync(CreateCommand("ResetCounter"), state);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Events);
        Assert.IsType<CounterReset>(result.Events[0]);
    }

    [Fact]
    public async Task UnknownCommandType_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.ProcessAsync(CreateCommand("UnknownCommand"), currentState: null));
    }
}
