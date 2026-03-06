using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Result of processing a command by an aggregate actor.
/// </summary>
/// <param name="Accepted">Whether the command was accepted for processing.</param>
/// <param name="ErrorMessage">Optional error message if the command was rejected.</param>
/// <param name="CorrelationId">The correlation identifier from the processed command.</param>
/// <param name="EventCount">The number of events persisted (0 for rejections and no-ops).</param>
/// <param name="ResultPayload">Optional serialized payload for enriched results (e.g., composite operation summaries).</param>
[DataContract]
public record CommandProcessingResult(
    [property: DataMember] bool Accepted,
    [property: DataMember] string? ErrorMessage = null,
    [property: DataMember] string? CorrelationId = null,
    [property: DataMember] int EventCount = 0,
    [property: DataMember] string? ResultPayload = null);
