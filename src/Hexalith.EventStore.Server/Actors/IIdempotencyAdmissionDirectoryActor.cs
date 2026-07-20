using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes canonical digest-version authority for every logical key in one tenant.</summary>
public interface IIdempotencyAdmissionDirectoryActor : IActor
{
    /// <summary>Resolves or creates the canonical authority without creating an admission record.</summary>
    Task<IdempotencyAdmissionDirectoryResult> ResolveAsync(IdempotencyAdmissionDirectoryRequest request);

    /// <summary>Advances a promotion only after the phase's durable external action succeeded.</summary>
    Task<IdempotencyAdmissionDirectoryResult> AdvanceAsync(IdempotencyAdmissionDirectoryAdvanceRequest request);

    /// <summary>Removes one protected alias only during governed final tenant purge.</summary>
    Task PurgeAliasAsync(IdempotencyAdmissionDirectoryAlias alias);
}
