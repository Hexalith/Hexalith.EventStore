#if P1R_CAPABILITIES
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

/// <summary>Server-owned semantic intent for this bounded, isolated qualification command.</summary>
internal sealed class FixtureIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc/>
    public string CommandType => "P1R.Counter.IncrementCounter";
    /// <inheritdoc/>
    public string AdapterId => "p1r-counter";
    /// <inheritdoc/>
    public string OperationId => "increment-counter";
    /// <inheritdoc/>
    public int DescriptorVersion => 1;
    /// <inheritdoc/>
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;
    /// <inheritdoc/>
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
        => new($"{command.Tenant}/{command.Domain}/{command.AggregateId}", command.Payload,
            SemanticOptions: null, PolicyVersion: "p1r-fixture-v1", DelegatedTaskScope: null, CredentialScope: null);
}
#endif
