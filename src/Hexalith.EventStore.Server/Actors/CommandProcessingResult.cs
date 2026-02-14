namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Result of processing a command by an aggregate actor.
/// </summary>
/// <param name="Accepted">Whether the command was accepted for processing.</param>
/// <param name="ErrorMessage">Optional error message if the command was rejected.</param>
/// <param name="CorrelationId">The correlation identifier from the processed command.</param>
public record CommandProcessingResult(bool Accepted, string? ErrorMessage = null, string? CorrelationId = null);
