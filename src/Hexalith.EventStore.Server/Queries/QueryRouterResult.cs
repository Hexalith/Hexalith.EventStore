
using System.Text.Json;

namespace Hexalith.EventStore.Server.Queries;
/// <summary>
/// Result of routing a query to a projection actor.
/// Allows the router to signal "not found" without throwing.
/// </summary>
public record QueryRouterResult(bool Success, JsonElement? Payload, bool NotFound, string? ErrorMessage = null, string? ProjectionType = null);
