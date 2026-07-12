namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// The support-safe outcome classification for a single projection erase target. Exposes only the
/// canonical, value-free target key and its outcome classification — never the erased value itself.
/// </summary>
/// <param name="TargetKey">The canonical, value-free key of the target.</param>
/// <param name="Outcome">The per-target outcome classification (e.g. <c>Complete</c>, <c>Conflict</c>, <c>Incomplete</c>, <c>Unknown</c>).</param>
public sealed record ProjectionEraseTargetOutcomeDto(string TargetKey, string Outcome);
