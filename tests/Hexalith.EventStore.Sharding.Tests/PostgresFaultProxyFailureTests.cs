using System.Net;
using System.Net.Sockets;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Unexpected real-socket failures remain visible while every accepted task still drains.</summary>
public sealed class PostgresFaultProxyFailureTests
{
    /// <summary>Ordinary clean sidecar EOF drains its peer without poisoning a replacement session.</summary>
    [Fact]
    public async Task CleanEofDrainsNormallyWhileListenerServesReplacementAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            await using var proxy = new PostgresFaultProxy(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None);
            proxy.Start();
            using (PostgresProtocolTestPeer old = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token))
            {
                old.Caller.Client.Shutdown(SocketShutdown.Send);
                while (proxy.ActiveConnections != 0) await Task.Delay(10, deadline.Token);
            }
            using PostgresProtocolTestPeer replacement = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await replacement.SendAsync('Q', "COMMIT\0", deadline.Token);
            (await replacement.ReadProviderAsync(deadline.Token))!.Field().ShouldBe("COMMIT");
            await replacement.ReplyAsync('C', "COMMIT\0", deadline.Token);
            await replacement.ReplyAsync('Z', "I", deadline.Token);
            (await replacement.ReadCallerAsync(deadline.Token))!.Field().ShouldBe("COMMIT");
            (await replacement.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            await proxy.DisposeAsync();
            proxy.AcceptedConnections.ShouldBe(2);
            proxy.DrainedConnections.ShouldBe(2);
            proxy.CommitCompletions.ShouldBe(1);
            proxy.Stopped.ShouldBeTrue();
        }
        finally { listener.Stop(); }
    }

    /// <summary>A truncated frontend/backend frame fails teardown instead of being an ordinary disconnect.</summary>
    /// <param name="backend">Whether the provider truncates its frame.</param>
    /// <param name="afterRecovery">Whether an earlier owned restart allowance must have ended.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UnexpectedTruncatedFrameIsRetainedAndFailsAsync(bool backend, bool afterRecovery)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var proxy = new PostgresFaultProxy(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None);
        try
        {
            proxy.Start();
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            if (afterRecovery)
            {
                proxy.BeginProviderRestart();
                proxy.EndProviderRestart();
            }
            TcpClient truncated = backend ? peer.Provider : peer.Caller;
            await truncated.GetStream().WriteAsync(new byte[] { (byte)'Q', 0, 0 }, deadline.Token);
            truncated.Client.Shutdown(SocketShutdown.Send);
            while (proxy.ActiveConnections != 0) await Task.Delay(10, deadline.Token);
            AggregateException failure = await Should.ThrowAsync<AggregateException>(() => proxy.DisposeAsync().AsTask());
            failure.InnerExceptions.OfType<InvalidDataException>().Single().InnerException.ShouldBeOfType<EndOfStreamException>();
            proxy.Stopped.ShouldBeTrue();
            proxy.AcceptedConnections.ShouldBe(1);
            proxy.DrainedConnections.ShouldBe(1);
        }
        finally
        {
            _ = await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask());
            listener.Stop();
        }
    }

    /// <summary>An accept-loop fault cannot skip an accepted session stalled in startup.</summary>
    [Fact]
    public async Task AcceptFailureStillDrainsPreviouslyAcceptedSessionAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var proxy = new PostgresFaultProxy(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None);
        try
        {
            proxy.Start();
            using var caller = new TcpClient();
            await caller.ConnectAsync("127.0.0.1", proxy.Port, deadline.Token);
            using TcpClient provider = await listener.AcceptTcpClientAsync(deadline.Token);
            await caller.GetStream().WriteAsync(new byte[] { 0 }, deadline.Token);
            proxy.InterruptListener();
            while (!proxy.AcceptFailed) await Task.Delay(10, deadline.Token);
            (await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask())).ShouldBeOfType<AggregateException>();
            proxy.Stopped.ShouldBeTrue();
            proxy.ActiveConnections.ShouldBe(0);
            proxy.DrainedConnections.ShouldBe(1);
        }
        finally
        {
            _ = await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask());
            listener.Stop();
        }
    }
}
