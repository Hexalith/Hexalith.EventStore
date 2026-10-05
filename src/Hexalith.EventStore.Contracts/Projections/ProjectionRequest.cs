
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Request sent to a domain service's /project endpoint with per-aggregate granularity.
/// Contains the aggregate identity and the events to project.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="Events">The events to project, in sequence order.</param>
public record ProjectionRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    ProjectionEventDto[] Events) {
    /// <summary>Gets untrusted effective-view transport hints that require verification before handler dispatch.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VerifiedEffectiveEventView[]? VerifiedEffectiveEvents { get; init; }

    /// <summary>Gets the untrusted event-evolution proof hint that requires route and complete-prefix verification.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? EventEvolutionProof { get; init; }
}
