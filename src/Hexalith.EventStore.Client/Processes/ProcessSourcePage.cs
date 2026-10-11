namespace Hexalith.EventStore.Client.Processes;

/// <summary>A keyset page from the authoritative source catalog.</summary>
/// <param name="Sources">Sources in strictly increasing canonical key order.</param>
/// <param name="NextKey">Last returned key if another page exists; null at the end.</param>
public sealed record ProcessSourcePage(IReadOnlyList<ProcessSource> Sources, string? NextKey);
