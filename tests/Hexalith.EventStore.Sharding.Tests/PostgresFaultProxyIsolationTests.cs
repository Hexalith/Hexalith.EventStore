using System.Net;
using System.Net.Sockets;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Negative real-socket proofs for connection, transaction and prepared-portal attribution.</summary>
public sealed class PostgresFaultProxyIsolationTests
{
    /// <summary>A marker on one concurrent connection cannot select another connection's COMMIT.</summary>
    /// <param name="acknowledgement">Whether the selected gate withholds the backend COMMIT completion.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MarkerCannotSelectConcurrentUnmarkedConnectionAsync(bool acknowledgement)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            await using var proxy = new PostgresFaultProxy(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None);
            proxy.Start();
            PostgresCommitFault fault = proxy.Arm("exact", acknowledgement
                ? PostgresCommitBoundary.CommitAcknowledgement : PostgresCommitBoundary.BeforeCommit);
            using PostgresProtocolTestPeer selected = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            using PostgresProtocolTestPeer other = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await MarkAsync(selected, "T", deadline.Token);
            await other.SendAsync('Q', "COMMIT\0", deadline.Token);
            (await other.ReadProviderAsync(deadline.Token))!.Field().ShouldBe("COMMIT");
            await CompleteAsync(other, deadline.Token);
            fault.Reached.ShouldBeFalse();
            fault.Commits.ShouldBe(0);
            fault.CompletedCommits.ShouldBe(0);
            await selected.SendAsync('Q', "COMMIT\0", deadline.Token);
            if (acknowledgement)
            {
                (await selected.ReadProviderAsync(deadline.Token))!.Field().ShouldBe("COMMIT");
                await selected.ReplyAsync('C', "COMMIT\0", deadline.Token);
                await selected.ReplyAsync('Z', "I", deadline.Token);
            }
            await fault.WaitAsync(deadline.Token);
            fault.BreakConnection();
            await proxy.DisposeAsync();
            fault.Notices.ShouldBe(1);
            fault.Commits.ShouldBe(1);
            proxy.CommitLedger.Single(entry => entry.Marker is null).Completed.ShouldBeTrue();
            PostgresCommitObservation selectedCommit = proxy.CommitLedger.Single(entry => entry.Marker == "exact");
            selectedCommit.Forwarded.ShouldBe(acknowledgement);
            selectedCommit.Completed.ShouldBe(acknowledgement);
            selectedCommit.AcknowledgementWithheld.ShouldBe(acknowledgement);
        }
        finally { listener.Stop(); }
    }

    /// <summary>Idle ReadyForQuery ends the prior marker's transaction before a later COMMIT.</summary>
    /// <param name="acknowledgement">Whether the armed gate targets the backend COMMIT completion.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdleTransactionClearsMarkerMatchAsync(bool acknowledgement)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            await using var proxy = new PostgresFaultProxy(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None);
            proxy.Start();
            PostgresCommitFault fault = proxy.Arm("exact", acknowledgement
                ? PostgresCommitBoundary.CommitAcknowledgement : PostgresCommitBoundary.BeforeCommit);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await MarkAsync(peer, "I", deadline.Token);
            await peer.SendAsync('Q', "COMMIT\0", deadline.Token);
            (await peer.ReadProviderAsync(deadline.Token))!.Field().ShouldBe("COMMIT");
            await CompleteAsync(peer, deadline.Token);
            fault.Notices.ShouldBe(1);
            fault.Commits.ShouldBe(0);
            fault.CompletedCommits.ShouldBe(0);
            fault.Reached.ShouldBeFalse();
            proxy.CommitLedger.Single().Marker.ShouldBeNull();
        }
        finally { listener.Stop(); }
    }

    /// <summary>Closed or transaction-expired portals cannot manufacture a COMMIT gate.</summary>
    /// <param name="invalidation">The actual protocol invalidation.</param>
    [Theory]
    [InlineData("portal-close")]
    [InlineData("statement-close-and-rebind")]
    [InlineData("idle")]
    public async Task InvalidPreparedPortalCannotSatisfyFaultAsync(string invalidation)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            await using var proxy = new PostgresFaultProxy(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None);
            proxy.Start();
            PostgresCommitFault fault = proxy.Arm("exact", PostgresCommitBoundary.BeforeCommit);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await PrepareAsync(peer, deadline.Token);
            if (invalidation == "idle")
            {
                await peer.ReplyAsync('Z', "I", deadline.Token);
                (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            }
            else
            {
                await peer.SendAsync('C', invalidation == "portal-close" ? "Pportal\0" : "Sstatement\0", deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
                if (invalidation == "statement-close-and-rebind")
                {
                    await peer.SendAsync('B', "missing\0statement\0\0\0\0\0\0\0", deadline.Token);
                    (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
                }
            }

            await MarkAsync(peer, "T", deadline.Token);
            await peer.SendAsync('E', invalidation == "statement-close-and-rebind" ? "missing\0\0\0\0\0" : "portal\0\0\0\0\0", deadline.Token);
            (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'E');
            await peer.ReplyAsync('E', "Mportal does not exist\0\0", deadline.Token);
            await peer.ReplyAsync('Z', "I", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'E');
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            fault.Reached.ShouldBeFalse();
            proxy.Commits.ShouldBe(0);
        }
        finally { listener.Stop(); }
    }

    /// <summary>Closing a prepared statement preserves its already-bound portal until transaction end.</summary>
    [Fact]
    public async Task StatementClosePreservesExistingBoundPortalAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            await using var proxy = new PostgresFaultProxy(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None);
            proxy.Start();
            PostgresCommitFault fault = proxy.Arm("exact", PostgresCommitBoundary.BeforeCommit);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await PrepareAsync(peer, deadline.Token);
            await peer.SendAsync('C', "Sstatement\0", deadline.Token);
            (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
            await MarkAsync(peer, "T", deadline.Token);
            await peer.SendAsync('E', "portal\0\0\0\0\0", deadline.Token);
            await fault.WaitAsync(deadline.Token);
            fault.BreakConnection();
            await proxy.DisposeAsync();
            fault.Commits.ShouldBe(1);
            fault.ForwardedCommits.ShouldBe(0);
        }
        finally { listener.Stop(); }
    }

    private static async Task PrepareAsync(PostgresProtocolTestPeer peer, CancellationToken token)
    {
        await peer.SendAsync('P', "statement\0COMMIT\0\0\0", token);
        await peer.SendAsync('B', "portal\0statement\0\0\0\0\0\0\0", token);
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'P');
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'B');
    }

    private static async Task MarkAsync(PostgresProtocolTestPeer peer, string status, CancellationToken token)
    {
        await peer.ReplyAsync('N', "Mexact\0\0", token);
        await peer.ReplyAsync('Z', status, token);
        (await peer.ReadCallerAsync(token))!.Type.ShouldBe((byte)'N');
        (await peer.ReadCallerAsync(token))!.Type.ShouldBe((byte)'Z');
    }

    private static async Task CompleteAsync(PostgresProtocolTestPeer peer, CancellationToken token)
    {
        await peer.ReplyAsync('C', "COMMIT\0", token);
        await peer.ReplyAsync('Z', "I", token);
        (await peer.ReadCallerAsync(token))!.Field().ShouldBe("COMMIT");
        (await peer.ReadCallerAsync(token))!.Type.ShouldBe((byte)'Z');
    }
}
