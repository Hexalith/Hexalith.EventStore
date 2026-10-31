using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Exercises the exact validator used by the live two-key experiment against corrupted evidence.</summary>
public sealed class ShardAttemptValidatorTests
{
    /// <summary>Missing/illegal stages and unexplained request, route, host or provider activity must fail.</summary>
    /// <param name="corruption">The evidence defect.</param>
    [Theory]
    [InlineData("missing-save-complete")]
    [InlineData("illegal-save-failed")]
    [InlineData("missing-exit")]
    [InlineData("wrong-exit")]
    [InlineData("wrong-request")]
    [InlineData("wrong-route")]
    [InlineData("unknown-host")]
    [InlineData("unknown-mutation")]
    [InlineData("provider-replay")]
    [InlineData("missing-provider-completion")]
    public void RejectsCorruptedActualValidatorInputs(string corruption)
    {
        var request = new ShardRequest("Reserve", "tenant", "domain", 7, 2, OperationId: "save");
        string actor = ShardIdentity.Encode("tenant", "domain");
        var operation = new RegisteredShardOperation(request, actor, "acknowledged-success", "saved", 1);
        AttemptObservation[] entries = [Entry("actor-entry"), Entry("save-start"), Entry("save-complete"), Entry("actor-exit", "saved")];
        PostgresCommitObservation[] ledger = [new(1, 1, null, true, true)];
        ShardAttemptValidator.Validate([operation], entries, [123], ledger);
        switch (corruption)
        {
            case "missing-save-complete": entries = [entries[0], entries[1], entries[3]]; break;
            case "illegal-save-failed": entries = [.. entries[..3], Entry("save-failed", "IOException"), entries[3]]; break;
            case "missing-exit": entries = entries[..3]; break;
            case "wrong-exit": entries[3] = entries[3] with { Detail = "failed" }; break;
            case "wrong-request": entries[1] = entries[1] with { Request = request with { Count = 3 } }; break;
            case "wrong-route": entries[1] = entries[1] with { ActorId = "other" }; break;
            case "unknown-host": entries[1] = entries[1] with { ProcessId = 456 }; break;
            case "unknown-mutation": entries = [.. entries, Entry("actor-entry") with { OperationId = "unregistered" }]; break;
            case "provider-replay": ledger = [.. ledger, new(2, 2, null, true, true)]; break;
            case "missing-provider-completion": ledger[0] = ledger[0] with { Completed = false }; break;
        }

        Should.Throw<InvalidDataException>(() => ShardAttemptValidator.Validate([operation], entries, [123], ledger));
        AttemptObservation Entry(string stage, string? detail = null) => new("save", stage, 123, DateTimeOffset.UtcNow, actor, request, detail);
    }

    /// <summary>Failed-save stages and withheld provider completion reconcile only with the actual selected fault.</summary>
    /// <param name="beforeCommit">The selected boundary.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FaultLedgerRequiresFailedSaveAndExactCompletion(bool beforeCommit)
    {
        var request = new ShardRequest("Admit", "tenant", "domain", 7, OperationId: "fault");
        string actor = ShardIdentity.Encode("tenant", "domain");
        var operation = new RegisteredShardOperation(request, actor, "uncertain", Sends: 1, FaultMarker: "marker",
            FaultBoundary: beforeCommit ? PostgresCommitBoundary.BeforeCommit : PostgresCommitBoundary.CommitAcknowledgement, FaultVerified: true);
        AttemptObservation[] entries = [Entry("actor-entry"), Entry("save-start"), Entry("save-failed", "IOException"), Entry("actor-exit", "failed")];
        PostgresCommitObservation[] ledger = [new(1, 1, "marker", !beforeCommit, !beforeCommit, !beforeCommit)];
        ShardAttemptValidator.Validate([operation], entries, [123], ledger);
        Should.Throw<InvalidDataException>(() => ShardAttemptValidator.Validate([operation], [entries[0], entries[1], entries[3]], [123], ledger));
        Should.Throw<InvalidDataException>(() => ShardAttemptValidator.Validate([operation], entries, [123], [ledger[0] with { Completed = beforeCommit }]));
        Should.Throw<InvalidDataException>(() => ShardAttemptValidator.Validate([operation], entries, [123], [ledger[0] with { Marker = "unexplained" }]));
        AttemptObservation Entry(string stage, string? detail = null) => new("fault", stage, 123, DateTimeOffset.UtcNow, actor, request, detail);
    }
}
