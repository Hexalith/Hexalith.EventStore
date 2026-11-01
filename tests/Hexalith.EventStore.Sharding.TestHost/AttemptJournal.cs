using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>Flushes optional test-only observations to per-host files outside runtime scratch.</summary>
/// <param name="binding">The manifest binding containing the optional journal directory.</param>
/// <param name="beforeAppend">An optional deterministic journal-failure test hook.</param>
public sealed class AttemptJournal(RunBinding binding, Action<string>? beforeAppend = null)
{
    private readonly object _sync = new();

    /// <summary>Gets whether the binding explicitly selects a two-key provider COMMIT experiment.</summary>
    public bool CommitWitnessEnabled => binding.Configuration.ContainsKey("provider-commit-proxy")
        && !binding.Configuration.ContainsKey("provider-one-key");

    /// <summary>Records every execution, including repeated observation identities.</summary>
    /// <param name="request">The actual request.</param>
    /// <param name="actorId">The routed actor identity.</param>
    /// <param name="stage">The reached execution stage.</param>
    /// <param name="detail">The returned code or observed failure type.</param>
    public void Record(ShardRequest request, string actorId, string stage, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId is null || !binding.Configuration.TryGetValue("attempt-journal-directory", out string? directory)) return;
        if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("The attempt journal must use its pre-bound retained directory.");
        beforeAppend?.Invoke(stage);
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new AttemptObservation(request.OperationId, stage,
            Environment.ProcessId, DateTimeOffset.UtcNow, actorId, request, detail)) + "\n");
        lock (_sync)
        {
            using var stream = new FileStream(Path.Combine(directory, "host-" + Environment.ProcessId + ".jsonl"),
                FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
    }
}
