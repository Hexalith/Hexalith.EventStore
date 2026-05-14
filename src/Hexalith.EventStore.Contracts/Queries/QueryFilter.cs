using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public filter expression placeholder for gateway query policy.
/// </summary>
/// <param name="Field">The public field identifier to filter.</param>
/// <param name="Operator">The public filter operator.</param>
/// <param name="Value">The filter value. Gateways must not echo this value in validation or ProblemDetails output.</param>
public sealed record QueryFilter(string Field, string Operator, JsonElement Value);
