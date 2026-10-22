using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

internal sealed class LiveIncrementCounterIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    public string CommandType => "IncrementCounter";

    public string AdapterId => "live-counter";

    public string OperationId => "increment-counter";

    public int DescriptorVersion => 1;

    public IdempotencyReplayRetentionTier RetentionTier => IdempotencyReplayRetentionTier.Mutation;

    public IdempotencyCanonicalIntent CreateIntent(IdempotencyIntentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new IdempotencyCanonicalIntent(
            $"{command.Tenant}/{command.Domain}/{command.AggregateId}",
            command.Payload,
            SemanticOptions: null,
            PolicyVersion: "live-test-v1",
            DelegatedTaskScope: null,
            CredentialScope: null);
    }
}
