namespace Hexalith.EventStore.Client.Processes;

/// <summary>A keyset page from the authoritative source catalog.</summary>
/// <param name="Sources">Sources in strictly increasing canonical key order.</param>
/// <param name="NextKey">Last returned key if another page exists; null at the end.</param>
public sealed record ProcessSourcePage(IReadOnlyList<ProcessSource> Sources, string? NextKey)
{
    /// <summary>Whether some authoritative scopes could not be read. Healthy entries still advance; the SDK retains degraded discovery and starts another sweep.</summary>
    public bool IsIncomplete { get; init; }
}
