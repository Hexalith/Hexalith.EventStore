using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Exercises actual loopback framing, exact notice selection, fault gates and owned cancellation.</summary>
public sealed class PostgresFaultProxyTests
{
    /// <summary>Startup and typed frames survive one-byte fragmentation without merging messages.</summary>
    [Fact]
    public async Task FramesRetainExactPayloadsAcrossFragmentationAsync()
    {
        using var wire = new MemoryStream();
        var startup = new PostgresFrame(0, [0, 3, 0, 0, 0]);
        var command = Text('Q', "COMMIT\0");
        await startup.WriteAsync(wire, CancellationToken.None);
        await command.WriteAsync(wire, CancellationToken.None);
        using var fragmented = new FragmentedProtocolStream(wire.ToArray());
        (await PostgresFrame.ReadAsync(fragmented, true, CancellationToken.None))!.Payload.ShouldBe(startup.Payload);
        PostgresFrame frame = (await PostgresFrame.ReadAsync(fragmented, false, CancellationToken.None))!;
        frame.Type.ShouldBe((byte)'Q');
        frame.Payload.ShouldBe(command.Payload);
        (await PostgresFrame.ReadAsync(fragmented, false, CancellationToken.None)).ShouldBeNull();
    }

    /// <summary>Invalid lengths and truncated messages fail rather than being forwarded as complete frames.</summary>
    /// <param name="length">The wire-declared length.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(16777217)]
    public async Task InvalidFrameLengthRejectsAsync(int length)
    {
        byte[] bytes = new byte[5];
        bytes[0] = (byte)'Q';
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(1), length);
        using var stream = new MemoryStream(bytes);
        await Should.ThrowAsync<InvalidDataException>(() => PostgresFrame.ReadAsync(stream, false, CancellationToken.None));
        using var partial = new MemoryStream([(byte)'Q', 0, 0]);
        await Should.ThrowAsync<EndOfStreamException>(() => PostgresFrame.ReadAsync(partial, false, CancellationToken.None));
    }

    /// <summary>Only an exact backend NOTICE may select a fault; explicit break and cancellation drain both directions.</summary>
    /// <param name="acknowledgement">Whether to withhold PostgreSQL's successful COMMIT response.</param>
    /// <param name="cancel">Whether teardown cancels the armed gate.</param>
    /// <param name="extended">Whether the COMMIT uses Parse/Bind/Execute framing.</param>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task ExactNoticeGatesCommitAndOwnsEverySessionAsync(bool acknowledgement, bool cancel, bool extended)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var providerListener = new TcpListener(IPAddress.Loopback, 0);
        providerListener.Start();
        try
        {
            int providerPort = ((IPEndPoint)providerListener.LocalEndpoint).Port;
            await using var proxy = new PostgresFaultProxy(0, providerPort, CancellationToken.None);
            proxy.Start();
            PostgresCommitFault fault = proxy.Arm("selected-exact-write", acknowledgement
                ? PostgresCommitBoundary.CommitAcknowledgement : PostgresCommitBoundary.BeforeCommit);
            using var caller = new TcpClient();
            await caller.ConnectAsync("127.0.0.1", proxy.Port, deadline.Token);
            using TcpClient provider = await providerListener.AcceptTcpClientAsync(deadline.Token);
            NetworkStream client = caller.GetStream();
            NetworkStream server = provider.GetStream();
            await new PostgresFrame(0, [0, 3, 0, 0, 0]).WriteAsync(client, deadline.Token);
            (await PostgresFrame.ReadAsync(server, true, deadline.Token))!.Payload.ShouldBe(new byte[] { 0, 3, 0, 0, 0 });

            // Similar SQL/frontend text and a different NOTICE must not select this connection.
            await Text('N', "Mselected-exact-write-extra\0\0").WriteAsync(server, deadline.Token);
            await Text('Z', "T").WriteAsync(server, deadline.Token);
            (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'N');
            (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'Z');
            await Text('Q', "COMMIT\0").WriteAsync(client, deadline.Token);
            (await PostgresFrame.ReadAsync(server, false, deadline.Token))!.Field().ShouldBe("COMMIT");
            await Text('C', "COMMIT\0").WriteAsync(server, deadline.Token);
            await Text('Z', "I").WriteAsync(server, deadline.Token);
            (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Field().ShouldBe("COMMIT");
            (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'Z');
            fault.Notices.ShouldBe(0);
            fault.Gates.ShouldBe(0);

            await Text('N', "Mselected-exact-write\0\0").WriteAsync(server, deadline.Token);
            await Text('Z', "T").WriteAsync(server, deadline.Token);
            (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'N');
            (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'Z');
            if (extended)
            {
                await Text('P', "statement\0commit\0\0\0").WriteAsync(client, deadline.Token);
                await Text('B', "portal\0statement\0\0\0\0\0\0\0").WriteAsync(client, deadline.Token);
                (await PostgresFrame.ReadAsync(server, false, deadline.Token))!.Type.ShouldBe((byte)'P');
                (await PostgresFrame.ReadAsync(server, false, deadline.Token))!.Type.ShouldBe((byte)'B');
                await Text('E', "portal\0\0\0\0\0").WriteAsync(client, deadline.Token);
            }
            else await Text('Q', " COMMIT; \0").WriteAsync(client, deadline.Token);
            if (acknowledgement)
            {
                (await PostgresFrame.ReadAsync(server, false, deadline.Token))!.Type.ShouldBe(extended ? (byte)'E' : (byte)'Q');
                await Text('C', "COMMIT\0").WriteAsync(server, deadline.Token);
                await Text('Z', "I").WriteAsync(server, deadline.Token);
            }

            await fault.WaitAsync(deadline.Token);
            fault.Notices.ShouldBe(1);
            fault.Commits.ShouldBe(1);
            fault.Gates.ShouldBe(1);
            client.DataAvailable.ShouldBeFalse();
            if (!acknowledgement) server.DataAvailable.ShouldBeFalse();
            if (cancel) await proxy.DisposeAsync();
            else
            {
                fault.BreakConnection();
                while (proxy.ActiveConnections != 0) await Task.Delay(10, deadline.Token);
                await proxy.DisposeAsync();
            }

            fault.ForwardedCommits.ShouldBe(acknowledgement ? 1 : 0);
            fault.CompletedCommits.ShouldBe(acknowledgement ? 1 : 0);
            proxy.Commits.ShouldBe(2);
            proxy.CommitCompletions.ShouldBe(acknowledgement ? 2 : 1);
            proxy.AcceptedConnections.ShouldBe(1);
            proxy.DrainedConnections.ShouldBe(1);
            proxy.ActiveConnections.ShouldBe(0);
            proxy.Stopped.ShouldBeTrue();
            using var absence = new TcpClient();
            await Should.ThrowAsync<SocketException>(async () => await absence.ConnectAsync("127.0.0.1", proxy.Port, deadline.Token));
        }
        finally { providerListener.Stop(); }
    }

    /// <summary>Every repeated provider observation remains visible, even after the first fault is released.</summary>
    [Fact]
    public async Task ObservationCountersRetainUnexpectedRetriesAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var providerListener = new TcpListener(IPAddress.Loopback, 0);
        providerListener.Start();
        try
        {
            await using var proxy = new PostgresFaultProxy(0, ((IPEndPoint)providerListener.LocalEndpoint).Port, CancellationToken.None);
            proxy.Start();
            PostgresCommitFault fault = proxy.Arm("repeated-write", PostgresCommitBoundary.BeforeCommit);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var caller = new TcpClient();
                await caller.ConnectAsync("127.0.0.1", proxy.Port, deadline.Token);
                using TcpClient provider = await providerListener.AcceptTcpClientAsync(deadline.Token);
                NetworkStream client = caller.GetStream();
                NetworkStream server = provider.GetStream();
                await new PostgresFrame(0, [0, 3, 0, 0, 0]).WriteAsync(client, deadline.Token);
                (await PostgresFrame.ReadAsync(server, true, deadline.Token)).ShouldNotBeNull();
                await Text('N', "Mrepeated-write\0\0").WriteAsync(server, deadline.Token);
                await Text('Z', "T").WriteAsync(server, deadline.Token);
                (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'N');
                (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'Z');
                await Text('Q', "COMMIT\0").WriteAsync(client, deadline.Token);
                if (attempt == 0)
                {
                    await fault.WaitAsync(deadline.Token);
                    fault.BreakConnection();
                    while (proxy.ActiveConnections != 0) await Task.Delay(10, deadline.Token);
                }
                else
                {
                    // A runtime replay is allowed through so every extra execution and
                    // committed write is observable, rather than hidden by deduplication.
                    (await PostgresFrame.ReadAsync(server, false, deadline.Token))!.Field().ShouldBe("COMMIT");
                    await Text('C', "COMMIT\0").WriteAsync(server, deadline.Token);
                    await Text('Z', "I").WriteAsync(server, deadline.Token);
                    (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Field().ShouldBe("COMMIT");
                    (await PostgresFrame.ReadAsync(client, false, deadline.Token))!.Type.ShouldBe((byte)'Z');
                }
            }

            await proxy.DisposeAsync();
            fault.Notices.ShouldBe(2);
            fault.Commits.ShouldBe(2);
            fault.ForwardedCommits.ShouldBe(1);
            fault.CompletedCommits.ShouldBe(1);
            fault.Gates.ShouldBe(2);
            proxy.AcceptedConnections.ShouldBe(2);
            proxy.DrainedConnections.ShouldBe(2);
        }
        finally { providerListener.Stop(); }
    }

    /// <summary>Cancellation also drains a connection stalled inside its incomplete startup frame.</summary>
    [Fact]
    public async Task CancellationDrainsPartialStartupAndListenerAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var providerListener = new TcpListener(IPAddress.Loopback, 0);
        providerListener.Start();
        try
        {
            await using var proxy = new PostgresFaultProxy(0, ((IPEndPoint)providerListener.LocalEndpoint).Port, CancellationToken.None);
            proxy.Start();
            using var caller = new TcpClient();
            await caller.ConnectAsync("127.0.0.1", proxy.Port, deadline.Token);
            using TcpClient provider = await providerListener.AcceptTcpClientAsync(deadline.Token);
            await caller.GetStream().WriteAsync(new byte[] { 0 }, deadline.Token);
            await proxy.DisposeAsync();
            proxy.AcceptedConnections.ShouldBe(1);
            proxy.DrainedConnections.ShouldBe(1);
            proxy.Stopped.ShouldBeTrue();
        }
        finally { providerListener.Stop(); }
    }

    private static PostgresFrame Text(char type, string value) => new((byte)type, Encoding.UTF8.GetBytes(value));
}
