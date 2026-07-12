namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Carries a tenant-scoped correlation resolution result.
/// </summary>
/// <param name="Outcome">The resolution outcome.</param>
/// <param name="MessageId">The sole live message identifier when resolved.</param>
public sealed record CommandCorrelationResolution(
    CommandCorrelationResolutionOutcome Outcome,
    string? MessageId = null);
