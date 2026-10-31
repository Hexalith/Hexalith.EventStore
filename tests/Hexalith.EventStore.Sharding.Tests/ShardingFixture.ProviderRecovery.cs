using System.Globalization;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Owned storage lifecycle and observable PostgreSQL failure boundaries.</summary>
internal sealed partial class ShardingFixture
{
    private string _providerIdentity = string.Empty;
    private string _databaseIdentity = string.Empty;

    private string ProviderVolume => _runId + "-postgres-data";

    /// <summary>Stops the exact provider while preserving its container and named storage.</summary>
    /// <param name="abrupt">Whether to send SIGKILL rather than the image's graceful stop signal.</param>
    internal async Task StopProviderAsync(bool abrupt)
    {
        await _lifecycle.WaitAsync(_work.Token);
        try
        {
            EnsureActive();
            if (await ProviderIdentityAsync() != _providerIdentity) throw new InvalidOperationException("Provider storage ownership drifted.");
            _databaseIdentity = (await QueryAsync("SELECT system_identifier::text FROM pg_control_system();")).Trim();
            _faultProxy?.BeginProviderRestart();
            _ = await CommandAsync("docker", abrupt ? ["kill", "--signal", "KILL", _postgres] : ["stop", "--time", "20", _postgres]);
            string status = (await CommandAsync("docker", ["inspect", "--format", "{{.State.Running}}|{{.State.ExitCode}}|{{.State.OOMKilled}}", _postgres])).Trim();
            string expected = abrupt ? "false|137|false" : "false|0|false";
            Record("provider-stopped", new { abrupt, status, expected, storageIdentity = await ProviderIdentityAsync(), databaseIdentity = _databaseIdentity,
                processIds = ProcessIds });
            if (status != expected) throw new InvalidOperationException("Provider did not stop at the selected graceful/SIGKILL boundary: " + status);
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>Recovers the same provider container and checks its mount and database identity.</summary>
    internal async Task RecoverProviderAsync()
    {
        await _lifecycle.WaitAsync(_work.Token);
        try
        {
            EnsureActive();
            _ = await CommandAsync("docker", ["start", _postgres]);
            await WaitPostgresAsync();
            _faultProxy?.EndProviderRestart();
            string identity = await ProviderIdentityAsync();
            string database = (await QueryAsync("SELECT system_identifier::text FROM pg_control_system();")).Trim();
            if (identity != _providerIdentity || database != _databaseIdentity)
                throw new InvalidOperationException("Recovery replaced provider storage or database identity.");
            Record("provider-recovered", new { storageIdentity = identity, databaseIdentity = database, processIds = ProcessIds,
                logs = Redact(await CommandAsync("docker", ["logs", "--tail", "120", _postgres])) });
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>Holds a bounded exclusive table lock until provider termination interrupts the session.</summary>
    /// <returns>The tracked lock-holder task, which the caller must observe after termination.</returns>
    internal Task<string> HoldStateLockAsync()
        => CommandAsync("docker", ["exec", "-e", "PGAPPNAME=sharding-recovery-lock", _postgres,
            "psql", "-U", "postgres", "-d", "sharding", "-v", "ON_ERROR_STOP=1", "-At", "-c",
            "BEGIN; LOCK TABLE public.state IN ACCESS EXCLUSIVE MODE; SELECT pg_sleep(25); ROLLBACK;"]);

    /// <summary>Waits for the actual provider table lock or a blocked state-save relation lock.</summary>
    /// <param name="blockedSave">Whether to observe a waiting writer rather than the lock holder.</param>
    internal async Task AwaitStateLockAsync(bool blockedSave)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        string predicate = blockedSave
            ? "a.application_name <> 'sharding-recovery-lock' AND NOT l.granted AND l.mode = 'RowExclusiveLock' AND a.wait_event_type = 'Lock'"
            : "a.application_name = 'sharding-recovery-lock' AND l.granted AND l.mode = 'AccessExclusiveLock'";
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            string result = (await QueryAsync("SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid "
                + "WHERE l.relation='public.state'::regclass AND " + predicate + ";", timeout.Token)).Trim();
            timeout.Token.ThrowIfCancellationRequested();
            if (int.Parse(result, CultureInfo.InvariantCulture) > 0)
            {
                string boundary = (await QueryAsync("SELECT json_agg(t)::text FROM (SELECT a.pid,a.application_name,a.state,a.wait_event_type,a.wait_event,l.mode,l.granted,a.query "
                    + "FROM pg_locks l JOIN pg_stat_activity a ON a.pid=l.pid WHERE l.relation='public.state'::regclass) t;", timeout.Token)).Trim();
                timeout.Token.ThrowIfCancellationRequested();
                Record(blockedSave ? "provider-save-blocked" : "provider-lock-granted", new { boundary });
                return;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private async Task<string> ProviderIdentityAsync()
    {
        string container = (await CommandAsync("docker", ["inspect", "--format",
            "{{.Id}}|{{index .Config.Labels \"hexalith.sharding.owner\"}}|{{range .Mounts}}{{if eq .Destination \"/var/lib/postgresql\"}}{{.Type}}|{{.Name}}|{{.Source}}|{{.Destination}}{{end}}{{end}}", _postgres])).Trim();
        if (!container.StartsWith(_postgres + "|" + _runId + "|volume|" + ProviderVolume + "|", StringComparison.Ordinal))
            throw new InvalidOperationException("PostgreSQL does not own the bound named volume.");
        string volume = (await CommandAsync("docker", ["volume", "inspect", "--format",
            "{{.Name}}|{{.Driver}}|{{.Mountpoint}}|{{index .Labels \"hexalith.sharding.owner\"}}", ProviderVolume])).Trim();
        if (!volume.StartsWith(ProviderVolume + "|local|", StringComparison.Ordinal) || !volume.EndsWith("|" + _runId, StringComparison.Ordinal))
            throw new InvalidOperationException("Provider volume ownership does not match this run.");
        return container + "\n" + volume;
    }

    private async Task<bool> RemoveProviderVolumeAsync()
    {
        string found = (await CommandAsync("docker", ["volume", "ls", "--format", "{{.Name}}", "--filter", "name=^" + ProviderVolume + "$"], cleanup: true)).Trim();
        if (found.Length != 0)
        {
            string owner = (await CommandAsync("docker", ["volume", "inspect", "--format", "{{index .Labels \"hexalith.sharding.owner\"}}", ProviderVolume], cleanup: true)).Trim();
            if (found != ProviderVolume || owner != _runId) throw new InvalidOperationException("Refusing to remove an unowned provider volume.");
            _ = await CommandAsync("docker", ["volume", "rm", ProviderVolume], cleanup: true);
        }

        string remaining = await CommandAsync("docker", ["volume", "ls", "--format", "{{.Name}}", "--filter", "label=hexalith.sharding.owner=" + _runId], cleanup: true);
        if (remaining.Trim().Length != 0)
            throw new InvalidOperationException("Owned provider volumes remain: " + remaining.Trim());
        return true;
    }

    private string CreateResiliency() => $$"""
        apiVersion: dapr.io/v1alpha1
        kind: Resiliency
        metadata:
          name: sharding-single-attempt
        scopes:
        - {{_runId}}
        spec:
          policies:
            retries:
              noRetry:
                policy: constant
                maxRetries: 0
          targets:
            actors:
              {{TestHost.ShardActor.ActorType}}:
                retry: noRetry
            components:
              sharding-state:
                outbound:
                  retry: noRetry
        """;
}
