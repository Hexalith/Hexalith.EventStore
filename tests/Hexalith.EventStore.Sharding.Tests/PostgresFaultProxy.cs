using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Owns a dependency-free loopback PostgreSQL fault boundary and drains every session.</summary>
internal sealed class PostgresFaultProxy : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly int _providerPort;
    private readonly CancellationTokenSource _stop;
    private readonly ConcurrentDictionary<int, PostgresProxySession> _connections = new();
    private readonly List<Task> _sessions = [];
    private readonly List<Exception> _failures = [];
    private readonly Dictionary<long, PostgresCommitObservation> _ledger = [];
    private PostgresCommitFault? _fault;
    private PostgresOneKeyFault? _oneKeyFault;
    private readonly Dictionary<long, PostgresWriteObservation> _writes = [];
    private readonly bool _oneKey;
    private volatile bool _workloadActive;
    private readonly List<object> _initialization = [];
    private Task? _accept;
    private int _accepted;
    private int _drained;
    private int _commits;
    private int _completions;
    private volatile bool _providerRestarting;

    /// <summary>Configures ports before the listener is started.</summary>
    internal PostgresFaultProxy(int port, int providerPort, CancellationToken token, bool oneKey = false, bool workloadInitiallyActive = true)
    {
        _listener = new(IPAddress.Loopback, port);
        _providerPort = providerPort;
        _oneKey = oneKey;
        _workloadActive = workloadInitiallyActive;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
    }

    /// <summary>Gets the owned listener port.</summary>
    internal int Port { get; private set; }

    /// <summary>Gets actual accepted sessions.</summary>
    internal int AcceptedConnections => Volatile.Read(ref _accepted);

    /// <summary>Gets fully drained sessions.</summary>
    internal int DrainedConnections => Volatile.Read(ref _drained);

    /// <summary>Gets still-owned sessions.</summary>
    internal int ActiveConnections => _connections.Count;

    /// <summary>Gets all frontend COMMIT executions, including unselected transactions and retries.</summary>
    internal int Commits => Volatile.Read(ref _commits);

    /// <summary>Gets all backend COMMIT completions observed.</summary>
    internal int CommitCompletions => Volatile.Read(ref _completions);

    /// <summary>Gets whether the listener and every accepted session have drained.</summary>
    internal bool Stopped { get; private set; }

    /// <summary>Gets whether an unexpected listener failure has completed its owned task.</summary>
    internal bool AcceptFailed => _accept?.IsFaulted == true;

    /// <summary>Gets the complete observed COMMIT ledger, rather than only aggregate counters.</summary>
    internal PostgresCommitObservation[] CommitLedger
    {
        get { lock (_ledger) return _ledger.Values.OrderBy(entry => entry.Id).ToArray(); }
    }

    /// <summary>Gets every ordinary write, including unselected writes and provider-only retries.</summary>
    internal PostgresWriteObservation[] WriteLedger
    {
        get { lock (_writes) return _writes.Values.OrderBy(entry => entry.Id).ToArray(); }
    }

    /// <summary>Gets hashed SQL observations from explicitly scoped component initialization windows.</summary>
    internal object[] InitializationLedger { get { lock (_initialization) return _initialization.ToArray(); } }

    /// <summary>Opens or closes the bound schema-initialization allowance around owned host startup.</summary>
    internal void SetWorkloadActive(bool active)
    {
        lock (_initialization) _initialization.Add(new { phase = active ? "workload" : "initialization", atUtc = DateTimeOffset.UtcNow });
        _workloadActive = active;
    }

    /// <summary>Arms a strict ordinary one-key fault.</summary>
    internal PostgresOneKeyFault ArmOneKey(string key, string marker, PostgresOneKeyBoundary boundary)
    {
        if (!_oneKey) throw new InvalidOperationException("One-key protocol accounting is not enabled.");
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        var fault = new PostgresOneKeyFault(key, marker, boundary);
        if (Interlocked.CompareExchange(ref _oneKeyFault, fault, null) is not null)
            throw new InvalidOperationException("A one-key provider fault is already armed.");
        return fault;
    }

    /// <summary>Disarms after complete settled attempt inspection.</summary>
    internal void DisarmOneKey(PostgresOneKeyFault fault)
    {
        if (Interlocked.CompareExchange(ref _oneKeyFault, null, fault) != fault)
            throw new InvalidOperationException("The one-key fault is not armed.");
    }

    /// <summary>Marks the exact owned provider lifecycle's expected connection losses.</summary>
    internal void BeginProviderRestart()
    {
        _providerRestarting = true;
        foreach (PostgresProxySession session in _connections.Values) session.ExpectProviderDisconnect();
    }

    /// <summary>Ends the owned provider outage classification after readiness is observed.</summary>
    internal void EndProviderRestart()
    {
        _providerRestarting = false;
        foreach (PostgresProxySession session in _connections.Values) session.EndExpectedProviderDisconnect();
    }

    /// <summary>Stops the real listener without cancellation to exercise unexpected accept failure.</summary>
    internal void InterruptListener() => _listener.Stop();

    /// <summary>Starts only after the configuration and ownership manifest are bound.</summary>
    internal void Start()
    {
        if (_accept is not null) throw new InvalidOperationException("Proxy already started.");
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accept = AcceptAsync();
    }

    /// <summary>Arms one exact marker; the caller keeps it armed until the request drains.</summary>
    internal PostgresCommitFault Arm(string marker, PostgresCommitBoundary boundary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        var fault = new PostgresCommitFault(marker, boundary);
        if (Interlocked.CompareExchange(ref _fault, fault, null) is not null)
            throw new InvalidOperationException("A provider fault is already armed.");
        return fault;
    }

    /// <summary>Removes instrumentation only after the caller inspected all attempts.</summary>
    internal void Disarm(PostgresCommitFault fault)
    {
        if (Interlocked.CompareExchange(ref _fault, null, fault) != fault)
            throw new InvalidOperationException("The selected fault is not armed.");
    }

    /// <summary>Cancels, closes and observes all listener/session tasks.</summary>
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        foreach (PostgresProxySession session in _connections.Values) session.Break();
        try
        {
            if (_accept is not null) await _accept;
        }
        catch (Exception exception) { lock (_failures) _failures.Add(exception); }
        finally
        {
            // An accept-loop failure must never skip an already accepted session.
            foreach (PostgresProxySession session in _connections.Values) session.Break();
            Task[] sessions;
            lock (_sessions) sessions = _sessions.ToArray();
            await Task.WhenAll(sessions);
        }
        Stopped = _connections.IsEmpty && AcceptedConnections == DrainedConnections;
        if (!Stopped) throw new InvalidOperationException("PostgreSQL proxy sessions remain undrained.");
        lock (_failures)
        {
            if (_failures.Count != 0) throw new AggregateException("PostgreSQL proxy protocol failed.", _failures);
        }
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                int id = Interlocked.Increment(ref _accepted);
                var session = new PostgresProxySession(client, _providerPort, () => Volatile.Read(ref _fault),
                    marker => ObserveCommit(id, marker), ObserveForwarded, ObserveCompletion,
                    _oneKey ? new PostgresOneKeySession(() => Volatile.Read(ref _oneKeyFault),
                        (statement, portal, key, value, marker) => ObserveWrite(id, statement, portal, key, value, marker), UpdateWrite, () => _workloadActive,
                        (stage, sql) => ObserveInitialization(id, stage, sql)) : null);
                if (_providerRestarting) session.ExpectProviderDisconnect();
                _connections[id] = session;
                Task task = ObserveSessionAsync(id, session);
                lock (_sessions) _sessions.Add(task);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
    }

    private async Task ObserveSessionAsync(int id, PostgresProxySession session)
    {
        try { await session.RunAsync(_stop.Token); }
        catch (InvalidDataException exception) { lock (_failures) _failures.Add(exception); }
        catch (Exception exception) when (session.ExpectedDisconnect || _stop.IsCancellationRequested
            || exception is PostgresFaultDisconnectException) { }
        catch (Exception exception) { lock (_failures) _failures.Add(exception); }
        finally
        {
            session.Break();
            _connections.TryRemove(id, out _);
            Interlocked.Increment(ref _drained);
        }
    }

    private void ObserveInitialization(int connectionId, string stage, string sql)
    {
        lock (_initialization) _initialization.Add(new { connectionId, stage,
            sqlSha256 = TestHost.RunBinding.HashText(sql), atUtc = DateTimeOffset.UtcNow });
    }

    private long ObserveWrite(int connectionId, string statement, string portal, string key, string value, string? marker)
    {
        lock (_writes)
        {
            long id = _writes.Count + 1;
            _writes.Add(id, new(id, connectionId, statement, portal, key, value, marker));
            return id;
        }
    }

    private void UpdateWrite(long id, string stage)
    {
        lock (_writes)
        {
            PostgresWriteObservation entry = _writes[id];
            _writes[id] = stage switch
            {
                "forwarded" => entry with { Forwarded = true },
                "command" when !entry.CommandCompleted => entry with { CommandCompleted = true },
                "idle" when entry.CommandCompleted && !entry.IdleCommitted => entry with { IdleCommitted = true },
                "withheld" when entry.IdleCommitted => entry with { AcknowledgementWithheld = true },
                "failed" => entry with { Failed = true },
                _ => throw new InvalidDataException("Illegal or duplicate one-key execution observation: " + stage),
            };
        }
    }

    private long ObserveCommit(int connectionId, string? marker)
    {
        lock (_ledger)
        {
            long id = Interlocked.Increment(ref _commits);
            _ledger.Add(id, new(id, connectionId, marker));
            return id;
        }
    }

    private void ObserveForwarded(long id)
    {
        lock (_ledger) _ledger[id] = _ledger[id] with { Forwarded = true };
    }

    private void ObserveCompletion(long id, bool withheld)
    {
        lock (_ledger)
        {
            if (_ledger[id].Completed) throw new InvalidDataException("Duplicate PostgreSQL COMMIT completion.");
            _ledger[id] = _ledger[id] with { Completed = true, AcknowledgementWithheld = withheld };
            Interlocked.Increment(ref _completions);
        }
    }
}
