
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Greeting;
using Hexalith.EventStore.Sample.Greeting.Commands;
using Hexalith.EventStore.Sample.Greeting.Events;
using Hexalith.EventStore.Sample.Greeting.State;

namespace Hexalith.EventStore.Sample.Tests.Greeting;

/// <summary>
/// Tests for the minimal Greeting domain aggregate.
/// </summary>
public class GreetingAggregateTests {
    private readonly IDomainProcessor _aggregate = new GreetingAggregate();

    private static CommandEnvelope CreateCommand<T>(T command)
        where T : notnull
        => new(
            MessageId: Guid.NewGuid().ToString(),
            TenantId: "sample-tenant",
            Domain: "greeting",
            AggregateId: "greeting-1",
            CommandType: typeof(T).Name,
            Payload: JsonSerializer.SerializeToUtf8Bytes(command),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "test-user",
            Extensions: null);

    [Fact]
    public async Task ProcessAsync_SendGreeting_ProducesGreetingSentEvent() {
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new SendGreeting()), currentState: null);

        Assert.True(result.IsSuccess);
        _ = Assert.Single(result.Events);
        _ = Assert.IsType<GreetingSent>(result.Events[0]);
    }

    [Fact]
    public async Task ProcessAsync_SendGreeting_NullState_ReturnsSuccess() {
        DomainResult result = await _aggregate.ProcessAsync(CreateCommand(new SendGreeting()), currentState: null);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsRejection);
        Assert.False(result.IsNoOp);
    }

    [Fact]
    public void GreetingState_Apply_GreetingSent_IncrementsMessageCount() {
        var state = new GreetingState();
        Assert.Equal(0, state.MessageCount);

        state.Apply(new GreetingSent());
        Assert.Equal(1, state.MessageCount);

        state.Apply(new GreetingSent());
        Assert.Equal(2, state.MessageCount);
    }
}
