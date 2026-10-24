namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Serializes a marked type, then substitutes a different enrolled non-null runtime type.</summary>
internal sealed class RuntimeSubstitutionPayload
{
    private int _reads;
    /// <summary>Gets a deliberately unstable runtime/provenance shape.</summary>
    public object Child => ++_reads == 1 ? new PolicyLifecyclePayload { Secret = "substitution-private-canary" } : new PartySnapshotState("substitution-private-canary", 1);
    /// <summary>Gets how often the serializer/discovery asked for the child.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Reads => _reads;
}
