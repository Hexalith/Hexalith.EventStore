using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

[EventStoreDomain("extra")]
public sealed class ExtraSdkCatalogProjection : EventStoreProjection<CounterState>;
