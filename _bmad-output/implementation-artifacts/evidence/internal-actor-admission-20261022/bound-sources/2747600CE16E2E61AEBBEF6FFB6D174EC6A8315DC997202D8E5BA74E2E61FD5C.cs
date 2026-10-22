namespace Hexalith.EventStore.Server.Actors;

/// <summary>Fixed-key discovery of terminal advisories, separate from publication recovery owners.</summary>
/// <param name="Entries">The outstanding immutable-intent owners.</param>
internal sealed record TerminalAdvisoryRepairIndex(IReadOnlyList<TerminalAdvisoryRepairEntry> Entries)
{
    /// <summary>The fixed actor-state key readable during activation.</summary>
    internal const string StateKey = "terminal-advisory-index";

    /// <summary>Gets an empty index.</summary>
    internal static TerminalAdvisoryRepairIndex Empty { get; } = new([]);

    /// <summary>Gets entries, preserving malformed evidence while tolerating a null collection.</summary>
    public IReadOnlyList<TerminalAdvisoryRepairEntry> Entries { get; init; } = Entries ?? [];
}
