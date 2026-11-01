using System.Text.Json;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Corrupts the exact validator inputs used by the ordinary one-key live experiment.</summary>
public sealed class OneKeyAttemptValidatorTests
{
    /// <summary>Every request, route, host, stage, key, value and provider execution must reconcile.</summary>
    /// <param name="corruption">The unexplained observation.</param>
    [Theory]
    [InlineData("request")]
    [InlineData("route")]
    [InlineData("host")]
    [InlineData("unregistered-actor")]
    [InlineData("duplicate-actor")]
    [InlineData("missing-stage")]
    [InlineData("wrong-exit")]
    [InlineData("sdk-replay")]
    [InlineData("provider-replay")]
    [InlineData("missing-provider")]
    [InlineData("wrong-key")]
    [InlineData("wrong-state")]
    [InlineData("numeric-generation")]
    [InlineData("additional-field")]
    [InlineData("wrong-marker")]
    [InlineData("provider-failure")]
    [InlineData("missing-idle")]
    [InlineData("missing-command")]
    [InlineData("missing-forward")]
    [InlineData("withheld-ordinary")]
    [InlineData("explicit-commit")]
    [InlineData("ordinary-fault-marker")]
    [InlineData("ordinary-fault-boundary")]
    [InlineData("ordinary-one-key-boundary")]
    [InlineData("ordinary-verified-fault")]
    public void RejectsCorruptedEvidence(string corruption)
    {
        var request = new ShardRequest("Admit", "tenant", "domain", long.MaxValue, OperationId: "admit");
        string actor = ShardIdentity.Encode("tenant", "domain");
        var operation = new RegisteredShardOperation(request, actor, "acknowledged-success", "saved", 1);
        AttemptObservation[] entries = Entries(request, actor, false);
        PostgresWriteObservation[] writes = [new(1, 1, "statement", "portal", Key(actor),
            JsonSerializer.Serialize(new ShardState("tenant", "domain", long.MaxValue, 0)), null, true, true, true)];
        PostgresCommitObservation[] commits = [];
        OneKeyAttemptValidator.Validate([operation], entries, [123], writes, commits, "run");
        switch (corruption)
        {
            case "request": entries[1] = entries[1] with { Request = request with { Count = 2 } }; break;
            case "route": entries[1] = entries[1] with { ActorId = "wrong" }; break;
            case "host": entries[1] = entries[1] with { ProcessId = 456 }; break;
            case "unregistered-actor": entries = [.. entries, entries[0] with { OperationId = "unknown" }]; break;
            case "duplicate-actor": entries = [.. entries, .. entries]; break;
            case "missing-stage": entries = [entries[0], entries[1], entries[3]]; break;
            case "wrong-exit": entries[3] = entries[3] with { Detail = "failed" }; break;
            case "sdk-replay": operation = operation with { Sends = 2 }; break;
            case "provider-replay": writes = [.. writes, writes[0] with { Id = 2 }]; break;
            case "missing-provider": writes = []; break;
            case "wrong-key": writes[0] = writes[0] with { Key = Key(actor) + "-extra" }; break;
            case "wrong-state": writes[0] = writes[0] with { Value = JsonSerializer.Serialize(new ShardState("tenant", "domain", long.MaxValue, 1)) }; break;
            case "numeric-generation": writes[0] = writes[0] with { Value = writes[0].Value.Replace("\"9223372036854775807\"", "9223372036854775807", StringComparison.Ordinal) }; break;
            case "additional-field": writes[0] = writes[0] with { Value = writes[0].Value[..^1] + ",\"extra\":true}" }; break;
            case "wrong-marker": writes[0] = writes[0] with { Marker = "unexplained" }; break;
            case "provider-failure": writes[0] = writes[0] with { Failed = true }; break;
            case "missing-idle": writes[0] = writes[0] with { IdleCommitted = false }; break;
            case "missing-command": writes[0] = writes[0] with { CommandCompleted = false }; break;
            case "missing-forward": writes[0] = writes[0] with { Forwarded = false }; break;
            case "withheld-ordinary": writes[0] = writes[0] with { AcknowledgementWithheld = true }; break;
            case "ordinary-fault-marker": operation = operation with { FaultMarker = "unexpected" }; writes[0] = writes[0] with { Marker = "unexpected" }; break;
            case "ordinary-fault-boundary": operation = operation with { FaultBoundary = PostgresCommitBoundary.BeforeCommit }; break;
            case "ordinary-one-key-boundary": operation = operation with { OneKeyBoundary = PostgresOneKeyBoundary.BeforeExecution }; break;
            case "ordinary-verified-fault": operation = operation with { FaultVerified = true }; break;
            case "explicit-commit": commits = [new(1, 1, null, true, true)]; break;
        }
        Should.Throw<InvalidDataException>(() => OneKeyAttemptValidator.Validate([operation], entries, [123], writes, commits, "run"));
    }

    /// <summary>Each fault permits exactly the observed execution and complete authority transition.</summary>
    /// <param name="committed">Whether idle commit was observed before connection break.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FaultRequiresExactPriorOrNextTransition(bool committed)
    {
        var admit = new ShardRequest("Admit", "tenant", "domain", long.MaxValue, OperationId: "admit");
        var reserve = new ShardRequest("Reserve", "tenant", "domain", long.MaxValue, 3, OperationId: "fault");
        string actor = ShardIdentity.Encode("tenant", "domain");
        RegisteredShardOperation[] operations = [new(admit, actor, "acknowledged-success", "saved", 1),
            new(reserve, actor, "uncertain", Sends: 1, FaultMarker: "exact", FaultVerified: true,
                OneKeyBoundary: committed ? PostgresOneKeyBoundary.CommittedIdle : PostgresOneKeyBoundary.BeforeExecution)];
        AttemptObservation[] entries = [.. Entries(admit, actor, false), .. Entries(reserve, actor, true)];
        PostgresWriteObservation[] writes = [new(1, 1, "s", "p", Key(actor), JsonSerializer.Serialize(new ShardState("tenant", "domain", long.MaxValue, 0)), null, true, true, true),
            new(2, 2, "s", "p", Key(actor), JsonSerializer.Serialize(new ShardState("tenant", "domain", long.MaxValue, 3)), "exact", committed, committed, committed, committed)];
        OneKeyAttemptValidator.Validate(operations, entries, [123], writes, [], "run");
        Should.Throw<InvalidDataException>(() => OneKeyAttemptValidator.Validate(operations, entries, [123],
            [writes[0], writes[1] with { IdleCommitted = !committed }], [], "run"));
        Should.Throw<InvalidDataException>(() => OneKeyAttemptValidator.Validate([operations[0], operations[1] with { FaultVerified = false }],
            entries, [123], writes, [], "run"));
    }

    /// <summary>Even self-consistent request, journal and provider evidence cannot authorize an invalid generation.</summary>
    /// <param name="generation">The nonpositive authority generation.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void RejectsNonpositiveGenerationTransition(long generation)
    {
        var request = new ShardRequest("Admit", "tenant", "domain", generation, OperationId: "invalid-generation");
        string actor = ShardIdentity.Encode("tenant", "domain");
        var operation = new RegisteredShardOperation(request, actor, "acknowledged-success", "saved", 1);
        PostgresWriteObservation[] writes = [new(1, 1, "s", "p", Key(actor),
            JsonSerializer.Serialize(new ShardState("tenant", "domain", generation, 0)), null, true, true, true)];
        Should.Throw<InvalidDataException>(() => OneKeyAttemptValidator.Validate([operation], Entries(request, actor, false), [123], writes, [], "run"));
    }

    private static AttemptObservation[] Entries(ShardRequest request, string actor, bool failed)
        => new[] { "actor-entry", "save-start", failed ? "save-failed" : "save-complete", "actor-exit" }
            .Select(stage => new AttemptObservation(request.OperationId!, stage, 123, DateTimeOffset.UtcNow, actor, request,
                stage == "actor-exit" ? failed ? "failed" : "saved" : stage == "save-failed" ? "IOException" : null)).ToArray();
    private static string Key(string actor) => "run||" + ShardActor.ActorType + "||" + actor + "||" + ShardActor.StateKey;
}
