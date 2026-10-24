namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Provides many bounded values so oversized production must stop before the serializer finishes.</summary>
internal sealed class OversizedPolicyPayload
{
    /// <summary>Gets a long enumerable with observable production progress.</summary>
    public IEnumerable<string> Values => Produce();
    /// <summary>Gets the observed produced-value count.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Produced { get; private set; }
    private IEnumerable<string> Produce()
    {
        string value = new('q', 1024);
        for (int i = 0; i < 50000; i++) { Produced++; yield return value; }
    }
}
