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
