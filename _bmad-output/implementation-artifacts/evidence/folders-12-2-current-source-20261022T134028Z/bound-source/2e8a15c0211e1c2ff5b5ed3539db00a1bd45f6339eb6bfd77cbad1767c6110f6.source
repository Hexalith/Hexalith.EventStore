using Hexalith.EventStore.SignalR;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Hosted service that starts the SignalR client on application startup,
/// ensuring the hub connection is established before components render.
/// </summary>
public sealed class SignalRClientStartup(EventStoreSignalRClient signalRClient, ILogger<SignalRClientStartup> logger) : IHostedService {
    public async Task StartAsync(CancellationToken cancellationToken) {
        try {
            logger.LogInformation("SignalR client starting...");
            await signalRClient.StartAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("SignalR client connected successfully.");
        }
        catch (Exception ex) {
            logger.LogError(ex, "SignalR client failed to connect. Real-time notifications will be unavailable.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
