using System.Net.Sockets;
using System.Text;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Two real loopback sockets connected through the tested proxy, with a minimal startup exchange.</summary>
internal sealed class PostgresProtocolTestPeer(TcpClient caller, TcpClient provider) : IDisposable
{
    /// <summary>Gets the frontend socket for half-close/corruption regressions.</summary>
    internal TcpClient Caller => caller;

    /// <summary>Gets the backend socket for half-close/corruption regressions.</summary>
    internal TcpClient Provider => provider;

    /// <summary>Connects a real session through the proxy.</summary>
    internal static async Task<PostgresProtocolTestPeer> ConnectAsync(PostgresFaultProxy proxy, TcpListener listener, CancellationToken token)
    {
        var caller = new TcpClient();
        await caller.ConnectAsync("127.0.0.1", proxy.Port, token);
        TcpClient provider = await listener.AcceptTcpClientAsync(token);
        var peer = new PostgresProtocolTestPeer(caller, provider);
        await new PostgresFrame(0, [0, 3, 0, 0, 0]).WriteAsync(caller.GetStream(), token);
        _ = await PostgresFrame.ReadAsync(provider.GetStream(), true, token)
            ?? throw new InvalidDataException("Missing test startup.");
        return peer;
    }

    /// <summary>Writes a framed frontend message.</summary>
    internal Task SendAsync(char type, string text, CancellationToken token)
        => new PostgresFrame((byte)type, Encoding.UTF8.GetBytes(text)).WriteAsync(caller.GetStream(), token);

    /// <summary>Writes a framed backend message.</summary>
    internal Task ReplyAsync(char type, string text, CancellationToken token)
        => new PostgresFrame((byte)type, Encoding.UTF8.GetBytes(text)).WriteAsync(provider.GetStream(), token);

    /// <summary>Reads a complete message at the backend.</summary>
    internal Task<PostgresFrame?> ReadProviderAsync(CancellationToken token) => PostgresFrame.ReadAsync(provider.GetStream(), false, token);

    /// <summary>Reads a complete message at the caller.</summary>
    internal Task<PostgresFrame?> ReadCallerAsync(CancellationToken token) => PostgresFrame.ReadAsync(caller.GetStream(), false, token);

    /// <summary>Disposes both test-owned sockets.</summary>
    public void Dispose()
    {
        caller.Dispose();
        provider.Dispose();
    }
}
