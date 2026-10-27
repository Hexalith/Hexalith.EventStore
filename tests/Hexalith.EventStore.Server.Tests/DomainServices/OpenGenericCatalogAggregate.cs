using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

[EventStoreDomain("counter")]
internal sealed class OpenGenericCatalogAggregate : EventStoreAggregate<CounterState>
{
    public static DomainResult Handle<T>(IncrementCounter command, CounterState state) => DomainResult.NoOp();
}
