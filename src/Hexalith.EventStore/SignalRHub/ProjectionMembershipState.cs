namespace Hexalith.EventStore.SignalRHub;

/// <summary>Serializes bounded full-membership snapshots for one physical connection.</summary>
internal sealed class ProjectionMembershipState
{
    /// <summary>Gets the transition gate.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>Gets every possibly admitted group until backend removal is acknowledged.</summary>
    public HashSet<string> PossibleGroups { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the last fully applied generation.</summary>
    public long Generation { get; set; } = -1;
}
