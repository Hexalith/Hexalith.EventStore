using Dapr.Actors;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Isolated actor protocol used only to observe HTTP admission and durable effects.</summary>
public interface IHttpHostSecurityLiveActor : IActor
{
    /// <summary>Persists the supplied positive-control marker through the owned sidecar.</summary>
    /// <param name="marker">The test marker.</param>
    /// <returns>The persistence task.</returns>
    Task RecordAsync(string marker);
}
