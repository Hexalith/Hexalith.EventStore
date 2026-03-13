using Hexalith.EventStore.SignalR;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Hosted service that starts the SignalR client on application startup,
/// ensuring the hub connection is established before components render.
/// </summary>
public sealed class SignalRClientStartup(EventStoreSignalRClient signalRClient) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => signalRClient.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
