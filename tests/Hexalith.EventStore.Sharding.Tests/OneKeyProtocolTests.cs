using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Exercises real one-key framing, exact wire selection and withheld idle completion without infrastructure.</summary>
public sealed class OneKeyProtocolTests
{
    private static readonly string Value = JsonSerializer.Serialize(new ShardState("tenant", "domain", long.MaxValue, 7));

    /// <summary>Pre-execution forwards no Execute; post-commit withholds success until an actual idle completion.</summary>
    /// <param name="committed">Select the committed-idle boundary.</param>
    /// <param name="cancel">Cancel owner teardown with the gate held.</param>
    /// <param name="binary">Use binary JSONB/bool/etag parameter formats.</param>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task ExactExecutionAndIdleCommitGatesAreRealAsync(bool committed, bool cancel, bool binary)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            await using var proxy = Proxy(listener);
            proxy.Start();
            PostgresOneKeyFault fault = proxy.ArmOneKey("exact-key", "exact-notice", committed
                ? PostgresOneKeyBoundary.CommittedIdle : PostgresOneKeyBoundary.BeforeExecution);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await PrepareAsync(peer, binary, deadline.Token);
            await OneKeyProtocolFrames.Bind("exact-key", Value, binary, binary).WriteAsync(peer.Caller.GetStream(), deadline.Token);
            await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
            await peer.SendAsync('S', "", deadline.Token);
            (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
            if (committed)
            {
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'E');
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'S');
                await peer.ReplyAsync('N', "Mexact-notice\0\0", deadline.Token);
                (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'N');
                await peer.ReplyAsync('C', binary ? "UPDATE 1\0" : "INSERT 0 1\0", deadline.Token);
                while (fault.Commands == 0) await Task.Delay(5, deadline.Token);
                fault.Reached.ShouldBeFalse(); // CommandComplete alone cannot establish the implicit commit.
                peer.Caller.GetStream().DataAvailable.ShouldBeFalse();
                await peer.ReplyAsync('Z', "I", deadline.Token);
            }
            await fault.WaitAsync(deadline.Token);
            peer.Caller.GetStream().DataAvailable.ShouldBeFalse();
            if (!committed) peer.Provider.GetStream().DataAvailable.ShouldBeFalse();
            if (cancel) await proxy.DisposeAsync();
            else
            {
                fault.BreakConnection();
                while (proxy.ActiveConnections != 0) await Task.Delay(5, deadline.Token);
                await proxy.DisposeAsync();
            }
            fault.Executions.ShouldBe(1);
            fault.Forwarded.ShouldBe(committed ? 1 : 0);
            fault.Notices.ShouldBe(committed ? 1 : 0);
            fault.Commands.ShouldBe(committed ? 1 : 0);
            fault.Idle.ShouldBe(committed ? 1 : 0);
            fault.Gates.ShouldBe(1);
            proxy.Commits.ShouldBe(0);
            PostgresWriteObservation write = proxy.WriteLedger.Single();
            write.Key.ShouldBe("exact-key"); write.Value.ShouldBe(Value); write.Statement.ShouldBe("statement"); write.Portal.ShouldBe("portal");
            write.Forwarded.ShouldBe(committed); write.IdleCommitted.ShouldBe(committed); write.AcknowledgementWithheld.ShouldBe(committed);
            proxy.AcceptedConnections.ShouldBe(proxy.DrainedConnections);
            proxy.Stopped.ShouldBeTrue();
        }
        finally { listener.Stop(); }
    }

    /// <summary>Wrong keys never select a gate; named statements survive idle while portals expire.</summary>
    [Fact]
    public async Task WrongKeyIsAccountedAndPreparedStatementSurvivesIdleAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            await using var proxy = Proxy(listener); proxy.Start();
            PostgresOneKeyFault fault = proxy.ArmOneKey("exact-key", "exact", PostgresOneKeyBoundary.BeforeExecution);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await PrepareAsync(peer, false, deadline.Token);
            await BindExecuteAsync(peer, "exact-key-extra", deadline.Token);
            await peer.ReplyAsync('C', "INSERT 0 1\0", deadline.Token);
            await peer.ReplyAsync('Z', "I", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            fault.Reached.ShouldBeFalse();
            await OneKeyProtocolFrames.Bind("exact-key", Value).WriteAsync(peer.Caller.GetStream(), deadline.Token);
            (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
            await peer.SendAsync('C', "Sstatement\0", deadline.Token); // Existing portal remains valid after statement Close.
            (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
            await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
            await fault.WaitAsync(deadline.Token);
            fault.BreakConnection(); await proxy.DisposeAsync();
            proxy.WriteLedger.Length.ShouldBe(2);
            proxy.WriteLedger[0].Marker.ShouldBeNull();
            proxy.WriteLedger[0].IdleCommitted.ShouldBeTrue();
            proxy.WriteLedger[1].Marker.ShouldBe("exact");
            proxy.WriteLedger[1].Forwarded.ShouldBeFalse();
        }
        finally { listener.Stop(); }
    }

    /// <summary>Repeated operations remain observable instead of being deduplicated by a broken gate.</summary>
    [Fact]
    public async Task UnexpectedReplayRemainsInCompleteLedgerAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            await using var proxy = Proxy(listener); proxy.Start();
            PostgresOneKeyFault fault = proxy.ArmOneKey("key", "exact", PostgresOneKeyBoundary.BeforeExecution);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
                await PrepareAsync(peer, false, deadline.Token);
                await OneKeyProtocolFrames.Bind("key", Value).WriteAsync(peer.Caller.GetStream(), deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
                await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
                if (attempt == 0)
                {
                    await fault.WaitAsync(deadline.Token); fault.BreakConnection();
                    while (proxy.ActiveConnections != 0) await Task.Delay(5, deadline.Token);
                }
                else
                {
                    (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'E');
                    await peer.ReplyAsync('C', "INSERT 0 1\0", deadline.Token);
                    await peer.ReplyAsync('Z', "I", deadline.Token);
                    (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
                    (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
                }
            }
            await proxy.DisposeAsync();
            fault.Executions.ShouldBe(2); fault.Gates.ShouldBe(2); fault.Forwarded.ShouldBe(1);
            proxy.WriteLedger.Length.ShouldBe(2);
            proxy.WriteLedger[1].IdleCommitted.ShouldBeTrue();
        }
        finally { listener.Stop(); }
    }

    /// <summary>Malformed/error responses and unsupported execution shapes cannot manufacture readiness.</summary>
    /// <param name="defect">The protocol evidence defect.</param>
    [Theory]
    [InlineData("error")]
    [InlineData("transaction")]
    [InlineData("missing-command")]
    [InlineData("duplicate-command")]
    [InlineData("wrong-row-count")]
    [InlineData("wrong-notice")]
    [InlineData("malformed-ready")]
    [InlineData("malformed-notice")]
    [InlineData("multiple-execute")]
    [InlineData("closed-portal")]
    [InlineData("expired-portal")]
    [InlineData("malformed-bind")]
    [InlineData("simple-write")]
    [InlineData("comment-write")]
    [InlineData("explicit-begin")]
    [InlineData("unknown-sql")]
    [InlineData("cached-startup-plan")]
    [InlineData("invalid-binary-jsonb")]
    [InlineData("invalid-binary-etag")]
    public async Task UnsupportedOrFailedEvidenceRejectsWithoutGateAsync(string defect)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var proxy = Proxy(listener);
        try
        {
            proxy.Start();
            PostgresOneKeyFault fault = proxy.ArmOneKey("key", "exact", PostgresOneKeyBoundary.CommittedIdle);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            if (defect is "simple-write" or "comment-write" or "explicit-begin" or "unknown-sql")
            {
                await peer.SendAsync('Q', (defect switch
                {
                    "simple-write" => PostgresOneKeySession.InsertSql,
                    "comment-write" => "/* hidden */ UPDATE \"state\" SET value = '{}'",
                    "explicit-begin" => "BEGIN",
                    _ => "SELECT set_config('unexpected', 'true', false)",
                }) + "\0", deadline.Token);
            }
            else if (defect == "cached-startup-plan")
            {
                proxy.SetWorkloadActive(false);
                await OneKeyProtocolFrames.Parse(sql: "SELECT value FROM dapr_metadata WHERE key = $1 FOR UPDATE").WriteAsync(peer.Caller.GetStream(), deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'P');
                await OneKeyProtocolFrames.Bind("key", Value).WriteAsync(peer.Caller.GetStream(), deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
                proxy.SetWorkloadActive(true);
                await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
            }
            else
            {
                await PrepareAsync(peer, defect == "invalid-binary-etag", deadline.Token);
                if (defect is "invalid-binary-jsonb" or "invalid-binary-etag")
                    await OneKeyProtocolFrames.Bind("key", Value, binary: true, etag: defect == "invalid-binary-etag",
                        jsonbVersion: defect == "invalid-binary-jsonb" ? (byte)0 : (byte)1, binaryEtagLength: 3)
                        .WriteAsync(peer.Caller.GetStream(), deadline.Token);
                else if (defect == "malformed-bind") await peer.SendAsync('B', "portal\0statement\0\0", deadline.Token);
                else
                {
                    await OneKeyProtocolFrames.Bind("key", Value).WriteAsync(peer.Caller.GetStream(), deadline.Token);
                    (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
                    if (defect == "closed-portal")
                    {
                        await peer.SendAsync('C', "Pportal\0", deadline.Token);
                        (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
                    }
                    if (defect == "expired-portal")
                    {
                        await peer.ReplyAsync('Z', "I", deadline.Token);
                        (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
                    }
                    await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
                    if (defect is not ("closed-portal" or "expired-portal"))
                    {
                        (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'E');
                        if (defect == "multiple-execute") await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
                        else
                        {
                            await peer.ReplyAsync('N', defect == "wrong-notice" ? "Mexact-extra\0\0" : defect == "malformed-notice" ? "Mexact\0" : "Mexact\0\0", deadline.Token);
                            if (defect == "error") await peer.ReplyAsync('E', "Mprovider failed\0\0", deadline.Token);
                            else if (defect != "missing-command") await peer.ReplyAsync('C', defect == "wrong-row-count" ? "INSERT 0 0\0" : "INSERT 0 1\0", deadline.Token);
                            if (defect == "duplicate-command") await peer.ReplyAsync('C', "INSERT 0 1\0", deadline.Token);
                            await peer.ReplyAsync('Z', defect == "transaction" ? "T" : defect == "malformed-ready" ? "II" : "I", deadline.Token);
                        }
                    }
                }
            }
            while (proxy.ActiveConnections != 0) await Task.Delay(5, deadline.Token);
            await Should.ThrowAsync<AggregateException>(() => proxy.DisposeAsync().AsTask());
            fault.Reached.ShouldBeFalse();
            proxy.Stopped.ShouldBeTrue();
        }
        finally
        {
            _ = await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask());
            listener.Stop();
        }
    }

    /// <summary>The pinned pgx pool ping is a permitted empty simple query and does not pollute write attribution.</summary>
    [Fact]
    public async Task PoolPingDrainsBeforeNextExactWriteAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            await using var proxy = Proxy(listener); proxy.Start();
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await peer.SendAsync('Q', "-- ping\0", deadline.Token);
            (await peer.ReadProviderAsync(deadline.Token))!.Field().ShouldBe("-- ping");
            await peer.ReplyAsync('I', "", deadline.Token);
            await peer.ReplyAsync('Z', "I", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'I');
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            await PrepareAsync(peer, false, deadline.Token);
            await BindExecuteAsync(peer, "ordinary", deadline.Token);
            await peer.ReplyAsync('C', "INSERT 0 1\0", deadline.Token);
            await peer.ReplyAsync('Z', "I", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            proxy.WriteLedger.Single().IdleCommitted.ShouldBeTrue();
        }
        finally { listener.Stop(); }
    }

    /// <summary>Cancellation of a missing boundary cannot report readiness or authorize a break.</summary>
    [Fact]
    public async Task CancellationNeverManufacturesBoundaryAsync()
    {
        var fault = new PostgresOneKeyFault("key", "marker", PostgresOneKeyBoundary.BeforeExecution);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => fault.WaitAsync(stop.Token));
        fault.Reached.ShouldBeFalse(); fault.Gates.ShouldBe(0);
        Should.Throw<InvalidOperationException>(fault.BreakConnection);
    }

    /// <summary>Text etags and independently encoded JSONB/bool/etag parameters select the actual committed-idle gate.</summary>
    /// <param name="encoding">The supported per-parameter encoding layout.</param>
    [Theory]
    [InlineData("text-etag")]
    [InlineData("mixed-jsonb")]
    [InlineData("mixed-text-json")]
    public async Task TextEtagAndMixedParameterFormatsReachExactGateAsync(string encoding)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            await using var proxy = Proxy(listener); proxy.Start();
            PostgresOneKeyFault fault = proxy.ArmOneKey("key", "exact", PostgresOneKeyBoundary.CommittedIdle);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await PrepareAsync(peer, true, deadline.Token);
            short[] formats = encoding switch { "text-etag" => [], "mixed-jsonb" => [0, 1, 0, 1], _ => [1, 0, 1, 0] };
            await OneKeyProtocolFrames.Bind("key", Value, etag: true, parameterFormats: formats).WriteAsync(peer.Caller.GetStream(), deadline.Token);
            await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
            (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
            (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'E');
            await peer.ReplyAsync('N', "Mexact\0\0", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'N');
            await peer.ReplyAsync('C', "UPDATE 1\0", deadline.Token);
            await peer.ReplyAsync('Z', "I", deadline.Token);
            await fault.WaitAsync(deadline.Token);
            peer.Caller.GetStream().DataAvailable.ShouldBeFalse();
            proxy.WriteLedger.Single().Value.ShouldBe(Value);
            fault.BreakConnection();
            await proxy.DisposeAsync();
            proxy.WriteLedger.Single().IdleCommitted.ShouldBeTrue();
            proxy.WriteLedger.Single().AcknowledgementWithheld.ShouldBeTrue();
        }
        finally { listener.Stop(); }
    }

    /// <summary>Unknown initialization SQL cannot hide an authority mutation or manufacture a fault gate.</summary>
    /// <param name="sql">The unapproved schema/read/mutation statement.</param>
    [Theory]
    [InlineData("DELETE FROM state WHERE key = $1")]
    [InlineData("UPDATE state SET value = '{}'::jsonb")]
    [InlineData("CREATE TABLE unexpected (value text)")]
    [InlineData("SELECT set_config('unexpected', 'true', false)")]
    [InlineData(PostgresOneKeySession.InsertSql)]
    public async Task InitializationRejectsUnknownSqlAsync(string sql)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var proxy = Proxy(listener);
        try
        {
            proxy.Start(); proxy.SetWorkloadActive(false);
            PostgresOneKeyFault fault = proxy.ArmOneKey("key", "exact", PostgresOneKeyBoundary.BeforeExecution);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await peer.SendAsync('Q', sql + "\0", deadline.Token);
            while (proxy.ActiveConnections != 0) await Task.Delay(5, deadline.Token);
            await Should.ThrowAsync<AggregateException>(() => proxy.DisposeAsync().AsTask());
            proxy.WriteLedger.ShouldBeEmpty(); fault.Reached.ShouldBeFalse();
        }
        finally { _ = await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask()); listener.Stop(); }
    }

    /// <summary>The pinned metadata/schema statements retain their real transaction and command completions.</summary>
    [Fact]
    public async Task InitializationAllowsPinnedMetadataAndSchemaCompletionsAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            await using var proxy = Proxy(listener); proxy.Start(); proxy.SetWorkloadActive(false);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            foreach ((string sql, string tag, string status) in new[]
            {
                ("""
                    CREATE TABLE IF NOT EXISTS dapr_metadata (
                        key text NOT NULL PRIMARY KEY,
                        value text NOT NULL
                    )
                    """, "CREATE TABLE", "I"),
                ("""
                    CREATE TABLE IF NOT EXISTS state (
                        key text NOT NULL PRIMARY KEY,
                        value jsonb NOT NULL,
                        isbinary boolean NOT NULL,
                        insertdate TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        updatedate TIMESTAMP WITH TIME ZONE NULL
                    )
                    """, "CREATE TABLE", "I"),
                ("INSERT INTO dapr_metadata (key, value) VALUES ($1, 'lock') ON CONFLICT (key) DO NOTHING", "INSERT 0 0", "I"),
                ("BEGIN", "BEGIN", "T"),
                ("SELECT value FROM dapr_metadata WHERE key = $1 FOR UPDATE", "SELECT 1", "T"),
                ("ROLLBACK", "ROLLBACK", "I"),
                ("ALTER TABLE state ADD expiredate TIMESTAMP WITH TIME ZONE", "ALTER TABLE", "I"),
            })
            {
                await peer.SendAsync('Q', sql + "\0", deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'Q');
                await peer.ReplyAsync('C', tag + "\0", deadline.Token);
                await peer.ReplyAsync('Z', status, deadline.Token);
                (await peer.ReadCallerAsync(deadline.Token))!.Field().ShouldBe(tag);
                (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            }
            proxy.InitializationLedger.Length.ShouldBe(8); // One phase marker and seven exact SQL observations.
            proxy.WriteLedger.ShouldBeEmpty();
        }
        finally { listener.Stop(); }
    }

    /// <summary>A received Parse or Bind error cannot use the prior idle status to satisfy before-Execute readiness.</summary>
    /// <param name="stage">The failed protocol stage.</param>
    [Theory]
    [InlineData("parse")]
    [InlineData("bind")]
    public async Task PriorErrorCannotManufactureBeforeExecutionGateAsync(string stage)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var proxy = Proxy(listener);
        try
        {
            proxy.Start();
            PostgresOneKeyFault fault = proxy.ArmOneKey("key", "exact", PostgresOneKeyBoundary.BeforeExecution);
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await PrepareAsync(peer, false, deadline.Token);
            if (stage == "bind")
            {
                await OneKeyProtocolFrames.Bind("key", Value).WriteAsync(peer.Caller.GetStream(), deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
            }
            await peer.ReplyAsync('E', "Mprior protocol failure\0\0", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'E');
            if (stage == "parse")
            {
                await OneKeyProtocolFrames.Bind("key", Value).WriteAsync(peer.Caller.GetStream(), deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'B');
            }
            await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), deadline.Token);
            while (proxy.ActiveConnections != 0) await Task.Delay(5, deadline.Token);
            await Should.ThrowAsync<AggregateException>(() => proxy.DisposeAsync().AsTask());
            fault.Reached.ShouldBeFalse(); fault.Executions.ShouldBe(0); proxy.WriteLedger.ShouldBeEmpty();
        }
        finally { _ = await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask()); listener.Stop(); }
    }

    /// <summary>Read/ping/schema completions are attributable; an unrelated COMMIT or absent completion fails.</summary>
    /// <param name="query">The permitted pending operation.</param>
    /// <param name="response">The illegal completion.</param>
    [Theory]
    [InlineData("get", "COMMIT")]
    [InlineData("ping", "COMMIT")]
    [InlineData("schema", "COMMIT")]
    [InlineData("get", "missing")]
    [InlineData("ping", "missing")]
    [InlineData("get", "empty")]
    public async Task NonwriteCompletionMustMatchPendingExecutionAsync(string query, string response)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var proxy = Proxy(listener);
        try
        {
            proxy.Start();
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            if (query == "get") await SendGetAsync(peer, deadline.Token);
            else
            {
                if (query == "schema") proxy.SetWorkloadActive(false);
                await peer.SendAsync('Q', (query == "ping" ? "-- ping" : "ALTER TABLE state ADD expiredate TIMESTAMP WITH TIME ZONE") + "\0", deadline.Token);
                (await peer.ReadProviderAsync(deadline.Token))!.Type.ShouldBe((byte)'Q');
            }
            await peer.ReplyAsync(response == "missing" ? 'Z' : response == "empty" ? 'I' : 'C',
                response == "missing" ? "I" : response == "empty" ? "" : response + "\0", deadline.Token);
            while (proxy.ActiveConnections != 0) await Task.Delay(5, deadline.Token);
            await Should.ThrowAsync<AggregateException>(() => proxy.DisposeAsync().AsTask());
            proxy.WriteLedger.ShouldBeEmpty();
        }
        finally { _ = await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask()); listener.Stop(); }
    }

    /// <summary>A normal Get result or explicit error/idle sequence finishes before a distinct write starts.</summary>
    /// <param name="error">Whether the read reports a provider error.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegalReadCompletionOrErrorDrainsBeforeWriteAsync(bool error)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            await using var proxy = Proxy(listener); proxy.Start();
            using PostgresProtocolTestPeer peer = await PostgresProtocolTestPeer.ConnectAsync(proxy, listener, deadline.Token);
            await SendGetAsync(peer, deadline.Token);
            await peer.ReplyAsync(error ? 'E' : 'C', error ? "Mread failure\0\0" : "SELECT 1\0", deadline.Token);
            await peer.ReplyAsync('Z', "I", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe(error ? (byte)'E' : (byte)'C');
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            await PrepareAsync(peer, false, deadline.Token);
            await BindExecuteAsync(peer, "ordinary", deadline.Token);
            await peer.ReplyAsync('C', "INSERT 0 1\0", deadline.Token);
            await peer.ReplyAsync('Z', "I", deadline.Token);
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'C');
            (await peer.ReadCallerAsync(deadline.Token))!.Type.ShouldBe((byte)'Z');
            proxy.WriteLedger.Single().IdleCommitted.ShouldBeTrue();
        }
        finally { listener.Stop(); }
    }

    private static async Task SendGetAsync(PostgresProtocolTestPeer peer, CancellationToken token)
    {
        await peer.ReplyAsync('Z', "I", token);
        (await peer.ReadCallerAsync(token))!.Type.ShouldBe((byte)'Z');
        await OneKeyProtocolFrames.Parse(sql: PostgresOneKeySession.GetSql).WriteAsync(peer.Caller.GetStream(), token);
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'P');
        await OneKeyProtocolFrames.BindParameters([Encoding.UTF8.GetBytes("key")], []).WriteAsync(peer.Caller.GetStream(), token);
        await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), token);
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'B');
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'E');
    }

    private static PostgresFaultProxy Proxy(TcpListener listener)
        => new(0, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None, oneKey: true);

    private static async Task PrepareAsync(PostgresProtocolTestPeer peer, bool etag, CancellationToken token)
    {
        await peer.ReplyAsync('Z', "I", token);
        (await peer.ReadCallerAsync(token))!.Type.ShouldBe((byte)'Z');
        await OneKeyProtocolFrames.Parse(sql: etag ? PostgresOneKeySession.UpdateSql : null).WriteAsync(peer.Caller.GetStream(), token);
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'P');
    }

    private static async Task BindExecuteAsync(PostgresProtocolTestPeer peer, string key, CancellationToken token)
    {
        await OneKeyProtocolFrames.Bind(key, Value).WriteAsync(peer.Caller.GetStream(), token);
        await OneKeyProtocolFrames.Execute().WriteAsync(peer.Caller.GetStream(), token);
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'B');
        (await peer.ReadProviderAsync(token))!.Type.ShouldBe((byte)'E');
    }
}
