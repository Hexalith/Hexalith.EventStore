using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Optional exact-key PostgreSQL wire faults and independent execution evidence.</summary>
internal sealed partial class ShardingFixture
{
    private readonly Dictionary<string, RegisteredShardOperation> _mutatingOperations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProviderFaultInstrumentation> _armedFaults = new(StringComparer.Ordinal);
    private bool _providerCommitProxy;
    private int _proxyPort;
    private PostgresFaultProxy? _faultProxy;

    /// <summary>Gets whether proxy teardown verified listener and sessions absent.</summary>
    internal bool ProxyStopped => _faultProxy is { Stopped: true, ActiveConnections: 0 };

    /// <summary>Gets actual accepted provider sessions, including startup connections.</summary>
    internal int ProxyAcceptedSessions => _faultProxy?.AcceptedConnections ?? 0;

    /// <summary>Gets sessions whose complete relay tasks drained.</summary>
    internal int ProxyDrainedSessions => _faultProxy?.DrainedConnections ?? 0;

    /// <summary>Reads the complete optional companion key independently from the provider.</summary>
    internal async Task<string?> DurableWitnessAsync(string tenant, string domain)
    {
        EnsureActive();
        string key = _runId + "||" + ShardActor.ActorType + "||" + ShardIdentity.Encode(tenant, domain) + "||" + ShardActor.CommitWitnessStateKey;
        string value = (await QueryAsync("SELECT value::text FROM state WHERE key = '" + key.Replace("'", "''", StringComparison.Ordinal) + "';")).Trim();
        Record("durable-commit-witness", new { tenant, domain, key, json = value });
        return value.Length == 0 ? null : value;
    }

    /// <summary>Assigns observations and rejects fixture-level reuse without hiding runtime duplicates.</summary>
    internal ShardRequest BindOperation(ShardRequest request, string? actorId = null)
    {
        if (_providerCommitProxy && request.OperationId is null && (!_providerOneKey || request.Operation != "Inspect")) request = request with { OperationId = Guid.NewGuid().ToString("N") };
        if (request.OperationId is { } id && request.Operation != "Inspect")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            lock (_mutatingOperations)
            {
                _armedFaults.TryGetValue(id, out ProviderFaultInstrumentation? fault);
                if (!_mutatingOperations.TryAdd(id, new(request, actorId ?? ShardIdentity.Encode(request.TenantId, request.Domain),
                    FaultMarker: fault?.Fault.Marker, FaultBoundary: fault?.Fault.Boundary)))
                    throw new InvalidOperationException("A mutating operation identity must never be replayed: " + id);
                if (_oneKeyFaults.TryGetValue(id, out ProviderOneKeyInstrumentation? oneKey))
                    _mutatingOperations[id] = _mutatingOperations[id] with { FaultMarker = oneKey.Fault.Marker, OneKeyBoundary = oneKey.Fault.Boundary };
            }
        }

        return request;
    }

    /// <summary>Installs an AFTER upsert trigger and nontransactional execution counter for one exact key.</summary>
    internal async Task<ProviderFaultInstrumentation> ArmCommitFaultAsync(ShardRequest request, PostgresCommitBoundary boundary)
    {
        EnsureActive();
        if (_faultProxy is null || request.OperationId is null) throw new InvalidOperationException("Bound proxy and explicit observation identity are required.");
        string name = "sharding_fault_" + Guid.NewGuid().ToString("N");
        string marker = name + ":exact-write";
        string key = _runId + "||" + ShardActor.ActorType + "||" + ShardIdentity.Encode(request.TenantId, request.Domain) + "||" + ShardActor.StateKey;
        string sql = $$"""
            CREATE SEQUENCE public.{{name}};
            CREATE FUNCTION public.{{name}}_fn() RETURNS trigger LANGUAGE plpgsql AS $fault$
            BEGIN
              IF NEW.key = '{{key.Replace("'", "''", StringComparison.Ordinal)}}' THEN
                PERFORM nextval('public.{{name}}');
                RAISE NOTICE '%', '{{marker}}';
              END IF;
              RETURN NEW;
            END $fault$;
            CREATE TRIGGER {{name}} AFTER INSERT OR UPDATE ON public.state
              FOR EACH ROW EXECUTE FUNCTION public.{{name}}_fn();
            """;
        _ = await QueryAsync(sql);
        PostgresCommitFault fault = _faultProxy.Arm(marker, boundary);
        Record("provider-fault-armed", new { request, boundary = boundary.ToString(), key, marker,
            trigger = name, sequence = name, sqlSha256 = RunBinding.HashText(sql), proxy = ProxyObservation() });
        var instrumentation = new ProviderFaultInstrumentation(fault, name, request.OperationId, request);
        lock (_mutatingOperations) _armedFaults.Add(request.OperationId, instrumentation);
        return instrumentation;
    }

    /// <summary>Requires actual provider marker/COMMIT readiness; a timeout fails the case.</summary>
    internal async Task AwaitCommitFaultAsync(ProviderFaultInstrumentation instrumentation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        await instrumentation.Fault.WaitAsync(deadline.Token);
        EnsureActive();
        Record("provider-fault-boundary", FaultObservation(instrumentation));
    }

    /// <summary>Asserts counts after request drain and before any provider restart or trigger removal.</summary>
    internal async Task CompleteCommitFaultAsync(ProviderFaultInstrumentation instrumentation)
    {
        PostgresCommitFault fault = instrumentation.Fault;
        long? executions = null;
        AttemptObservation[] entries = [];
        string? reservation = null;
        string? witness = null;
        bool terminated = false;
        await FaultExperiment.RunAsync(async () =>
        {
            await AwaitActorExitAsync(instrumentation.OperationId);
            terminated = true;
        }, async () =>
        {
            executions = long.Parse((await QueryAsync("SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM public."
                + instrumentation.Name + ";")).Trim(), CultureInfo.InvariantCulture);
        }, async () => reservation = await DurableAsync(instrumentation.Request.TenantId, instrumentation.Request.Domain),
        async () => witness = await DurableWitnessAsync(instrumentation.Request.TenantId, instrumentation.Request.Domain),
        async () => entries = await AttemptsAsync(instrumentation.OperationId), () =>
        {
            Record("provider-fault-final-attempts-before-restart", new { instrumentation.OperationId, terminated, executions,
                reservation, witness, fault = FaultObservation(instrumentation), entries, ledger = _faultProxy!.CommitLedger });
            bool acknowledgedBoundary = fault.Boundary == PostgresCommitBoundary.CommitAcknowledgement;
            if (!terminated || executions != 1 || fault.Notices != 1 || fault.Commits != 1 || fault.Gates != 1
                || fault.ForwardedCommits != (acknowledgedBoundary ? 1 : 0)
                || fault.CompletedCommits != (acknowledgedBoundary ? 1 : 0)
                || !entries.Select(entry => entry.Stage).SequenceEqual(new[] { "actor-entry", "save-start", "save-failed", "actor-exit" }))
                throw new InvalidOperationException("Actual provider or actor attempts differ from the single terminated fault; see retained evidence.");
            return Task.CompletedTask;
        });
        lock (_mutatingOperations)
            _mutatingOperations[instrumentation.OperationId] = _mutatingOperations[instrumentation.OperationId] with { FaultVerified = true };
        _faultProxy!.Disarm(fault);
        _ = await QueryAsync($"DROP TRIGGER {instrumentation.Name} ON public.state; DROP FUNCTION public.{instrumentation.Name}_fn(); DROP SEQUENCE public.{instrumentation.Name};");
    }

    /// <summary>Reads every independently flushed host journal, including replacement hosts.</summary>
    internal async Task<AttemptObservation[]> AttemptsAsync(string? operationId = null)
    {
        string directory = Path.Combine(_artifacts, "attempts");
        var result = new List<AttemptObservation>();
        foreach (string file in Directory.GetFiles(directory, "*.jsonl").Order(StringComparer.Ordinal))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string snapshot = await reader.ReadToEndAsync(_work.Token);
            // An in-flight append may end mid-record. Only newline-terminated flushed
            // records can establish actor exit; the next observation retries the tail.
            foreach (string line in snapshot[..(snapshot.LastIndexOf('\n') + 1)].Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                AttemptObservation entry = JsonSerializer.Deserialize<AttemptObservation>(line, JsonOptions)
                    ?? throw new InvalidDataException("Invalid attempt journal record.");
                if (Path.GetFileName(file) != "host-" + entry.ProcessId.ToString(CultureInfo.InvariantCulture) + ".jsonl")
                    throw new InvalidDataException("Attempt journal filename does not match its attributed host.");
                if (operationId is null || entry.OperationId == operationId) result.Add(entry);
            }
        }

        return result.ToArray();
    }

    /// <summary>Checks every mutating submission has one actor entry and no hidden repeated saves.</summary>
    internal async Task AssertOperationAttemptsAsync()
    {
        AttemptObservation[] entries = await AttemptsAsync();
        RegisteredShardOperation[] operations;
        lock (_mutatingOperations) operations = _mutatingOperations.Values.ToArray();
        int[] hosts = _processes.Where(process => process.StartInfo.ArgumentList.Contains(Path.Combine(_hostDirectory,
            "Hexalith.EventStore.Sharding.TestHost.dll"))).Select(process => process.Id).ToArray();
        PostgresCommitObservation[] ledger = _faultProxy!.CommitLedger;
        Record("all-operation-attempts", new { submittedMutations = operations.Length, operations, entries, hosts, ledger, proxy = ProxyObservation() });
        if (_faultProxy.Commits != ledger.Length || _faultProxy.CommitCompletions != ledger.Count(entry => entry.Completed))
            throw new InvalidDataException("Aggregate proxy counters do not reconcile with the complete COMMIT ledger.");
        ShardAttemptValidator.Validate(operations, entries, hosts, ledger);
    }

    /// <summary>Waits for a flushed server exit independently from SDK response/timeout.</summary>
    internal async Task AwaitActorExitAsync(string operationId)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        AttemptObservation[] entries = [];
        try
        {
            while (true)
            {
                entries = await AttemptsAsync(operationId);
                RegisteredShardOperation submitted;
                lock (_mutatingOperations) submitted = _mutatingOperations[operationId];
                int[] hosts = _processes.Where(process => process.StartInfo.ArgumentList.Contains(Path.Combine(_hostDirectory,
                    "Hexalith.EventStore.Sharding.TestHost.dll"))).Select(process => process.Id).ToArray();
                if (entries.Any(entry => entry.Request != submitted.Request || entry.ActorId != submitted.ActorId || !hosts.Contains(entry.ProcessId)))
                    throw new InvalidDataException("Unattributed actor termination evidence for " + operationId);
                int entered = entries.Count(entry => entry.Stage == "actor-entry");
                if (entered > 0 && entered == entries.Count(entry => entry.Stage == "actor-exit")) return;
                await Task.Delay(25, deadline.Token);
            }
        }
        catch
        {
            Record("actor-termination-not-observed", new { operationId, entries, proxy = ProxyObservation(), ledger = _faultProxy?.CommitLedger });
            throw;
        }
    }

    private void RecordOperationOutcome(ShardRequest request, string outcome, string? code, int sends)
    {
        if (request.OperationId is null || request.Operation == "Inspect") return;
        lock (_mutatingOperations)
            _mutatingOperations[request.OperationId] = _mutatingOperations[request.OperationId] with { Outcome = outcome, ReplyCode = code, Sends = sends };
    }

    /// <summary>Retains the caller's settled or timed-out outcome before any destructive cleanup.</summary>
    internal void RecordFaultRequestOutcome(string operationId, bool settled, Exception? failure, int sends)
        => Record("provider-fault-settled-request", new { operationId, settled, sends, failure = failure?.GetType().Name,
            message = failure is null ? null : Redact(failure.Message) });

    private object FaultObservation(ProviderFaultInstrumentation instrumentation) => new
    {
        instrumentation.OperationId, instrumentation.Name, boundary = instrumentation.Fault.Boundary.ToString(),
        instrumentation.Fault.Marker, instrumentation.Fault.Notices, instrumentation.Fault.Commits,
        instrumentation.Fault.ForwardedCommits, instrumentation.Fault.CompletedCommits, instrumentation.Fault.Gates,
        proxy = ProxyObservation(),
    };

    private object ProxyObservation() => new
    {
        enabled = _providerCommitProxy, port = _proxyPort, providerPort = _postgresPort,
        accepted = _faultProxy?.AcceptedConnections ?? 0, drained = _faultProxy?.DrainedConnections ?? 0,
        active = _faultProxy?.ActiveConnections ?? 0, commits = _faultProxy?.Commits ?? 0,
        completedCommits = _faultProxy?.CommitCompletions ?? 0, stopped = _faultProxy?.Stopped ?? false,
        ledger = _faultProxy?.CommitLedger, writes = _faultProxy?.WriteLedger, ordinaryOneKey = _providerOneKey, initialization = _faultProxy?.InitializationLedger,
    };

    private async Task<bool> VerifyProxyAbsentAsync()
    {
        if (_faultProxy is not { Stopped: true, ActiveConnections: 0 }) return false;
        using var probe = new TcpClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await probe.ConnectAsync("127.0.0.1", _proxyPort, deadline.Token);
            return false;
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionRefused) { return true; }
    }
}
