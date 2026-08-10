using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Provides exact read-only legacy inspection and irreversible source redirect operations.</summary>
internal interface IIdempotencyLegacySourceActor : IActor
{
    /// <summary>Inspects only the exact supported message-keyed source state.</summary>
    Task<IdempotencyLegacySourceInspection> InspectLegacySourceAsync(IdempotencyLegacySourceRequest request);

    /// <summary>Persists and verifies a payload-free redirect while retaining original source evidence.</summary>
    Task<IdempotencyLegacySourceInspection> SetLegacySourceRedirectAsync(
        IdempotencyLegacySourceRedirectRequest request);
}
