namespace Hexalith.EventStore.ServiceDefaults.Tests.GeneratedRest;

/// <summary>Retains actual handler effects so refused requests must leave persisted fixture state unchanged.</summary>
public sealed class GeneratedRestProbeState
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _tenants = new();
    /// <summary>The persisted successful request scopes.</summary>
    public IReadOnlyList<string> Tenants => _tenants.ToArray();
    /// <summary>Records a real admitted action effect.</summary>
    public void Record(string tenant) => _tenants.Enqueue(tenant);
}
