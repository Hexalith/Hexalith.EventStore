
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Greeting.Commands;
using Hexalith.EventStore.Sample.Greeting.Events;
using Hexalith.EventStore.Sample.Greeting.State;

namespace Hexalith.EventStore.Sample.Greeting;

/// <summary>Minimal domain demonstrating multi-domain registration. See CounterAggregate for full pattern repertoire (rejection, no-op, tombstoning).</summary>
public sealed class GreetingAggregate : EventStoreAggregate<GreetingState> {
    // 256 bytes conservatively covers both fixed scalar-only state objects and their overhead.
    private static readonly DetachedStateCapture<GreetingState> _snapshotCapture = new(256,
        static (state, cancellationToken) => { cancellationToken.ThrowIfCancellationRequested(); return state.DetachedCopy(); });

    /// <inheritdoc/>
    protected override DetachedStateCapture<GreetingState> SnapshotCapture => _snapshotCapture;

    public static DomainResult Handle(SendGreeting command, GreetingState? state)
        => DomainResult.Success(new IEventPayload[] { new GreetingSent() });
}
