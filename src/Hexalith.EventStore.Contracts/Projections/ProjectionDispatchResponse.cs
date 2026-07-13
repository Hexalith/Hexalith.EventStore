namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Versioned bounded response from named projection dispatch.
/// </summary>
/// <param name="Version">The wire contract version.</param>
/// <param name="Outcomes">The independently classified projection outcomes.</param>
public sealed record ProjectionDispatchResponse(
    int Version,
    IReadOnlyList<ProjectionDispatchOutcome> Outcomes);
