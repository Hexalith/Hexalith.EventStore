using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Describes the durable outcome of one named projection handler.
/// </summary>
/// <param name="ProjectionType">The registered projection route.</param>
/// <param name="Status">The closed durable outcome classification.</param>
/// <param name="State">Optional state for compatibility with the legacy projection-actor path.</param>
/// <param name="ReasonCode">An optional bounded support-safe reason code.</param>
public sealed record ProjectionDispatchOutcome(
    string ProjectionType,
    [property: JsonRequired] ProjectionDispatchStatus Status,
    JsonElement? State,
    string? ReasonCode);
