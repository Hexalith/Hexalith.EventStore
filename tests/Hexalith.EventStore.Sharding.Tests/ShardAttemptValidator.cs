using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Reconciles the complete bound synthetic two-key profile; supplies no production guarantee.</summary>
internal static class ShardAttemptValidator
{
    /// <summary>Rejects hidden actor/provider execution, missing stages and unattributed journal records.</summary>
    internal static void Validate(RegisteredShardOperation[] operations, AttemptObservation[] entries,
        int[] ownedHostPids, PostgresCommitObservation[] ledger)
    {
        Dictionary<string, RegisteredShardOperation> registered = operations.ToDictionary(operation => operation.Request.OperationId!, StringComparer.Ordinal);
        AttemptObservation[] mutations = entries.Where(entry => entry.Request.Operation != "Inspect").ToArray();
        foreach (AttemptObservation entry in mutations)
        {
            if (!registered.TryGetValue(entry.OperationId, out RegisteredShardOperation? operation)
                || entry.Request != operation.Request || entry.ActorId != operation.ActorId
                || entry.ActorId != ShardIdentity.Encode(entry.Request.TenantId, entry.Request.Domain)
                || !ownedHostPids.Contains(entry.ProcessId))
                throw new InvalidDataException("Unregistered or misattributed mutating journal entry: " + entry.OperationId);
        }

        int saved = 0;
        foreach (RegisteredShardOperation operation in operations)
        {
            AttemptObservation[] actual = mutations.Where(entry => entry.OperationId == operation.Request.OperationId).ToArray();
            string[] expected;
            string exit;
            if (operation.Outcome == "acknowledged-success" && operation.ReplyCode == "saved")
            {
                expected = ["actor-entry", "save-start", "save-complete", "actor-exit"];
                exit = "saved";
                saved++;
            }
            else if (operation.Outcome is "acknowledged-success" or "known-rejection"
                && !string.IsNullOrWhiteSpace(operation.ReplyCode) && operation.ReplyCode != "saved")
            {
                expected = ["actor-entry", "actor-exit"];
                exit = operation.ReplyCode;
            }
            else if (operation.Outcome == "uncertain" && operation.FaultVerified && operation.FaultMarker is not null
                && operation.FaultBoundary is not null)
            {
                expected = ["actor-entry", "save-start", "save-failed", "actor-exit"];
                exit = "failed";
            }
            else throw new InvalidDataException("Unsettled or unsupported operation outcome: " + operation.Request.OperationId);

            if (operation.Sends != 1 || !actual.Select(entry => entry.Stage).SequenceEqual(expected)
                || actual.Select(entry => entry.ProcessId).Distinct().Count() != 1
                || actual[^1].Detail != exit
                || actual.Any(entry => entry.Stage == "save-failed" && string.IsNullOrWhiteSpace(entry.Detail)))
                throw new InvalidDataException("Illegal, missing or duplicate execution stages: " + operation.Request.OperationId);
        }

        RegisteredShardOperation[] faults = operations.Where(operation => operation.FaultMarker is not null).ToArray();
        if (ledger.Select(entry => entry.Id).Distinct().Count() != ledger.Length
            || ledger.Length != mutations.Count(entry => entry.Stage == "save-start"))
            throw new InvalidDataException("The complete COMMIT ledger does not reconcile with actor save starts.");
        foreach (RegisteredShardOperation operation in faults)
        {
            PostgresCommitObservation[] commits = ledger.Where(entry => entry.Marker == operation.FaultMarker).ToArray();
            bool completed = operation.FaultBoundary == PostgresCommitBoundary.CommitAcknowledgement;
            if (!operation.FaultVerified || operation.Outcome != "uncertain" || commits.Length != 1
                || commits[0].Forwarded != completed || commits[0].Completed != completed
                || commits[0].AcknowledgementWithheld != completed)
                throw new InvalidDataException("The selected fault COMMIT ledger is unexplained: " + operation.Request.OperationId);
        }

        PostgresCommitObservation[] ordinary = ledger.Where(entry => entry.Marker is null).ToArray();
        if (ordinary.Length != saved || ordinary.Any(entry => !entry.Forwarded || !entry.Completed || entry.AcknowledgementWithheld)
            || ledger.Any(entry => entry.Marker is not null && !faults.Any(operation => operation.FaultMarker == entry.Marker)))
            throw new InvalidDataException("Unexplained provider-only replay or incomplete ordinary COMMIT.");
    }
}
