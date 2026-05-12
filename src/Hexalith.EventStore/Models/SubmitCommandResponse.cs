namespace Hexalith.EventStore.Models;

/// <summary>
/// Compatibility wrapper for the public command gateway response contract.
/// </summary>
public record SubmitCommandResponse(string CorrelationId)
    : Hexalith.EventStore.Contracts.Commands.SubmitCommandResponse(CorrelationId);
