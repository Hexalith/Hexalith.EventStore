namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Isolates callback mutation probes for the legacy replay owner.</summary>
internal sealed class LegacyReplayMutationScope : IDisposable
{
    private static readonly AsyncLocal<LegacyReplayMutationScope?> _current = new();
    private readonly LegacyReplayMutationScope? _prior = _current.Value;

    /// <summary>Installs one isolated probe.</summary>
    internal LegacyReplayMutationScope() => _current.Value = this;

    /// <summary>Gets the current probe.</summary>
    internal static LegacyReplayMutationScope Current => _current.Value!;

    /// <summary>Gets or sets a callback run when the working state is created.</summary>
    internal Action? OnConstruct { get; set; }

    /// <summary>Gets or sets a callback run after the first Apply.</summary>
    internal Action? OnFirstApply { get; set; }

    /// <summary>Gets the applied payload values.</summary>
    internal List<int> Applied { get; } = [];

    /// <summary>Gets or sets the number of state instances created.</summary>
    internal int Constructed { get; set; }

    /// <inheritdoc/>
    public void Dispose() => _current.Value = _prior;
}
