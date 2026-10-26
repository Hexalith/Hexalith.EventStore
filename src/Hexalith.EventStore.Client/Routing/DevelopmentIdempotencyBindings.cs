using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Client.Routing;

/// <summary>Approved value-free, unactivated Development key references. No key availability is asserted.</summary>
public static class DevelopmentIdempotencyBindings
{
    /// <summary>Joins an explicit owner policy to the approved future Development secret references.</summary>
    public static IdempotencyCatalogEntry Bind(CommandIntentDeclaration declaration, long generation)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        var entry = new IdempotencyCatalogEntry(declaration.Route.RouteId, generation, declaration.Route.Domain,
            declaration.Route.MessageType!, declaration.AdapterId, declaration.OperationId, declaration.PolicyVersion,
            declaration.DescriptorSchema, RouteCatalogCodec.ComputeDescriptorDigest(declaration.DescriptorSchema),
            declaration.RetentionTier switch
            {
                Contracts.Commands.IdempotencyReplayRetentionTier.Mutation => "mutation",
                Contracts.Commands.IdempotencyReplayRetentionTier.Commit => "commit",
                _ => throw new ArgumentException("Unsupported explicit retention tier.", nameof(declaration)),
            },
            "development-v1", ["development-v1"], "secretstore", "eventstore-idempotency-ring", "development-v1",
            [new("development-v1", "development-v1")], "eventstore", string.Empty);
        return entry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry) };
    }
}
