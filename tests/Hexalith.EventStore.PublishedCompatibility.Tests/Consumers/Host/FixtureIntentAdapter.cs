#if FLOOR
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace PublishedConsumer;

/// <summary>Binds the disposable increment operation to the production idempotency intent registry.</summary>
public sealed class FixtureIntentAdapter : IIdempotencyIntentAdapter
{
    public string CommandType => "Increment";
    public string AdapterId => "p1r-counter";
    public string OperationId => "increment";
    public int DescriptorVersion => 1;
    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
        => new($"{command.Tenant}/{command.Domain}/{command.AggregateId}", command.Payload, null, "p1r-v1", null, null);
}
#endif
