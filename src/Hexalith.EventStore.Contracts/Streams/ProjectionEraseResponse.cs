namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// EventStore-side projection erase response. Discloses only the classified outcome, an optional
/// support-safe reason code, and the per-target outcome classifications — never raw target values,
/// store names, physical keys, or ETags.
/// </summary>
/// <param name="Outcome">The classified erase outcome (the coordinator outcome kind name).</param>
/// <param name="ReasonCode">An optional, support-safe reason code; never discloses target values.</param>
/// <param name="Targets">The per-target outcome classifications recorded during the operation.</param>
public sealed record ProjectionEraseResponse(
    string Outcome,
    string? ReasonCode,
    IReadOnlyList<ProjectionEraseTargetOutcomeDto> Targets);
