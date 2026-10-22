using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Sample.Greeting;
using Hexalith.EventStore.Sample.Greeting.Commands;
using Hexalith.EventStore.Sample.Greeting.Events;
using Hexalith.EventStore.Sample.Greeting.State;

namespace Hexalith.EventStore.Sample.Tests;

public sealed class DetachedSnapshotTests
{
    [Theory]
    [InlineData(int.MaxValue, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(17, true)]
    public async Task CounterActualAggregatePreservesSourceScalarsAndTermination(int count, bool terminated)
    {
        var snapshot = new CounterState();
        typeof(CounterState).GetProperty(nameof(CounterState.Count))!.SetValue(snapshot, count);
        if (terminated) { snapshot.Apply(new CounterClosed()); }
        CounterState detached = Copy(snapshot);
        Assert.NotSame(snapshot, detached);
        Assert.Equal(count, detached.Count);
        Assert.Equal(terminated, detached.IsTerminated);
        var current = new DomainServiceCurrentState(snapshot, [Event(new CounterIncremented(), "counter")], 1, 2);
        var result = await new CounterAggregate().ProcessAsync(Command(new IncrementCounter(), "counter"), current);
        Assert.Equal(count, snapshot.Count);
        Assert.Equal(terminated, snapshot.IsTerminated);
        Assert.Equal(terminated, result.IsRejection);
        Assert.Equal(!terminated, result.IsSuccess);
        Assert.IsType<CounterState>(snapshot);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public async Task GreetingActualAggregatePreservesSourceScalarAtIntegerExtremes(int count)
    {
        var snapshot = new GreetingState();
        typeof(GreetingState).GetProperty(nameof(GreetingState.MessageCount))!.SetValue(snapshot, count);
        GreetingState detached = Copy(snapshot);
        Assert.NotSame(snapshot, detached);
        Assert.Equal(count, detached.MessageCount);
        var current = new DomainServiceCurrentState(snapshot, [Event(new GreetingSent(), "greeting")], 1, 2);
        var result = await new GreetingAggregate().ProcessAsync(Command(new SendGreeting(), "greeting"), current);
        Assert.True(result.IsSuccess);
        Assert.Equal(count, snapshot.MessageCount);
    }

    [Fact]
    public async Task CounterPreservesCopiedZeroForActualHandleDecision()
    {
        var snapshot = new CounterState();
        snapshot.Apply(new CounterIncremented());
        var current = new DomainServiceCurrentState(snapshot, [Event(new CounterDecremented(), "counter")], 1, 2);
        var result = await new CounterAggregate().ProcessAsync(Command(new DecrementCounter(), "counter"), current);
        Assert.True(result.IsRejection);
        Assert.IsType<CounterCannotGoNegative>(Assert.Single(result.Events));
        Assert.Equal(1, snapshot.Count);
    }

    private static CommandEnvelope Command<T>(T payload, string domain) => new("command", "tenant", domain, "aggregate", typeof(T).Name,
        JsonSerializer.SerializeToUtf8Bytes(payload), "correlation", null, "user", null);
    private static EventEnvelope Event<T>(T payload, string domain) => new(new EventMetadata("message", "aggregate", domain, "tenant", domain, 2, 2,
        DateTimeOffset.UnixEpoch, "correlation", "cause", "user", "1", typeof(T).FullName!, 1, "json"), JsonSerializer.SerializeToUtf8Bytes(payload), null);
    private static T Copy<T>(T state) where T : class
        => (T)typeof(T).GetMethod("DetachedCopy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, null)!;
}
