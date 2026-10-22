using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Builds bounded references to completed original replay work, without rereading source history.</summary>
internal static class ProjectionSourceGuardReplayCleanup
{
    internal static ProjectionDispatchRequest CreateRequest(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        IReadOnlyList<ProjectionOwnerProof> owners)
        => new(new(identity.TenantId, identity.Domain, identity.AggregateId, [])
        {
            RebuildSourceHeadSequence = owners[0].Selection.AppliedSequence,
            RebuildSourceHeadMessageId = owners[0].Selection.MessageId,
            RebuildOwnerGenerations = owners.ToDictionary(static owner => owner.Selection.ProjectionName,
                static owner => owner.Selection.OwnerGeneration, StringComparer.Ordinal),
            RebuildOwnerIdentities = owners.ToDictionary(static owner => owner.Selection.ProjectionName,
                static owner => owner.Selection.OwnerIdentity, StringComparer.Ordinal),
        }, [.. owners.Select(static owner => owner.Selection.ProjectionName)], owners[0].Selection.BatchId, catalog.CatalogFingerprint);
}
