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
/// <param name="BackpressureExceeded">Whether the command was rejected due to per-aggregate backpressure (Story 4.3, FR67).</param>
/// <param name="BackpressurePendingCount">The pending command count observed when backpressure rejected the command.</param>
/// <param name="BackpressureThreshold">The configured pending command threshold for backpressure rejection.</param>
/// <param name="RejectionEventType">The stable domain rejection event type when rejected by domain policy.</param>
/// <param name="FailureReason">The stable deterministic failure classification when one exists.</param>
/// <param name="ResultPayloadWithheld">Whether public result-payload projection was durably withheld.</param>
[DataContract]
public record CommandProcessingResult(
    [property: DataMember] bool Accepted,
    [property: DataMember] string? ErrorMessage = null,
    [property: DataMember] string? CorrelationId = null,
    [property: DataMember] int EventCount = 0,
    [property: DataMember] string? ResultPayload = null,
    [property: DataMember] bool BackpressureExceeded = false,
    [property: DataMember] int? BackpressurePendingCount = null,
    [property: DataMember] int? BackpressureThreshold = null,
    [property: DataMember] string? RejectionEventType = null,
    [property: DataMember] string? FailureReason = null,
    [property: DataMember] bool ResultPayloadWithheld = false);
