
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Sample.Tests.Counter;

public class CounterProcessorTests {
    private readonly CounterProcessor _processor = new();

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
    public async Task IncrementCounter_NullState_ProducesCounterIncrementedEvent() {
        DomainResult result = await _processor.ProcessAsync(CreateCommand(new IncrementCounter()), currentState: null);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    [Fact]
    public async Task IncrementCounter_ExistingState_ProducesCounterIncrementedEvent() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());
        state.Apply(new CounterIncremented());

        DomainResult result = await _processor.ProcessAsync(CreateCommand(new IncrementCounter()), state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterIncremented>(result.Events[0]);
    }

    [Fact]
    public async Task DecrementCounter_CountIsZero_ProducesCounterCannotGoNegativeRejection() {
        DomainResult result = await _processor.ProcessAsync(CreateCommand(new DecrementCounter()), currentState: null);

        Assert.True(result.IsRejection);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterCannotGoNegative>(result.Events[0]);
    }

    [Fact]
    public async Task DecrementCounter_CountGreaterThanZero_ProducesCounterDecrementedEvent() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _processor.ProcessAsync(CreateCommand(new DecrementCounter()), state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterDecremented>(result.Events[0]);
    }

    [Fact]
    public async Task ResetCounter_CountIsZero_ProducesNoOp() {
        DomainResult result = await _processor.ProcessAsync(CreateCommand(new ResetCounter()), currentState: null);

        Assert.True(result.IsNoOp);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task ResetCounter_CountGreaterThanZero_ProducesCounterResetEvent() {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _processor.ProcessAsync(CreateCommand(new ResetCounter()), state);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<CounterReset>(result.Events[0]);
    }

    [Fact]
    public async Task UnknownCommandType_ThrowsInvalidOperationException() => await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.ProcessAsync(
                new CommandEnvelope(
                    TenantId: "sample-tenant",
                    Domain: "counter",
                    AggregateId: "counter-1",
                    CommandType: "UnknownCommand",
                    Payload: JsonSerializer.SerializeToUtf8Bytes(new { }),
                    CorrelationId: "corr-1",
                    CausationId: null,
                    UserId: "test-user",
                    Extensions: null),
                currentState: null));

    [Fact]
    public async Task EmptyPayload_ThrowsInvalidOperationException() => await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.ProcessAsync(
                new CommandEnvelope(
                    TenantId: "sample-tenant",
                    Domain: "counter",
                    AggregateId: "counter-1",
                    CommandType: "IncrementCounter",
                    Payload: [],
                    CorrelationId: "corr-1",
                    CausationId: null,
                    UserId: "test-user",
                    Extensions: null),
                currentState: null));
}
