namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Returns a sensitive subtree during serialization and null during subsequent typed discovery.</summary>
internal sealed class ChangingGetterPayload
{
    private int _reads;
    /// <summary>Gets a deliberately unstable typed value for the E1 regression.</summary>
    public PolicyLifecyclePayload? Child => Interlocked.Increment(ref _reads) == 1 ? new() { Secret = "changing-getter-private-canary" } : null;
}
