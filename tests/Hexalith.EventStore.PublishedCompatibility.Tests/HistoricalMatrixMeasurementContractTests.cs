using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Proves characterization never hides a supported regression or grants historical compatibility.</summary>
public sealed class HistoricalMatrixMeasurementContractTests
{
    private const string Claims = """{"selected":"3.128.2","source":"935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8","qualified":false,"p1rUsable":false,"rollback":null,"owners":"pending","conformance":"pending"}""";

    /// <summary>A real old-writer deficit remains failed and incompatible after characterization.</summary>
    [Fact]
    public void KnownUnsafeHistoricalEffectRemainsFailedAndNonqualifying()
    {
        var observation = Observation("metadata-write", "3.70.1-Release-head12-floor5", "failed", "unverified", ["boundary-floor-preserved"]);
        HistoricalMatrixMeasurementContract.ValidateObservation(observation);
        observation.Execution.ShouldBe("failed");
        observation.Failures.ShouldBe(["boundary-floor-preserved"]);
        observation.Passing.ShouldBeFalse();
    }

    /// <summary>The independently tested manifest refusal remains incompatible in each actual origin.</summary>
    [Theory]
    [InlineData("Release")]
    [InlineData("Debug")]
    public void SuccessfulManifestRefusalDoesNotGrantCompatibility(string configuration)
    {
        var observation = Observation("registered-evolution", configuration + "-registered-manifest-production-hop", "passed", "incompatible");
        HistoricalMatrixMeasurementContract.ValidateObservation(observation);
        observation.Passing.ShouldBeFalse();
    }

    /// <summary>Selected consumers and previously supported legacy operations retain strict Passing.</summary>
    [Theory]
    [InlineData("invalid-evidence", "3.128.2-Release-floor-0-tenant-a")]
    [InlineData("full-replay", "3.70.1-to-3.128.2")]
    [InlineData("query-wire", "3.128.2-package-Probe-to-3.128.2-source-Probe-json")]
    public void SupportedFailureAlwaysBlocksCharacterization(string scenario, string name)
    {
        var good = Observation(scenario, name);
        HistoricalMatrixMeasurementContract.ValidateObservation(good);
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixMeasurementContract.ValidateObservation(
            good with { Execution = "failed", Failures = ["unexpected-supported-defect"] }));
    }

    /// <summary>A comparison version is never a blanket exemption for new failures or missing checks.</summary>
    [Theory]
    [InlineData("new-cause")]
    [InlineData("missing-cause")]
    [InlineData("unavailable")]
    [InlineData("zero-assertions")]
    [InlineData("missing-time")]
    [InlineData("unexpected-exception")]
    public void KnownDeficitRejectsEveryUnmeasuredOrNewCause(string mutation)
    {
        var original = Observation("metadata-write", "3.70.1-Release-head12-floor5", "failed", "unverified", ["boundary-floor-preserved"]);
        var changed = mutation switch
        {
            "new-cause" => original with { Failures = ["boundary-floor-preserved", "foreign-new-defect"] },
            "missing-cause" => original with { Failures = [] },
            "unavailable" => original with { Execution = "unavailable" },
            "zero-assertions" => original with { Assertions = 0 },
            "missing-time" => original with { Ended = null },
            _ => original with { Error = "System.TimeoutException: an unrelated failure" },
        };
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixMeasurementContract.ValidateObservation(changed));
    }

    /// <summary>Only the observed old ProjectionEventDto XML contract absence matches the retained exception.</summary>
    [Fact]
    public void OldXmlDeficitRejectsAnotherException()
    {
        const string expected = "System.InvalidOperationException: Exit 134: dotnet /owned/package-3.70.1-Probe/Probe.dll\n\nUnhandled exception. System.Runtime.Serialization.InvalidDataContractException: Type 'Hexalith.EventStore.Contracts.Projections.ProjectionEventDto' cannot be serialized.";
        var original = Observation("projection-wire", "3.70.1-package-Probe-to-3.128.2-package-Probe-xml", "failed", "unverified")
            with { Assertions = 0, Error = expected };
        HistoricalMatrixMeasurementContract.ValidateObservation(original);
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixMeasurementContract.ValidateObservation(
            original with { Error = "System.TimeoutException: no measured serialization boundary" }));
    }

    /// <summary>Unknown comparison cases cannot hide behind a familiar old-version prefix.</summary>
    [Fact]
    public void UnknownCaseRefusesObservation()
        => Should.Throw<InvalidOperationException>(() => HistoricalMatrixMeasurementContract.ValidateObservation(
            Observation("metadata-write", "3.70.1-new-unreviewed-operation", "failed", "unverified", ["boundary-floor-preserved"])));

    /// <summary>Removing or duplicating any case cannot satisfy the complete experiment inventory.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrDuplicateInventoryRefusesCharacterization(bool duplicate)
    {
        using var summary = JsonDocument.Parse(Claims);
        ScenarioObservation[] observations = duplicate ? Enumerable.Repeat(Observation("checkout", "package-to-exact-tag-source"), 239).ToArray() : [];
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixMeasurementContract.Validate(observations, summary.RootElement, default, [], []));
    }

    /// <summary>CI success never accepts a false qualification, rollback or changed historical owner decision.</summary>
    [Theory]
    [InlineData("qualified")]
    [InlineData("p1rUsable")]
    [InlineData("rollback")]
    [InlineData("owners")]
    [InlineData("conformance")]
    [InlineData("selected")]
    [InlineData("source")]
    public void FalseQualificationOrTupleClaimsRemainBlocked(string field)
    {
        JsonObject claims = JsonNode.Parse(Claims)!.AsObject();
        claims[field] = field is "qualified" or "p1rUsable" ? JsonValue.Create(true) : JsonValue.Create("unapproved");
        using var summary = JsonDocument.Parse(claims.ToJsonString());
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixMeasurementContract.ValidateReceiptClaims(summary.RootElement));
    }

    /// <summary>Honest unchanged nonqualifying claims remain valid.</summary>
    [Fact]
    public void HistoricalNonqualificationClaimsRemainUnchanged()
    {
        using var summary = JsonDocument.Parse(Claims);
        HistoricalMatrixMeasurementContract.ValidateReceiptClaims(summary.RootElement);
    }

    /// <summary>The independent complete synthetic experiment reaches every receipt guard without qualifying anything.</summary>
    [Fact]
    public void CompleteIndependentSyntheticFixturePreservesAllHistoricalResults()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        fixture.Validate();
        fixture.Observations.Count.ShouldBe(239);
        fixture.Summary["failed"]!.GetValue<int>().ShouldBe(74);
        fixture.Summary["incompatible"]!.GetValue<int>().ShouldBe(49);
        fixture.Summary["qualified"]!.GetValue<bool>().ShouldBeFalse();
    }

    /// <summary>Coherently captured replacement commands cannot change this case's operation or leave its observation window.</summary>
    [Theory]
    [InlineData("operation")]
    [InlineData("kind")]
    [InlineData("serializer")]
    [InlineData("outside-window")]
    [InlineData("duplicate-execution")]
    public void CompleteInputRequiresTheActualCaseOperationAndContainedDistinctExecution(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Scenario == "query-wire" && row.Case == "3.128.2-package-Probe-to-3.128.2-source-Probe-json");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        JsonObject data = JsonNode.Parse(records["probe-command"])!.AsObject();
        int actual = fixture.Commands.FindIndex(command => command.Started == data["Started"]!.GetValue<DateTimeOffset>());
        var command = fixture.Commands[actual];
        if (mutation == "outside-window") command = command with { Started = command.Started.AddYears(100), Ended = command.Ended.AddYears(100) };
        else if (mutation == "duplicate-execution")
        {
            records["probe-command#3"] = records["probe-command"];
            fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
            Should.Throw<InvalidOperationException>(fixture.Validate); return;
        }
        else
        {
            var input = JsonNode.Parse(command.Command.Input!)!.AsObject();
            input[mutation] = mutation == "operation" ? "identity" : mutation == "kind" ? "projection" : "xml";
            command = command with { Command = command.Command with { Input = input.ToJsonString() } };
        }
        fixture.Commands[actual] = command;
        records["probe-command"] = JsonSerializer.Serialize(new { command.Command, command.Started, command.Ended, command.ExitCode, command.OutputHash });
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Copying a source consumer and its loaded identity onto the destination cannot erase the required version hop.</summary>
    [Fact]
    public void CompleteInputRejectsCoherentlyDuplicatedHopProfiles()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Scenario == "full-replay" && row.Case == "3.70.1-to-3.128.2");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        records["runtime-profile#2"] = records["runtime-profile"];
        records["loaded-Host#2"] = records["loaded-Host"]; records["loaded-Domain#2"] = records["loaded-Domain"];
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Successful checks retain their actual committed bytes, exact refusal and pre-due ordering.</summary>
    [Theory]
    [InlineData("erased-committed-hashes")]
    [InlineData("changed-replay-hash")]
    [InlineData("selected-writer-effects")]
    [InlineData("changed-refusal-state")]
    [InlineData("changed-refusal-coordinate")]
    [InlineData("reminder-before-preparation")]
    public void CompleteInputRejectsErasedSafetyRelationships(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        string scenario = mutation.StartsWith("selected-writer", StringComparison.Ordinal) ? "invalid-evidence"
            : mutation.StartsWith("changed-refusal", StringComparison.Ordinal) ? "mixed-api"
            : mutation.StartsWith("reminder", StringComparison.Ordinal) ? "natural-reminder" : "full-replay";
        string name = scenario == "invalid-evidence" ? "3.128.2-Release-head12-floor0" : scenario == "mixed-api" ? "3.128.2-Release"
            : scenario == "natural-reminder" ? "Release" : "3.70.1-to-3.128.2";
        int index = fixture.Observations.FindIndex(row => row.Scenario == scenario && row.Case == name);
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        if (scenario == "full-replay")
        {
            foreach (string key in records.Keys.Where(key => key.StartsWith("inventory:", StringComparison.Ordinal)).ToArray())
            {
                var inventory = JsonNode.Parse(records[key])!.AsObject(); var hashes = inventory["hashes"]!.AsObject();
                if (mutation == "erased-committed-hashes") foreach (string field in hashes.Select(pair => pair.Key).ToArray()) hashes[field] = null;
                else if (key.EndsWith("#2", StringComparison.Ordinal)) hashes[hashes.Select(pair => pair.Key).Single(field => field.EndsWith(":events:1", StringComparison.Ordinal))] = new string('0', 64);
                records[key] = inventory.ToJsonString();
            }
        }
        else
        {
            string key = scenario == "invalid-evidence" ? "actual-writer-admission-result" : scenario == "mixed-api" ? "capability-security-observations" : "bounded-reminder-registration-restart-window";
            var data = JsonNode.Parse(records[key])!.AsObject();
            if (scenario == "invalid-evidence") { data["refused"] = false; data["protectionCalls"] = 999; data["allocations"] = 999; data["staging"] = 999; }
            else if (scenario == "natural-reminder") data["due"] = row.Started!.Value.AddSeconds(-1);
            else if (mutation == "changed-refusal-coordinate") data["forged"]!["code"] = "foreign-refusal";
            else data["forged"]!["after"]!.AsObject()["tenant-a:counter:synthetic:events:1"] = new string('0', 64);
            records[key] = data.ToJsonString();
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Actual HTTP paths cannot substitute for erased loaded bodies or unsuccessful mutations.</summary>
    [Theory]
    [InlineData("empty-get")]
    [InlineData("post-503")]
    [InlineData("post-outside-case")]
    [InlineData("missing-reader-path")]
    public void CompleteInputRejectsErasedHttpBodiesStatusesAndEndpointGeometry(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        string scenario = mutation == "empty-get" ? "full-replay" : mutation == "missing-reader-path" ? "invalid-evidence" : "natural-reminder";
        string name = scenario == "full-replay" ? "3.70.1-to-3.128.2" : scenario == "invalid-evidence" ? "3.128.2-Release-floor-0-tenant-a" : "Release";
        int index = fixture.Observations.FindIndex(row => row.Scenario == scenario && row.Case == name);
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        if (mutation == "empty-get")
            foreach (string key in records.Keys.Where(key => key.StartsWith("http-get:", StringComparison.Ordinal)).ToArray()) records[key] = "{}";
        else if (mutation == "missing-reader-path")
        {
            string key = records.Keys.Single(key => key.StartsWith("http-get:", StringComparison.Ordinal) && key.Contains("/reader/", StringComparison.Ordinal));
            string body = records[key]; records.Remove(key); records.Add(key.Replace("/reader/", "/identity/", StringComparison.Ordinal), body);
        }
        else
        {
            var post = JsonNode.Parse(records["http-post"])!.AsObject();
            if (mutation == "post-503") post["status"] = 503;
            else { post["began"] = row.Started!.Value.AddYears(100); post["ended"] = row.Started!.Value.AddYears(100).AddSeconds(1); }
            records["http-post"] = post.ToJsonString();
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Observed provider labels must agree with the actual owned launcher, immutable removal and repeated absence.</summary>
    [Theory]
    [InlineData("foreign-owner")]
    [InlineData("wrong-image")]
    [InlineData("wrong-name")]
    [InlineData("uninterrupted")]
    [InlineData("changed-removal")]
    [InlineData("erased-absence")]
    [InlineData("late-inspection")]
    public void CompleteInputRejectsBorrowedProviderCleanupOrErasedLifecycle(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Scenario == "failure-cleanup" && row.Case == "timeout-and-detached-descendant");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        if (mutation is "foreign-owner" or "wrong-image" or "wrong-name")
        {
            var data = JsonNode.Parse(records["provider-created-before-interruption"])!.AsArray()[0]!;
            if (mutation == "foreign-owner") data["Config"]!["Labels"]!["p1r.owner"] = "p1r-" + new string('a', 32);
            if (mutation == "wrong-image") data["Image"] = "sha256:" + new string('b', 64);
            if (mutation == "wrong-name") data["Name"] = "/borrowed-creation-lifecycle";
            records["provider-created-before-interruption"] = new JsonArray(data.DeepClone()).ToJsonString();
        }
        else
        {
            int commandIndex = fixture.Commands.FindIndex(command => command.Started >= row.Started && command.Ended <= row.Ended
                && (mutation == "uninterrupted" ? command.Command.FileName == "python3"
                    : mutation == "changed-removal" ? command.Command.Arguments.FirstOrDefault() == "rm"
                    : command.Command.Arguments.FirstOrDefault() == "inspect" && (mutation == "erased-absence" ? command.ExitCode != 0 : command.ExitCode == 0)));
            var command = fixture.Commands[commandIndex];
            fixture.Commands[commandIndex] = mutation == "uninterrupted" ? command with { ExitCode = 0 }
                : mutation == "changed-removal" ? command with { Command = command.Command with { Arguments = ["rm", "-fv", new string('f', 64)] } }
                : mutation == "erased-absence" ? command with { ExitCode = 0 }
                : command with { Started = row.Started!.Value.AddSeconds(20), Ended = row.Started!.Value.AddSeconds(21) };
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Safe command metadata never copies an arbitrary action string out of credential-bearing input.</summary>
    [Fact]
    public void UnknownActorActionIsNotCapturedAsDiagnosticData()
    {
        var command = new QualificationCommand("dotnet", [], "/synthetic", TimeSpan.FromSeconds(1), Input: "{\"operation\":\"cross-client\",\"action\":\"arbitrary-private-material\"}");
        var captured = HistoricalExecutionCommand.Capture(new(command, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, "", "", new string('a', 64)));
        captured.Action.ShouldBeNull();
    }

    /// <summary>Even a matching captured stdout cannot replace exact actor identities, causal refusals or persisted append/replay relationships.</summary>
    [Theory]
    [InlineData("erased-identities")]
    [InlineData("forged-ok")]
    [InlineData("forged-cause")]
    [InlineData("wrong-append-sequence")]
    [InlineData("lost-replayed-flag")]
    [InlineData("changed-original-event")]
    [InlineData("missing-new-event")]
    [InlineData("changed-restart-bytes")]
    public void CompleteInputRejectsFalsifiedCrossClientEffectsAndRefusals(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Case == "client-3.128.2-Release-to-host-3.128.2-Debug");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        if (mutation is "changed-original-event" or "missing-new-event" or "changed-restart-bytes")
        {
            string key = records.Keys.Where(key => key.StartsWith("inventory:", StringComparison.Ordinal))
                .Single(key => HistoricalMatrixScenarioContract.Ordinal(key) == (mutation == "changed-original-event" ? 4 : mutation == "missing-new-event" ? 8 : 12));
            var data = JsonNode.Parse(records[key])!; var hashes = data["hashes"]!.AsObject();
            hashes[mutation == "changed-restart-bytes" ? "tenant-a:counter:synthetic:metadata"
                : mutation == "changed-original-event" ? "tenant-a:counter:synthetic:events:1" : "tenant-a:counter:synthetic:events:2"]
                = mutation == "missing-new-event" ? null : new string('f', 64);
            records[key] = data.ToJsonString();
        }
        else
        {
            string action = mutation == "erased-identities" ? "common" : mutation is "forged-ok" or "forged-cause" ? "fenced-forged"
                : mutation == "wrong-append-sequence" ? "fenced-valid" : "trusted-replay";
            string key = "cross-client-" + action; var data = JsonNode.Parse(records[key])!.AsObject();
            if (mutation == "erased-identities") data["identities"] = new JsonArray();
            if (mutation == "forged-ok") data["ok"] = true;
            if (mutation == "forged-cause") data["actualType"] = null;
            if (mutation == "wrong-append-sequence") data["outcome"]!["sequence"] = 1;
            if (mutation == "lost-replayed-flag") data["outcome"]!["replayed"] = false;
            records[key] = data.ToJsonString();
            int commandIndex = fixture.Commands.FindIndex(command => command.Started >= row.Started && command.Ended <= row.Ended
                && HistoricalExecutionCommand.Capture(command).Action == action);
            var command = fixture.Commands[commandIndex];
            fixture.Commands[commandIndex] = command with { StandardOutput = "P1R_RESULT " + data.ToJsonString(), OutputHash = QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes("P1R_RESULT " + data.ToJsonString() + command.StandardError)) };
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Original append and provider restore observations preserve prior bytes, record event13 and remain stable after restart.</summary>
    [Theory]
    [InlineData("missing-append")]
    [InlineData("changed-prior-event")]
    [InlineData("unchanged-metadata")]
    [InlineData("restore-second-tenant")]
    public void CompleteInputRejectsErasedAppendAndRestoreRelationships(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Scenario == (mutation == "restore-second-tenant" ? "post-upgrade-restore" : "metadata-write")
            && row.Case == (mutation == "restore-second-tenant" ? "3.128.2" : "3.70.1-to-3.128.2-metadata-write"));
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        string[] keys = records.Keys.Where(key => key.StartsWith("inventory:", StringComparison.Ordinal)).ToArray();
        string key = mutation == "restore-second-tenant" ? keys.Last(key => JsonNode.Parse(records[key])!["fixture"]!.GetValue<string>().StartsWith("tenant-b:", StringComparison.Ordinal))
            : keys.First(key => JsonNode.Parse(records[key])!["hashes"]!.AsObject().Any(field => field.Key.EndsWith(":events:13", StringComparison.Ordinal)));
        var data = JsonNode.Parse(records[key])!; var hashes = data["hashes"]!.AsObject();
        string actor = data["fixture"]!.GetValue<string>();
        if (mutation == "missing-append") hashes[actor + ":events:13"] = null;
        if (mutation == "changed-prior-event") hashes[actor + ":events:5"] = new string('a', 64);
        if (mutation == "restore-second-tenant") hashes[actor + ":events:1"] = new string('b', 64);
        if (mutation == "unchanged-metadata")
        {
            string before = keys.First(candidate => JsonNode.Parse(records[candidate])!["fixture"]!.GetValue<string>() == actor);
            hashes[actor + ":metadata"] = JsonNode.Parse(records[before])!["hashes"]![actor + ":metadata"]!.DeepClone();
        }
        records[key] = data.ToJsonString();
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Every saved aggregate is checked after a successful complete-input validation.</summary>
    [Theory]
    [InlineData("assertions")]
    [InlineData("passed")]
    [InlineData("failed")]
    [InlineData("unavailable")]
    [InlineData("incompatible")]
    public void CompleteInputRejectsEachFalsifiedAggregate(string aggregate)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        fixture.Validate();
        fixture.Summary[aggregate] = fixture.Summary[aggregate]!.GetValue<int>() + 1;
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("erase measured");
    }

    /// <summary>Erased successful checks cannot hide behind unchanged failure IDs and recomputed totals.</summary>
    [Theory]
    [InlineData("full-replay", "3.70.1-to-3.128.2")]
    [InlineData("metadata-write", "3.70.1-Release-head12-floor5")]
    public void CompleteInputRejectsErasedAssertions(string scenario, string name)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        int index = fixture.Observations.FindIndex(row => row.Scenario == scenario && row.Case == name);
        fixture.Observations[index] = fixture.Observations[index] with { Assertions = 1 };
        fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("assertion floor");
    }

    /// <summary>Required records and independent runtime, loaded-byte and inventory coordinates remain mandatory.</summary>
    [Theory]
    [InlineData("all-records")]
    [InlineData("one-required-record")]
    [InlineData("runtime-owner")]
    [InlineData("loaded-hash")]
    [InlineData("inventory-owner")]
    public void CompleteInputRejectsErasedOrForeignEvidence(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        int index = fixture.Observations.FindIndex(row => row.Scenario == "full-replay" && row.Case == "3.70.1-to-3.128.2");
        var records = new Dictionary<string, string>(fixture.Observations[index].Measurements!);
        if (mutation == "all-records") records.Clear();
        else if (mutation == "one-required-record") records.Remove("runtime-profile#2");
        else
        {
            string key = mutation == "runtime-owner" ? "runtime-profile" : mutation == "loaded-hash" ? "loaded-Host" : records.Keys.First(key => key.StartsWith("inventory:", StringComparison.Ordinal));
            JsonObject data = JsonNode.Parse(records[key])!.AsObject();
            if (mutation == "runtime-owner") data["appId"] = "foreign-owner";
            else if (mutation == "loaded-hash") data["identities"]![0]!["sha256"] = new string('0', 64);
            else data["fixture"] = "tenant-b:counter:foreign";
            records[key] = data.ToJsonString();
        }
        fixture.Observations[index] = fixture.Observations[index] with { Measurements = records };
        fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>The serialized result inventory must exactly preserve the actual validated observations.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("changed-result")]
    [InlineData("changed-measurement")]
    public void CompleteInputRejectsDifferentSavedObservations(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        if (mutation == "missing") fixture.Summary.Remove("observations");
        else if (mutation == "empty") fixture.Summary["observations"] = new JsonArray();
        else if (mutation == "changed-result") fixture.Summary["observations"]![0]!["Assertions"] = 1;
        else fixture.Summary["observations"]![0]!["Measurements"] = new JsonObject();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("saved observations");
    }

    /// <summary>Saved provenance is checked against an independently retained actual capture.</summary>
    [Theory]
    [InlineData("builds")]
    [InlineData("executor")]
    [InlineData("executorDiffHash")]
    [InlineData("executorInputs")]
    [InlineData("coordinatorHash")]
    [InlineData("templateHashes")]
    public void CompleteInputRejectsMissingOrInconsistentCapture(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        if (field is "coordinatorHash" or "templateHashes") fixture.Summary["executorInputs"]!.AsObject().Remove(field);
        else if (field == "executorInputs") fixture.Summary.Remove(field);
        else fixture.Summary[field] = new string('9', field == "executorDiffHash" ? 64 : 40);
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("saved provenance");
    }

    /// <summary>Coherently copied malformed capture data cannot replace the actual required executor coordinates.</summary>
    [Theory]
    [InlineData("executor")]
    [InlineData("diffHash")]
    [InlineData("coordinatorHash")]
    [InlineData("builds")]
    [InlineData("templateHashes")]
    [InlineData("consumerInputOrigin")]
    public void CompleteInputRequiresWellFormedActualCapture(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        fixture.Capture[field] = field == "templateHashes" ? new JsonObject()
            : JsonValue.Create(field == "builds" ? "db897eac9f2819220b32d237109d7255218b46a9" : "erased");
        fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Operation capture keeps the real stdin hash without retaining unrelated private input fields.</summary>
    [Fact]
    public void ActualCommandSnapshotDoesNotRetainPrivateStdin()
    {
        string input = JsonSerializer.Serialize(new { operation = "wire", kind = "projection", serializer = "xml", privateInput = "synthetic-sensitive-input" });
        var command = new QualificationCommand("dotnet", ["/synthetic/Probe.dll"], "/synthetic", TimeSpan.FromSeconds(30), Input: input);
        var snapshot = HistoricalExecutionCommand.Capture(new(command, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1), 0, "", "", new string('a', 64)));
        snapshot.Operation.ShouldBe("wire");
        snapshot.Kind.ShouldBe("projection");
        snapshot.Serializer.ShouldBe("xml");
        snapshot.Record.GetProperty("Command").GetProperty("InputHash").GetString().ShouldBe(command.InputHash);
        JsonSerializer.Serialize(snapshot).ShouldNotContain("synthetic-sensitive-input");
    }

    /// <summary>Unrelated executables or operations cannot borrow the known old XML exception text.</summary>
    [Theory]
    [InlineData("error-executable")]
    [InlineData("command-executable")]
    [InlineData("operation")]
    [InlineData("missing-command")]
    public void CompleteInputBindsXmlFailureToActualConsumerOperation(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create();
        int index = fixture.Observations.FindIndex(row => row.Case == "3.70.1-package-Probe-to-3.128.2-package-Probe-xml" && row.Scenario == "projection-wire");
        ScenarioObservation observation = fixture.Observations[index];
        int commandIndex = fixture.Commands.FindIndex(command => command.ExitCode == 134 && command.Started >= observation.Started && command.Ended <= observation.Ended);
        if (mutation == "error-executable") fixture.Observations[index] = observation with { Error = observation.Error!.Replace("/synthetic/contract-fixture/3.70.1-package-Probe/bin/Release/net10.0/Probe.dll", "/unrelated/Foreign.dll", StringComparison.Ordinal) };
        else if (mutation == "missing-command") fixture.Commands.RemoveAt(commandIndex);
        else
        {
            CommandObservation command = fixture.Commands[commandIndex];
            fixture.Commands[commandIndex] = command with { Command = mutation == "operation"
                ? command.Command with { Input = "{\"operation\":\"unrelated\",\"kind\":\"projection\"}" }
                : command.Command with { Arguments = ["/unrelated/Foreign.dll"] } };
        }
        fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    private static ScenarioObservation Observation(string scenario, string name, string execution = "passed",
        string compatibility = "compatible", string[]? failures = null)
        => new(scenario, name, execution, compatibility, 4, failures ?? [], Started: DateTimeOffset.UnixEpoch, Ended: DateTimeOffset.UnixEpoch.AddSeconds(1));
}
