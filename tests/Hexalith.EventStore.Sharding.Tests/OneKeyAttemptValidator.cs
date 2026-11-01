using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Reconciles every ordinary one-key write with its request, actor turn and full exact authority.</summary>
internal static class OneKeyAttemptValidator
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Fails missing, duplicated, reordered, misattributed or unexplained provider and actor evidence.</summary>
    internal static void Validate(RegisteredShardOperation[] operations, AttemptObservation[] entries, int[] ownedHostPids,
        PostgresWriteObservation[] writes, PostgresCommitObservation[] commits, string runId)
    {
        if (commits.Length != 0 || writes.Select(write => write.Id).Distinct().Count() != writes.Length
            || !writes.Select(write => write.Id).SequenceEqual(Enumerable.Range(1, writes.Length).Select(value => (long)value)))
            throw new InvalidDataException("Explicit transactions or invalid one-key execution identities.");
        Dictionary<string, RegisteredShardOperation> registered = operations.ToDictionary(operation => operation.Request.OperationId!, StringComparer.Ordinal);
        foreach (AttemptObservation entry in entries)
        {
            if (!registered.TryGetValue(entry.OperationId, out RegisteredShardOperation? operation)
                || entry.Request != operation.Request || entry.ActorId != operation.ActorId
                || entry.ActorId != ShardIdentity.Encode(entry.Request.TenantId, entry.Request.Domain)
                || !ownedHostPids.Contains(entry.ProcessId))
                throw new InvalidDataException("Unregistered or misattributed one-key actor observation.");
        }
        int cursor = 0;
        var authority = new Dictionary<string, ShardState>(StringComparer.Ordinal);
        foreach (RegisteredShardOperation operation in operations)
        {
            ShardRequest request = operation.Request;
            AttemptObservation[] actual = entries.Where(entry => entry.OperationId == request.OperationId).ToArray();
            bool saved = operation.Outcome == "acknowledged-success" && operation.ReplyCode == "saved"
                && operation.FaultMarker is null && operation.FaultBoundary is null && operation.OneKeyBoundary is null && !operation.FaultVerified;
            bool faulted = operation.Outcome == "uncertain" && operation.FaultVerified && operation.FaultMarker is not null
                && operation.OneKeyBoundary is not null && operation.FaultBoundary is null;
            string[] expected;
            string exit;
            if (saved) { expected = ["actor-entry", "save-start", "save-complete", "actor-exit"]; exit = "saved"; }
            else if (faulted) { expected = ["actor-entry", "save-start", "save-failed", "actor-exit"]; exit = "failed"; }
            else if (operation.Outcome is "acknowledged-success" or "known-rejection"
                && !string.IsNullOrWhiteSpace(operation.ReplyCode) && operation.ReplyCode != "saved"
                && operation.FaultMarker is null && operation.OneKeyBoundary is null && operation.FaultBoundary is null && !operation.FaultVerified)
            { expected = ["actor-entry", "actor-exit"]; exit = operation.ReplyCode; }
            else throw new InvalidDataException("Unsettled one-key operation.");
            if (operation.Sends != 1 || !actual.Select(entry => entry.Stage).SequenceEqual(expected)
                || actual.Select(entry => entry.ProcessId).Distinct().Count() != 1 || actual[^1].Detail != exit
                || actual.Any(entry => entry.Stage == "save-failed" && string.IsNullOrWhiteSpace(entry.Detail)))
                throw new InvalidDataException("Illegal, missing or duplicate one-key actor stages.");
            if (!saved && !faulted) continue;
            if (cursor >= writes.Length) throw new InvalidDataException("Missing one-key provider execution.");
            PostgresWriteObservation write = writes[cursor++];
            string key = runId + "||" + ShardActor.ActorType + "||" + operation.ActorId + "||" + ShardActor.StateKey;
            authority.TryGetValue(key, out ShardState? prior);
            if (request.Generation <= 0 || (prior is not null && (prior.Generation <= 0 || prior.Ceiling < 0)))
                throw new InvalidDataException("Invalid generation or prior authority transition.");
            ShardState next = request.Operation switch
            {
                "Admit" when prior is null => new(request.TenantId, request.Domain, request.Generation, 0),
                "Reserve" when prior is not null && request.Count > 0 => prior with { Ceiling = checked(prior.Ceiling + request.Count) },
                "Seed" when prior is not null && prior.Ceiling == 0 && request.SeedCeiling >= 0 => prior with { Ceiling = request.SeedCeiling },
                _ => throw new InvalidDataException("Provider write has no permitted authority transition."),
            };
            bool committed = saved || operation.OneKeyBoundary == PostgresOneKeyBoundary.CommittedIdle;
            if (write.Key != key || write.ConnectionId <= 0 || write.Marker != operation.FaultMarker
                || write.Forwarded != committed || write.CommandCompleted != committed || write.IdleCommitted != committed
                || write.AcknowledgementWithheld != (faulted && committed) || write.Failed
                || next.Generation != request.Generation || ReadExactState(write.Value) != next)
                throw new InvalidDataException("One-key provider execution does not match its full request and durable transition.");
            if (committed) authority[key] = next;
        }
        if (cursor != writes.Length) throw new InvalidDataException("Unexplained provider-only replay or unregistered save.");
    }

    /// <summary>Requires the complete four-field state and exact decimal Int64 strings.</summary>
    internal static ShardState ReadExactState(string value)
    {
        using JsonDocument json = JsonDocument.Parse(value);
        if (json.RootElement.ValueKind != JsonValueKind.Object || json.RootElement.EnumerateObject().Count() != 4)
            throw new InvalidDataException("Incomplete or additional durable authority fields.");
        string[] names = json.RootElement.EnumerateObject().Select(member => member.Name.ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(new[] { "ceiling", "domain", "generation", "tenantid" }))
            throw new InvalidDataException("Invalid complete authority member set.");
        ShardState state = JsonSerializer.Deserialize<ShardState>(value, ReadOptions) ?? throw new InvalidDataException("Missing authority.");
        if (state.Generation <= 0 || state.Ceiling < 0) throw new InvalidDataException("Invalid durable authority generation or ceiling.");
        foreach ((string name, long number) in new[] { ("Generation", state.Generation), ("Ceiling", state.Ceiling) })
        {
            JsonElement field = json.RootElement.EnumerateObject().Single(member => member.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
            if (field.ValueKind != JsonValueKind.String || field.GetString() != number.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Lossy or noncanonical Int64 authority.");
        }
        return state;
    }
}
