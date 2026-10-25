namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Returns a sensitive subtree during serialization and null during subsequent typed discovery.</summary>
/// <param name="canary">The per-test value whose disclosure is forbidden.</param>
internal sealed class ChangingGetterPayload(string canary)
{
    private int _reads;
    /// <summary>Gets a deliberately unstable typed value for the E1 regression.</summary>
    public PolicyLifecyclePayload? Child => Interlocked.Increment(ref _reads) == 1 ? new() { Secret = canary, } : null;
}
