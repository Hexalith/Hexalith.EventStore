namespace Hexalith.EventStore.Client.Projections;

/// <summary>Requests durable retry without parking when valid fold state exceeds a configured capacity.</summary>
/// <remarks>Accepted contents remain selected. A capacity/configuration correction or covering authoritative rebuild is required.</remarks>
public sealed class SharedProjectionCapacityExceededException : Exception
{
    /// <summary>Creates a support-safe capacity failure without retaining payload data.</summary>
    public SharedProjectionCapacityExceededException()
        : base("The shared projection fold exceeds its configured capacity; increase capacity or restore/rebuild the scope.")
    {
    }
}
