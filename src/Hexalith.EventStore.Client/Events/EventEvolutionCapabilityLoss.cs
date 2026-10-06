namespace Hexalith.EventStore.Client.Events;

/// <summary>Irreversibly records observed trusted-code policy loss for a shared execution scope.</summary>
/// <remarks>
/// A host must share one instance across its catalogs and qualified loader observers. The absence
/// of an observation establishes no readiness, artifact binding or before-effect protection.
/// This local control cannot undo effects or reconcile already committed Dapr state.
/// </remarks>
internal sealed class EventEvolutionCapabilityLoss
{
    private int _observed;

    /// <summary>Gets the process-lifetime default shared by catalogs in the admitted Client load context.</summary>
    /// <remarks>Additional Client load contexts require the host's qualified observation and sharing boundary.</remarks>
    internal static EventEvolutionCapabilityLoss Process { get; } = new();

    /// <summary>Records a policy violation without retaining executable paths or sensitive diagnostic data.</summary>
    internal void ObserveViolation() => Interlocked.Exchange(ref _observed, 1);

    /// <summary>Refuses subsequent callbacks and uncommitted results after an observed violation.</summary>
    internal void RequireNoObservedLoss()
    {
        if (Volatile.Read(ref _observed) != 0)
        {
            throw new InvalidOperationException("CapabilityMismatch: event evolution capability was lost after a trusted-code policy violation.");
        }
    }
}
