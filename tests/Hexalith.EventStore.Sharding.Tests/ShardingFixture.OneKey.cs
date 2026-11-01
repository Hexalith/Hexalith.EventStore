using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Bound ordinary one-key provider faults, durable snapshots and complete attempt accounting.</summary>
internal sealed partial class ShardingFixture
{
    private bool _providerOneKey;
    private readonly Dictionary<string, ProviderOneKeyInstrumentation> _oneKeyFaults = new(StringComparer.Ordinal);

    /// <summary>Reads every application state row, rejecting unexpected keys in the live assertion.</summary>
    internal async Task<Dictionary<string, string>> AllStateRowsAsync()
    {
        Dictionary<string, string> complete = await AllProviderRowsAsync();
        return complete.ToDictionary(row => row.Key, row =>
        {
            using JsonDocument json = JsonDocument.Parse(row.Value);
            return json.RootElement.GetProperty("value").GetRawText();
        }, StringComparer.Ordinal);
    }

    /// <summary>Reads complete provider rows, including exact JSON, binary flag, TTL and provider metadata.</summary>
    internal async Task<Dictionary<string, string>> AllProviderRowsAsync()
    {
        string raw = (await QueryAsync("SELECT COALESCE(json_object_agg(s.key, to_jsonb(s) || jsonb_build_object('xmin', s.xmin::text)), '{}'::json)::text FROM public.state s;")).Trim();
        using JsonDocument json = JsonDocument.Parse(raw);
        var rows = json.RootElement.EnumerateObject().ToDictionary(member => member.Name, member => member.Value.GetRawText(), StringComparer.Ordinal);
        Record("all-complete-provider-state-rows", new { rows });
        foreach ((string key, string value) in rows)
        {
            using JsonDocument row = JsonDocument.Parse(value);
            if (row.RootElement.GetProperty("key").GetString() != key || row.RootElement.GetProperty("isbinary").GetBoolean()
                || row.RootElement.GetProperty("expiredate").ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("Unexpected binary, expiring or misattributed application row.");
        }
        return rows;
    }

    /// <summary>Gets the exact state key used by the runtime for this run.</summary>
    internal string ProviderStateKey(string tenant, string domain)
        => _runId + "||" + ShardActor.ActorType + "||" + ShardIdentity.Encode(tenant, domain) + "||" + ShardActor.StateKey;

    /// <summary>Installs independent exact-key execution instrumentation before selecting a wire gate.</summary>
    internal async Task<ProviderOneKeyInstrumentation> ArmOneKeyFaultAsync(ShardRequest request, PostgresOneKeyBoundary boundary)
    {
        EnsureActive();
        if (!_providerOneKey || _faultProxy is null || request.OperationId is null)
            throw new InvalidOperationException("The ordinary one-key profile and explicit operation identity are required.");
        string name = "sharding_one_key_" + Guid.NewGuid().ToString("N");
        string marker = name + ":exact-write";
        string key = ProviderStateKey(request.TenantId, request.Domain);
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
        PostgresOneKeyFault fault = _faultProxy.ArmOneKey(key, marker, boundary);
        var instrumentation = new ProviderOneKeyInstrumentation(fault, name, request);
        lock (_mutatingOperations) _oneKeyFaults.Add(request.OperationId, instrumentation);
        Record("one-key-fault-armed", new { request, key, marker, boundary = boundary.ToString(), name, sqlSha256 = RunBinding.HashText(sql) });
        return instrumentation;
    }

    /// <summary>Requires the actual selected wire boundary under a bounded deadline.</summary>
    internal async Task AwaitOneKeyFaultAsync(ProviderOneKeyInstrumentation instrumentation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        await instrumentation.Fault.WaitAsync(deadline.Token);
        EnsureActive();
        Record("one-key-boundary", OneKeyObservation(instrumentation));
    }

    /// <summary>Inspects settled attempts independently before removing any trigger or restarting storage.</summary>
    internal async Task CompleteOneKeyFaultAsync(ProviderOneKeyInstrumentation instrumentation)
    {
        long? executions = null;
        AttemptObservation[] entries = [];
        Dictionary<string, string>? rows = null;
        bool terminated = false;
        await FaultExperiment.RunAsync(async () =>
        {
            await AwaitActorExitAsync(instrumentation.Request.OperationId!);
            terminated = true;
        }, async () => executions = long.Parse((await QueryAsync("SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM public."
                + instrumentation.Name + ";")).Trim(), CultureInfo.InvariantCulture),
        async () => rows = await AllStateRowsAsync(),
        async () => entries = await AttemptsAsync(instrumentation.Request.OperationId), () =>
        {
            PostgresOneKeyFault fault = instrumentation.Fault;
            bool committed = fault.Boundary == PostgresOneKeyBoundary.CommittedIdle;
            Record("one-key-settled-attempts-before-restart", new { terminated, executions, rows, entries,
                observation = OneKeyObservation(instrumentation), writes = _faultProxy!.WriteLedger, commits = _faultProxy.CommitLedger });
            if (!terminated || executions != (committed ? 1 : 0) || fault.Executions != 1 || fault.Forwarded != (committed ? 1 : 0)
                || fault.Notices != (committed ? 1 : 0) || fault.Commands != (committed ? 1 : 0) || fault.Idle != (committed ? 1 : 0)
                || fault.Gates != 1 || _faultProxy!.Commits != 0
                || !entries.Select(entry => entry.Stage).SequenceEqual(new[] { "actor-entry", "save-start", "save-failed", "actor-exit" }))
                throw new InvalidDataException("Unexplained ordinary one-key fault attempts; retained diagnostics must be inspected.");
            return Task.CompletedTask;
        });
        lock (_mutatingOperations)
            _mutatingOperations[instrumentation.Request.OperationId!] = _mutatingOperations[instrumentation.Request.OperationId!] with { FaultVerified = true };
        _faultProxy!.DisarmOneKey(instrumentation.Fault);
        _ = await QueryAsync($"DROP TRIGGER {instrumentation.Name} ON public.state; DROP FUNCTION public.{instrumentation.Name}_fn(); DROP SEQUENCE public.{instrumentation.Name};");
    }

    /// <summary>Reconciles every submitted mutation, actor stage and provider write, with no explicit COMMIT.</summary>
    internal async Task AssertOneKeyAttemptsAsync()
    {
        AttemptObservation[] entries = await AttemptsAsync();
        RegisteredShardOperation[] operations;
        lock (_mutatingOperations) operations = _mutatingOperations.Values.ToArray();
        int[] hosts = _processes.Where(process => process.StartInfo.ArgumentList.Contains(Path.Combine(_hostDirectory,
            "Hexalith.EventStore.Sharding.TestHost.dll"))).Select(process => process.Id).ToArray();
        PostgresWriteObservation[] writes = _faultProxy!.WriteLedger;
        Record("all-one-key-attempts", new { operations, entries, hosts, writes, commits = _faultProxy.CommitLedger, initialization = _faultProxy.InitializationLedger });
        OneKeyAttemptValidator.Validate(operations, entries, hosts, writes, _faultProxy.CommitLedger, _runId);
    }

    private static object OneKeyObservation(ProviderOneKeyInstrumentation instrumentation) => new
    {
        instrumentation.Request, instrumentation.Name, instrumentation.Fault.Key, instrumentation.Fault.Marker,
        boundary = instrumentation.Fault.Boundary.ToString(), instrumentation.Fault.Executions, instrumentation.Fault.Forwarded,
        instrumentation.Fault.Notices, instrumentation.Fault.Commands, instrumentation.Fault.Idle, instrumentation.Fault.Gates,
    };
}
