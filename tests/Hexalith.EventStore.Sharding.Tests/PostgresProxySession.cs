using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Relays framed protocol traffic and selects a transaction only from its exact NOTICE.</summary>
/// <param name="client">The owned frontend socket.</param>
/// <param name="providerPort">The loopback provider port.</param>
/// <param name="armed">The currently armed exact-key fault.</param>
/// <param name="observeCommit">Counts every observed frontend COMMIT.</param>
/// <param name="observeCompletion">Counts every backend COMMIT completion.</param>
/// <param name="observeForwarded">Records the complete forwarded COMMIT frame.</param>
internal sealed class PostgresProxySession(TcpClient client, int providerPort, Func<PostgresCommitFault?> armed,
    Func<string?, long> observeCommit, Action<long> observeForwarded, Action<long, bool> observeCompletion)
{
    private readonly TcpClient _provider = new();
    private readonly ConcurrentDictionary<string, bool> _statements = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _portals = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<long> _pendingCommits = new();
    private PostgresCommitFault? _matched;
    private volatile bool _expectedDisconnect;
    private volatile bool _providerDisconnectExpected;

    /// <summary>Gets whether owner teardown or a recorded provider restart explains this disconnect.</summary>
    internal bool ExpectedDisconnect => _expectedDisconnect || _providerDisconnectExpected;

    /// <summary>Marks a bound provider lifecycle operation before it interrupts sessions.</summary>
    internal void ExpectProviderDisconnect() => _providerDisconnectExpected = true;

    /// <summary>Ends the allowance for sessions surviving the provider's observed readiness.</summary>
    internal void EndExpectedProviderDisconnect() => _providerDisconnectExpected = false;

    /// <summary>Closes both owned sockets, including a connection stopped at a gate.</summary>
    internal void Break()
    {
        _expectedDisconnect = true;
        CloseSockets();
    }

    private void CloseSockets()
    {
        client.Dispose();
        _provider.Dispose();
    }

    /// <summary>Runs both directions and observes both tasks when either direction ends.</summary>
    internal async Task RunAsync(CancellationToken token)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        await _provider.ConnectAsync("127.0.0.1", providerPort, stop.Token);
        NetworkStream frontend = client.GetStream();
        NetworkStream backend = _provider.GetStream();
        while (true)
        {
            PostgresFrame? startup = await PostgresFrame.ReadAsync(frontend, startup: true, stop.Token);
            if (startup is null) return;
            if (startup.Payload.Length < 4) throw new InvalidDataException("Invalid PostgreSQL startup packet.");
            int code = BinaryPrimitives.ReadInt32BigEndian(startup.Payload);
            if (code is 80877103 or 80877104)
            {
                // The isolated component binds sslmode=disable. Refuse optional SSL/GSS
                // negotiation explicitly, keeping all later traffic in observable framing.
                await frontend.WriteAsync(new byte[] { (byte)'N' }, stop.Token);
                continue;
            }

            await startup.WriteAsync(backend, stop.Token);
            break;
        }

        Task forward = FrontendAsync(frontend, backend, stop.Token);
        Task backward = BackendAsync(backend, frontend, stop.Token);
        _ = await Task.WhenAny(forward, backward);
        // Preserve either direction's actual failure before inducing cancellation.
        // A clean EOF otherwise cancels the peer as part of normal session drain.
        Exception? failure = new[] { forward, backward }.FirstOrDefault(task => task.IsFaulted)?.Exception!.InnerException;
        bool unexpected = failure is not null and not PostgresFaultDisconnectException && !ExpectedDisconnect && !token.IsCancellationRequested;
        stop.Cancel();
        CloseSockets();
        try { await Task.WhenAll(forward, backward); }
        catch when (failure is null) { }
        catch when (failure is not null)
        {
            if (unexpected) throw new InvalidDataException("Unexpected PostgreSQL protocol/session failure.", failure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task FrontendAsync(Stream source, Stream target, CancellationToken token)
    {
        while (await PostgresFrame.ReadAsync(source, startup: false, token) is { } frame)
        {
            bool commit = IsCommit(frame);
            PostgresCommitFault? fault = Volatile.Read(ref _matched);
            long commitId = 0;
            if (commit)
            {
                commitId = observeCommit(fault?.Marker);
                _pendingCommits.Enqueue(commitId);
                fault?.ObserveCommit();
                if (fault?.Boundary == PostgresCommitBoundary.BeforeCommit) await fault.GateAsync(token);
            }

            await frame.WriteAsync(target, token);
            if (commit)
            {
                observeForwarded(commitId);
                fault?.ObserveForwardedCommit();
            }
        }
    }

    private async Task BackendAsync(Stream source, Stream target, CancellationToken token)
    {
        while (await PostgresFrame.ReadAsync(source, startup: false, token) is { } frame)
        {
            PostgresCommitFault? candidate = armed();
            if (candidate is not null && frame.HasNotice(candidate.Marker))
            {
                candidate.ObserveNotice();
                Volatile.Write(ref _matched, candidate);
            }

            PostgresCommitFault? fault = Volatile.Read(ref _matched);
            if (frame.Type == (byte)'C' && frame.Field() == "COMMIT")
            {
                if (!_pendingCommits.TryDequeue(out long commitId)) throw new InvalidDataException("Unexplained PostgreSQL COMMIT completion.");
                observeCompletion(commitId, fault?.Boundary == PostgresCommitBoundary.CommitAcknowledgement && fault.Gates == 0);
                fault?.ObserveCompletedCommit();
                if (fault?.Boundary == PostgresCommitBoundary.CommitAcknowledgement) await fault.GateAsync(token);
            }

            if (frame.Type == (byte)'Z' && frame.Payload.AsSpan().SequenceEqual(new byte[] { (byte)'I' }))
            {
                Volatile.Write(ref _matched, null);
                _portals.Clear();
            }
            await frame.WriteAsync(target, token);
        }
    }

    private bool IsCommit(PostgresFrame frame)
    {
        if (frame.Type == (byte)'Q') return IsCommitSql(frame.Field());
        if (frame.Type == (byte)'P')
        {
            string name = frame.Field();
            _statements[name] = IsCommitSql(frame.Field(System.Text.Encoding.UTF8.GetByteCount(name) + 1));
        }
        else if (frame.Type == (byte)'B')
        {
            string portal = frame.Field();
            string statement = frame.Field(System.Text.Encoding.UTF8.GetByteCount(portal) + 1);
            _portals[portal] = _statements.GetValueOrDefault(statement);
        }
        else if (frame.Type == (byte)'E') return _portals.GetValueOrDefault(frame.Field());
        else if (frame.Type == (byte)'C')
        {
            if (frame.Payload.Length == 0) throw new InvalidDataException("Invalid PostgreSQL Close message.");
            string name = frame.Field(1);
            if (frame.Payload[0] == (byte)'S') _statements.TryRemove(name, out _);
            else if (frame.Payload[0] == (byte)'P') _portals.TryRemove(name, out _);
            else throw new InvalidDataException("Unsupported PostgreSQL Close target.");
        }
        return false;
    }

    private static bool IsCommitSql(string sql)
        => sql.Trim().TrimEnd(';').Trim().Equals("COMMIT", StringComparison.OrdinalIgnoreCase);
}
