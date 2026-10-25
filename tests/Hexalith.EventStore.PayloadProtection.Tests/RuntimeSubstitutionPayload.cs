namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Serializes a marked type, then substitutes a different enrolled non-null runtime type.</summary>
/// <param name="canary">The per-test value whose disclosure is forbidden.</param>
internal sealed class RuntimeSubstitutionPayload(string canary)
{
    private int _reads;
    /// <summary>Gets a deliberately unstable runtime/provenance shape.</summary>
    public object Child => ++_reads == 1 ? new PolicyLifecyclePayload { Secret = canary } : new PartySnapshotState(canary, 1);
    /// <summary>Gets how often the serializer/discovery asked for the child.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Reads => _reads;
}
