namespace Hexalith.EventStore.Server.Actors;

/// <summary>Actor-owned continuation for bounded, fair recovery discovery across activations.</summary>
/// <param name="NextOffset">The next index position to inspect, normalized against the current owner set.</param>
internal sealed record RecoveryScanProgress(int NextOffset)
{
    /// <summary>The publication discovery continuation key.</summary>
    internal const string PublicationKey = "publication-recovery-scan";

    /// <summary>The independent advisory discovery continuation key.</summary>
    internal const string AdvisoryKey = "terminal-advisory-recovery-scan";
}
