using System.Text.Json;

namespace Hexalith.EventStore.Models;

/// <summary>
/// Compatibility wrapper for the public command gateway response contract.
/// </summary>
public record SubmitCommandResponse(string CorrelationId, JsonElement? ResultPayload = null, string? MessageId = null)
    : Hexalith.EventStore.Contracts.Commands.SubmitCommandResponse(CorrelationId, ResultPayload, MessageId);
