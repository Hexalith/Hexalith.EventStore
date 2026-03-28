
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Response from a domain service's /project endpoint.
/// State is opaque — EventStore stores and serves it without interpreting the schema.
/// </summary>
/// <param name="ProjectionType">The projection type name (e.g., "counter-summary").</param>
/// <param name="State">The opaque projection state as a JSON element.
/// Callers must use <see cref="JsonElement.Clone"/> or keep the originating
/// <see cref="JsonDocument"/> alive — a disposed document invalidates this element.</param>
public record ProjectionResponse(
    string ProjectionType,
    JsonElement State);
