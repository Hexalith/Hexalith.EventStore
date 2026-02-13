namespace Hexalith.EventStore.CommandApi.Models;

using System.Text.Json;

/// <summary>
/// API-facing request DTO for submitting commands.
/// Separate from <see cref="Contracts.Commands.CommandEnvelope"/> which is the internal domain type.
/// </summary>
public record SubmitCommandRequest(
    string Tenant,
    string Domain,
    string AggregateId,
    string CommandType,
    JsonElement Payload,
    Dictionary<string, string>? Extensions = null);
