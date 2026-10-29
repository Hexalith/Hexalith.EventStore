using Dapr.Actors.Runtime;
using Dapr.Client;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Records activation and persists an isolated generic-state marker for HTTP boundary controls.</summary>
public sealed class HttpHostSecurityLiveActor(ActorHost host, DaprClient client) : Actor(host), IHttpHostSecurityLiveActor
{
    private static int _activations;

    /// <summary>Gets the number of activations in this process.</summary>
    internal static int Activations => Volatile.Read(ref _activations);

    /// <inheritdoc />
    public Task RecordAsync(string marker) => client.SaveStateAsync("statestore", "story514-actor-" + Id, marker);

    /// <inheritdoc />
    protected override Task OnActivateAsync()
    {
        Interlocked.Increment(ref _activations);
        return Task.CompletedTask;
    }
}
