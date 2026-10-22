
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Sample.Counter;
/// <summary>
/// Counter aggregate using the fluent EventStoreAggregate API.
/// </summary>
public sealed class CounterAggregate : EventStoreAggregate<CounterState> {
    // 256 bytes conservatively covers both fixed scalar-only state objects and their overhead.
    private static readonly DetachedStateCapture<CounterState> _snapshotCapture = new(256,
        static (state, cancellationToken) => { cancellationToken.ThrowIfCancellationRequested(); return state.DetachedCopy(); });

    /// <inheritdoc/>
    protected override DetachedStateCapture<CounterState> SnapshotCapture => _snapshotCapture;

    public static DomainResult Handle(IncrementCounter command, CounterState? state)
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

    public static DomainResult Handle(DecrementCounter command, CounterState? state) {
        if ((state?.Count ?? 0) == 0) {
            return DomainResult.Rejection(new IRejectionEvent[] { new CounterCannotGoNegative() });
        }

        return DomainResult.Success(new IEventPayload[] { new CounterDecremented() });
    }

    public static DomainResult Handle(ResetCounter command, CounterState? state) {
        if ((state?.Count ?? 0) == 0) {
            return DomainResult.NoOp();
        }

        return DomainResult.Success(new IEventPayload[] { new CounterReset() });
    }

    public static DomainResult Handle(CloseCounter command, CounterState? state)
        => DomainResult.Success(new IEventPayload[] { new CounterClosed() });
}
