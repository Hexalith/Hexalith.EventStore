using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>One exact-key trigger/sequence retained until its request has fully drained.</summary>
/// <param name="Fault">The selected wire gate and observed counters.</param>
/// <param name="Name">The unique SQL object prefix.</param>
/// <param name="OperationId">The mutating operation's observation identity.</param>
/// <param name="Request">The full exact submitted mutation.</param>
internal sealed record ProviderFaultInstrumentation(PostgresCommitFault Fault, string Name, string OperationId, ShardRequest Request);
