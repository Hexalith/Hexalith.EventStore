using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Commands;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

internal sealed class LiveIncrementCounterIdempotencyIntentAdapter : IDeclaredIdempotencyIntentAdapter
{
    internal static CommandIntentDeclaration Policy { get; } = new(
        new("live-counter-increment", "command", "counter", "IncrementCounter", null, "v1", 1,
            "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(IncrementCounter).FullName!, "exact", ["IncrementCounter"], "domain-service", null, [])),
        "live-counter", "increment-counter", "live-test-v2",
        new("live-counter-payload-v2", 1, [new("target", "string"), new("payload", "string")]),
        IdempotencyReplayRetentionTier.Mutation, false);

    internal static string DeclarationDigest => LiveAdmissionCatalogBinding.Digest(
        RouteCatalogCodec.ComputeDeclarationDigest(Policy.Route) + RouteCatalogCodec.ComputeDescriptorDigest(Policy.DescriptorSchema));

    public CommandIntentDeclaration Declaration => Policy;

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
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(command.Payload) }),
            SemanticOptions: null,
            PolicyVersion: Policy.PolicyVersion,
            DelegatedTaskScope: null,
            CredentialScope: null);
    }
}
