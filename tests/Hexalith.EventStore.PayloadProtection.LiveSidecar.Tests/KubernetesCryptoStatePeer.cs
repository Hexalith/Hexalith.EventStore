using System.Diagnostics;
using Dapr.Client;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Owns the independent state-only sidecar and client for one test.</summary>
/// <param name="appId">The app ID passed to the sidecar.</param>
/// <param name="client">The client bound to the peer's gRPC port.</param>
/// <param name="sidecar">The owned sidecar process.</param>
internal sealed class KubernetesCryptoStatePeer(string appId, DaprClient client, Process sidecar) : IAsyncDisposable
{
    /// <summary>Gets the peer's expected app ID.</summary>
    internal string AppId { get; } = appId;

    /// <summary>Gets the client connected to the peer.</summary>
    internal DaprClient Client { get; } = client;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            Client.Dispose();
        }
        finally
        {
            await KubernetesCryptoFixture.StopSidecarProcessAsync(sidecar);
        }
    }
}
