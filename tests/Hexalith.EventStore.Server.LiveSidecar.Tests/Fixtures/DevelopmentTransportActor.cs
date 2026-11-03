using Dapr.Actors.Runtime;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>A real, persisted actor used only by the owned transport qualification.</summary>
/// <param name="host">The actual Dapr actor host.</param>
public sealed class DevelopmentTransportActor(ActorHost host) : Actor(host), IDevelopmentTransportActor, IRemindable
{
    /// <inheritdoc/>
    public Task RecordAsync(string marker) => StateManager.SetStateAsync("marker", marker);
    /// <inheritdoc/>
    public Task<string> ReadAsync() => StateManager.GetStateAsync<string>("marker");
    /// <inheritdoc/>
    public async Task<bool> ReminderDeliveredAsync()
        => (await StateManager.TryGetStateAsync<bool>("reminder-delivered")).Value;
    /// <inheritdoc/>
    public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        => StateManager.SetStateAsync("reminder-delivered", true);
}
