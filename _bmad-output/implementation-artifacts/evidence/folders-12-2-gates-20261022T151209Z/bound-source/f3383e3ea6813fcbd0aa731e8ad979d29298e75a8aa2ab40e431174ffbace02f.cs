using Hexalith.EventStore.Client.Aggregates;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Uses an Apply event from the separate fixture assembly.</summary>
internal sealed class ExternalEventAggregate : EventStoreAggregate<ExternalEventState>;
