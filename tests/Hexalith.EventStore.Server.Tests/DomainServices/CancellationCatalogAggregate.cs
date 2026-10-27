using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

[EventStoreDomain("counter")]
internal sealed class CancellationCatalogAggregate : EventStoreAggregate<CounterState>
{
    public CancellationToken ObservedToken { get; private set; }
    public string? ObservedWireType { get; private set; }

    public static DomainResult Handle(IncrementCounter command, CounterState state) => throw new InvalidOperationException("The legacy overload must not be chosen.");

    public Task<DomainResult> Handle(IncrementCounter command, CounterState state, CommandEnvelope envelope, CancellationToken cancellationToken)
    {
        ObservedToken = cancellationToken;
        ObservedWireType = envelope.CommandType;
        return Task.FromResult(DomainResult.NoOp());
    }
}
