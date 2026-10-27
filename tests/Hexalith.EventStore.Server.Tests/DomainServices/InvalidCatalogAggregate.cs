using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

[EventStoreDomain("counter")]
internal sealed class InvalidCatalogAggregate : EventStoreAggregate<CounterState>
{
    public static DomainResult Handle() => DomainResult.NoOp();
    public static DomainResult Handle(IncrementCounter command, object state) => DomainResult.NoOp();
    public static string Handle(DecrementCounter command, CounterState state) => "invalid";
    public static DomainResult Handle(ResetCounter command, CounterState state, string invalidEnvelope) => DomainResult.NoOp();
    public static DomainResult Handle(CloseCounter command, CounterState state, string invalidEnvelope, CancellationToken cancellationToken) => DomainResult.NoOp();
}
