using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>
/// Isolates provider network faults to the qualification sidecars without stopping the shared Redis service.
/// It forwards opaque bytes on loopback only and never interprets or retains the provider protocol.
/// </summary>
public sealed class SignalRDaprNetworkFaultProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<long, (TcpClient Incoming, TcpClient Outgoing)> _connections = new();
    private readonly ConcurrentDictionary<long, Task> _forwarders = new();
    private readonly Task _accepting;
    private long _nextId;
    private long _rejected;
    private long _disconnected;
    private int _blocked;

    /// <summary>Starts a transparent test-only route to the existing loopback Redis service.</summary>
    public SignalRDaprNetworkFaultProxy()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accepting = AcceptAsync();
    }

    /// <summary>Gets the isolated loopback port used in the disposable Dapr component files.</summary>
    public int Port { get; }

    /// <summary>Gets the number of connections actively forwarded to the existing provider.</summary>
    public int ActiveConnections => _connections.Count;

    /// <summary>Gets the number of new connections refused during the network fault.</summary>
    public long RejectedConnections => Interlocked.Read(ref _rejected);

    /// <summary>Gets the number of established connections severed by the fault.</summary>
    public long DisconnectedConnections => Interlocked.Read(ref _disconnected);

    /// <summary>Severs this fixture's active provider connections and refuses reconnects.</summary>
    public async Task BlockAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _blocked, 1);
        foreach ((TcpClient incoming, TcpClient outgoing) in _connections.Values)
        {
            incoming.Dispose();
            outgoing.Dispose();
            Interlocked.Increment(ref _disconnected);
        }

        await Task.WhenAll(_forwarders.Values).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Restores this fixture's provider route; it does not restart either application or sidecar.</summary>
    public void Restore() => Interlocked.Exchange(ref _blocked, 0);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        foreach ((TcpClient incoming, TcpClient outgoing) in _connections.Values)
        {
            incoming.Dispose();
            outgoing.Dispose();
        }

        await _accepting.ConfigureAwait(false);
        await Task.WhenAll(_forwarders.Values).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        _stopping.Dispose();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                TcpClient incoming = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
                if (Volatile.Read(ref _blocked) != 0)
                {
                    Interlocked.Increment(ref _rejected);
                    incoming.Dispose();
                    continue;
                }

                long id = Interlocked.Increment(ref _nextId);
                _forwarders[id] = ForwardAsync(id, incoming);
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (SocketException) when (_stopping.IsCancellationRequested)
        {
        }
    }

    private async Task ForwardAsync(long id, TcpClient incoming)
    {
        using (incoming)
        using (var outgoing = new TcpClient())
        {
            _connections[id] = (incoming, outgoing);
            try
            {
                // Recheck after registration so a concurrent fault cannot leave a live bypass.
                if (Volatile.Read(ref _blocked) != 0 || _stopping.IsCancellationRequested)
                {
                    return;
                }

                await outgoing.ConnectAsync(IPAddress.Loopback, 6379, _stopping.Token).ConfigureAwait(false);
                NetworkStream source = incoming.GetStream();
                NetworkStream target = outgoing.GetStream();
                Task upload = source.CopyToAsync(target, _stopping.Token);
                Task download = target.CopyToAsync(source, _stopping.Token);
                await Task.WhenAny(upload, download).ConfigureAwait(false);
                incoming.Dispose();
                outgoing.Dispose();
                await Task.WhenAll(upload, download).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // Closing either end is the deliberately injected network fault.
            }
            finally
            {
                _connections.TryRemove(id, out _);
            }
        }
    }
}
