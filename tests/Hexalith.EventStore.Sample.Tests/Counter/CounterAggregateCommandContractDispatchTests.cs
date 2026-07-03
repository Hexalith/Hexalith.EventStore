using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.Counter;

/// <summary>
/// Proves <see cref="CounterAggregate"/> dispatches command envelopes keyed by the kebab-case
/// <see cref="ICommandContract.CommandType"/> discriminator submitted by generated REST controllers,
/// while preserving legacy CLR short-name dispatch.
/// </summary>
public sealed class CounterAggregateCommandContractDispatchTests
{
    private readonly IDomainProcessor _aggregate = new CounterAggregate();

    private static CommandEnvelope Envelope<T>(T command, string commandType)
        where T : notnull
        => new(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: "sample-tenant",
            Domain: "counter",
            AggregateId: "counter-1",
            CommandType: commandType,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "test-user",
            Extensions: null);

    [Fact]
    public async Task ProcessAsync_IncrementCounter_KebabCommandType_ProducesCounterIncremented()
    {
        DomainResult result = await _aggregate.ProcessAsync(
            Envelope(new IncrementCounter(), IncrementCounter.CommandType),
            currentState: null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<CounterIncremented>();
    }

    [Fact]
    public async Task ProcessAsync_DecrementCounter_KebabCommandType_DispatchesToDecrementHandler()
    {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _aggregate.ProcessAsync(
            Envelope(new DecrementCounter(), DecrementCounter.CommandType),
            state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<CounterDecremented>();
    }

    [Fact]
    public async Task ProcessAsync_ResetCounter_KebabCommandType_DispatchesToResetHandler()
    {
        // Reset on a zero counter is a no-op — proving the kebab discriminator resolved the reset handler
        // (not a "no Handle method found" throw).
        DomainResult result = await _aggregate.ProcessAsync(
            Envelope(new ResetCounter(), ResetCounter.CommandType),
            currentState: null);

        result.IsNoOp.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessAsync_CloseCounter_KebabCommandType_ProducesCounterClosed()
    {
        var state = new CounterState();
        state.Apply(new CounterIncremented());

        DomainResult result = await _aggregate.ProcessAsync(
            Envelope(new CloseCounter(), CloseCounter.CommandType),
            state);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<CounterClosed>();
    }

    [Fact]
    public async Task ProcessAsync_IncrementCounter_LegacyClrShortName_StillProducesCounterIncremented()
    {
        DomainResult result = await _aggregate.ProcessAsync(
            Envelope(new IncrementCounter(), nameof(IncrementCounter)),
            currentState: null);

        result.IsSuccess.ShouldBeTrue();
        result.Events.ShouldHaveSingleItem().ShouldBeOfType<CounterIncremented>();
    }
}
