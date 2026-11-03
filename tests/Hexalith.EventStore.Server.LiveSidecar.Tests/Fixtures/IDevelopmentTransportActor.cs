using Dapr.Actors;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>The disposable transport qualification actor contract.</summary>
public interface IDevelopmentTransportActor : IActor
{
    /// <summary>Persists the supplied marker through the real actor state manager.</summary>
    Task RecordAsync(string marker);
    /// <summary>Reads the actual persisted marker after activation or restart.</summary>
    Task<string> ReadAsync();
    /// <summary>Reads whether the owned scheduler delivered the reminder.</summary>
    Task<bool> ReminderDeliveredAsync();
}
