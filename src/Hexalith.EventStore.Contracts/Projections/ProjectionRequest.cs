
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

    /// <summary>
    /// Gets the owner stream head observed by EventStore while preparing a mapped full replay.
    /// This transport value is admitted only on the protected rebuild route; handlers must still
    /// compare it with the supplied contiguous owner prefix and selected read model.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RebuildSourceHeadSequence { get; init; }

    /// <summary>Gets the frozen persisted head identity for an explicitly guarded staged replay.</summary>
    [JsonIgnore]
    internal string? RebuildSourceHeadMessageId { get; init; }

    /// <summary>Gets the admitted selected generations that a guarded immutable replay manifest must preserve.</summary>
    [JsonIgnore]
    internal IReadOnlyDictionary<string, string>? RebuildOwnerGenerations { get; init; }

    /// <summary>Gets the exact admitted owner identities paired with guarded replay generations.</summary>
    [JsonIgnore]
    internal IReadOnlyDictionary<string, string>? RebuildOwnerIdentities { get; init; }

    /// <summary>Gets the private compact attestation of the complete admitted owner bindings.</summary>
    [JsonIgnore]
    internal string? RebuildOwnerBindingFingerprint { get; init; }

    /// <summary>
    /// Gets the aggregate actor's completed-erasure observation for a protected, empty full replay.
    /// The mapped rebuild route accepts this only with an empty prefix and a zero source head.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RebuildSourceErased { get; init; }
}
