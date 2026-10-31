namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>A test-only second state key forcing the provider's multi-operation transaction path.</summary>
/// <param name="OperationId">The actual mutating operation's observation identity.</param>
/// <param name="State">The complete authority staged in the same save.</param>
public sealed record ShardCommitWitness(string OperationId, ShardState State);
