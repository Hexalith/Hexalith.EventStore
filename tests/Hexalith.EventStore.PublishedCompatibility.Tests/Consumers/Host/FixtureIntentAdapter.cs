#if FLOOR
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace PublishedConsumer;

/// <summary>Binds the disposable increment operation to the production idempotency intent registry.</summary>
public sealed class FixtureIntentAdapter : IIdempotencyIntentAdapter
{
    /// <inheritdoc/>
    public string CommandType => "Increment";
    /// <inheritdoc/>
    public string AdapterId => "p1r-counter";
    /// <inheritdoc/>
    public string OperationId => "increment";
    /// <inheritdoc/>
    public int DescriptorVersion => 1;
    /// <inheritdoc/>
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    /// <inheritdoc/>
    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
        => new($"{command.Tenant}/{command.Domain}/{command.AggregateId}", command.Payload, null, "p1r-v1", null, null);
}
#endif
