using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Exact single-key provider instrumentation retained until actor and caller drain.</summary>
/// <param name="Fault">The selected wire fault.</param>
/// <param name="Name">Owned SQL trigger/function/sequence prefix.</param>
/// <param name="Request">The complete bound mutation.</param>
internal sealed record ProviderOneKeyInstrumentation(PostgresOneKeyFault Fault, string Name, ShardRequest Request);
