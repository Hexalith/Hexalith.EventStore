using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Independent coherent mutations of the concrete iteration2 producer/evidence boundaries.</summary>
public sealed class HistoricalLoopTwoEvidenceTests
{
    /// <summary>Refusal values must bind actual contemporaneous reads, with legitimate accepted writes between the two groups.</summary>
    [Theory]
    [InlineData("zero-returned-values")]
    [InlineData("copy-earlier-state")]
    [InlineData("missing-capture")]
    [InlineData("missing-read-command")]
    [InlineData("foreign-pid")]
    [InlineData("foreign-owner")]
    [InlineData("phase-order")]
    [InlineData("late-read")]
    [InlineData("changed-captured-state")]
    [InlineData("missing-accepted-event")]
    public void ProtectedRefusalsRequireTheirActualOrderedStateValues(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Measurements?.ContainsKey("capability-security-observations") == true);
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        var result = JsonNode.Parse(records["capability-security-observations"])!.AsObject();
        var capture = JsonNode.Parse(records["capability-security-capture"])!.AsObject();
        if (mutation == "zero-returned-values")
            foreach (string field in new[] { "stale", "forged", "completedStale", "unauthorized", "forgedEffect" })
            foreach (string phase in new[] { "before", "after" })
            foreach (string key in result[field]![phase]!.AsObject().Where(pair => pair.Value is not null).Select(pair => pair.Key).ToArray())
                result[field]![phase]![key] = new string('0', 64);
        else if (mutation == "copy-earlier-state") result["completedStale"]!["before"] = result["stale"]!["before"]!.DeepClone();
        else if (mutation == "missing-capture") records.Remove("capability-security-capture");
        else if (mutation == "missing-read-command") fixture.Commands.RemoveAll(command => command.Command.Arguments.Contains(HistoricalSecurityCapture.ReadScript));
        else if (mutation == "foreign-pid") capture["pid"] = 99999;
        else if (mutation == "foreign-owner") capture["owner"] = "p1r-" + new string('0', 32);
        else if (mutation == "phase-order") capture["snapshots"]![0]!["phase"] = "after";
        else if (mutation == "late-read") capture["snapshots"]![9]!["ended"] = row.Ended!.Value.AddSeconds(1);
        else if (mutation == "changed-captured-state") capture["snapshots"]![4]!["hashes"]!["tenant-a:counter:synthetic:metadata"] = new string('0', 64);
        else result["completedStale"]!["before"]!["tenant-a:counter:synthetic:events:2"] = null;
        records["capability-security-observations"] = result.ToJsonString();
        if (mutation != "missing-capture") records["capability-security-capture"] = capture.ToJsonString();
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>A copied successful HTTP body cannot contradict the actual selected reader's required refusal.</summary>
    [Theory]
    [InlineData("ok")]
    [InlineData("error")]
    [InlineData("message")]
    [InlineData("old-reader")]
    public void ReaderCaseRequiresItsExactRefusalOrCharacterizedLegacySuccess(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        string name = mutation == "old-reader" ? "3.70.1-Release-floor-0-tenant-a" : "3.128.2-Release-floor-0-tenant-a";
        int index = fixture.Observations.FindIndex(row => row.Scenario == "invalid-evidence" && row.Case == name);
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        string old = records["actual-versioned-reader-result"]; var data = JsonNode.Parse(old)!.AsObject();
        if (mutation is "ok" or "old-reader") data["ok"] = mutation == "ok";
        else data[mutation] = "foreign-refusal";
        string changed = data.ToJsonString(); records["actual-versioned-reader-result"] = changed;
        foreach (string key in records.Keys.Where(key => key.StartsWith("http-get:", StringComparison.Ordinal) && records[key] == old).ToArray())
        {
            records[key] = changed;
            string address = key["http-get:".Length..].Split('#')[0];
            foreach (string timingKey in records.Keys.Where(key => key.Split('#')[0] == "http-get-observation").ToArray())
            {
                var timing = JsonNode.Parse(records[timingKey])!.AsObject();
                if (timing["address"]!.GetValue<string>() != address) continue;
                timing["bodyHash"] = Hash(changed); records[timingKey] = timing.ToJsonString();
            }
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>The exact production router, invoker, bound principal and protected outcome remain mandatory.</summary>
    [Theory]
    [InlineData("valid", "success")]
    [InlineData("valid", "router")]
    [InlineData("valid", "invoker")]
    [InlineData("valid", "principal")]
    [InlineData("valid", "proof")]
    [InlineData("valid", "watermark")]
    [InlineData("forged", "success")]
    [InlineData("missing", "errorMessage")]
    [InlineData("foreign-tenant", "success")]
    [InlineData("foreign-actor", "success")]
    public void HandlerResultCannotReplaceTheActualProductionBoundary(string mode, string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Case == "Release-production-declared-handler-discovery-transport");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        string key = "production-handler-" + mode; var data = JsonNode.Parse(records[key])!.AsObject();
        if (mutation == "success") data[mutation] = mode != "valid";
        else if (mutation == "principal") data["payload"]!["tenantId"] = "foreign-tenant";
        else if (mutation == "proof") data["payload"]!["identityAdmissionProof"] = "";
        else if (mutation == "watermark") data["payload"]!["value"]!["watermark"] = 12;
        else data[mutation] = "foreign-implementation";
        records[key] = data.ToJsonString(); fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Actual stdout and stderr must hash correctly and contain one genuine structured result.</summary>
    [Theory]
    [InlineData("stdout-hash")]
    [InlineData("stderr-hash")]
    [InlineData("empty-result")]
    [InlineData("duplicate-result")]
    [InlineData("changed-identity")]
    public void ProbeRequiresObservedOutputBytesAndOneBoundResult(string mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int rowIndex = fixture.Observations.FindIndex(row => row.Scenario == "provenance" && row.Case == "3.128.2-package-Probe");
        var row = fixture.Observations[rowIndex]; var records = new Dictionary<string, string>(row.Measurements!);
        var measured = JsonNode.Parse(records["probe-command"])!.AsObject();
        int index = fixture.Commands.FindIndex(command => command.Started == measured["Started"]!.GetValue<DateTimeOffset>());
        var command = fixture.Commands[index]; string stdout = command.StandardOutput, stderr = command.StandardError;
        if (mutation is "stdout-hash" or "empty-result") stdout = "";
        if (mutation == "stderr-hash") stderr = "unrecorded diagnostics";
        if (mutation == "duplicate-result") stdout += "\n" + stdout;
        if (mutation == "changed-identity")
        {
            var output = JsonNode.Parse(stdout["P1R_RESULT ".Length..])!.AsObject(); output["identities"]![0]!["sha256"] = new string('0', 64);
            stdout = "P1R_RESULT " + output.ToJsonString();
        }
        command = command with { StandardOutput = stdout, StandardError = stderr,
            OutputHash = mutation.EndsWith("-hash", StringComparison.Ordinal) ? command.OutputHash : Hash(stdout + stderr) };
        fixture.Commands[index] = command;
        measured["OutputHash"] = command.OutputHash; records["probe-command"] = measured.ToJsonString();
        fixture.Observations[rowIndex] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>A coherently rehashed stdin must still execute the exact finite numeric metadata case.</summary>
    [Theory]
    [InlineData("currentSequence")]
    [InlineData("retainedFloor")]
    [InlineData("missing-floor")]
    public void MetadataCaptureBindsCaseDefiningNumericInputs(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int rowIndex = fixture.Observations.FindIndex(row => row.Scenario == "metadata-read" && row.Case == "3.128.2-floor-5");
        var row = fixture.Observations[rowIndex]; var records = new Dictionary<string, string>(row.Measurements!);
        var measured = JsonNode.Parse(records["probe-command"])!.AsObject();
        int index = fixture.Commands.FindIndex(command => command.Started == measured["Started"]!.GetValue<DateTimeOffset>());
        var command = fixture.Commands[index]; var input = JsonNode.Parse(command.Command.Input!)!.AsObject();
        var metadata = JsonNode.Parse(input["value"]!.GetValue<string>())!.AsObject();
        if (mutation == "missing-floor") metadata.Remove("retainedFloor"); else metadata[mutation] = 6;
        input["value"] = metadata.ToJsonString(); command = command with { Command = command.Command with { Input = input.ToJsonString() } };
        fixture.Commands[index] = command;
        records["probe-command"] = JsonSerializer.Serialize(new { command.Command, command.Started, command.Ended, command.ExitCode, command.OutputHash });
        fixture.Observations[rowIndex] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Matching all reported loaded bodies cannot replace the independently prepared source bytes.</summary>
    [Fact]
    public void CoherentSourceIdentityDriftCannotReplacePreparedOutputHashes()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Scenario == "full-replay" && row.Case == "3.128.2-to-tagged-source");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        foreach (string key in records.Keys.ToArray())
        {
            var data = JsonNode.Parse(records[key]);
            if (data is not JsonObject identity || identity["identities"] is not JsonArray identities
                || identities[0]!["configuration"]!.GetValue<string>() != "Debug") continue;
            foreach (var item in identities) item!["sha256"] = new string('0', 64);
            records[key] = identity.ToJsonString();
        }
        foreach (string key in records.Keys.Where(key => key.Split('#')[0] == "http-get-observation").ToArray())
        {
            var timing = JsonNode.Parse(records[key])!.AsObject(); string address = timing["address"]!.GetValue<string>();
            var body = records.FirstOrDefault(pair => pair.Key.StartsWith("http-get:" + address, StringComparison.Ordinal));
            if (body.Key is not null) { timing["bodyHash"] = Hash(body.Value); records[key] = timing.ToJsonString(); }
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Coherent transport hashes cannot substitute unrelated paths, inputs or lost durable cursor semantics.</summary>
    [Theory]
    [InlineData("unrelated-paths")]
    [InlineData("foreign-host")]
    [InlineData("foreign-aggregate")]
    [InlineData("reordered-operations")]
    [InlineData("wrong-position")]
    [InlineData("wrong-sequence")]
    [InlineData("foreign-cursor")]
    [InlineData("lost-first-decode")]
    [InlineData("false-advanced-decode")]
    [InlineData("lost-advanced-watermark")]
    [InlineData("erased-tamper")]
    [InlineData("restart-before-issuance")]
    public void DurableCursorRetainsActualOrderedPathsInputsAndOutputs(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Case == "Release-durably-applied-watermark-cursor");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        var posts = records.Where(pair => pair.Key.Split('#')[0] == "http-post").Select(pair => (pair.Key, Value: JsonNode.Parse(pair.Value)!.AsObject())).ToArray();
        if (mutation == "unrelated-paths") foreach (var post in posts) post.Value["address"] = "http://127.0.0.1:31000/nothing";
        if (mutation == "foreign-host") posts[0].Value["address"] = "http://127.0.0.1:1/project-model/synthetic";
        if (mutation == "foreign-aggregate") posts[10].Value["address"] = "http://127.0.0.1:31002/cursor-model/foreign";
        if (mutation == "reordered-operations") (posts[0].Value["address"], posts[1].Value["address"]) =
            (JsonValue.Create(posts[1].Value["address"]!.GetValue<string>()), JsonValue.Create(posts[0].Value["address"]!.GetValue<string>()));
        if (mutation == "wrong-position") posts[0].Value["request"]!["position"] = 12;
        if (mutation == "wrong-sequence") posts[9].Value["request"]!["sequence"] = 988;
        if (mutation == "foreign-cursor") posts[2].Value["request"]!["cursorHash"] = new string('0', 64);
        if (mutation == "lost-first-decode") posts[1].Value["response"]!["decoded"] = false;
        if (mutation == "false-advanced-decode") posts[10].Value["response"]!["decoded"] = true;
        if (mutation == "lost-advanced-watermark") posts[10].Value["response"]!["modelWatermark"] = 987;
        if (mutation == "erased-tamper") posts[8].Value["response"]!["cursorHash"] = posts[1].Value["response"]!["cursorHash"]!.DeepClone();
        if (mutation == "restart-before-issuance")
        {
            string timingKey = records.Keys.Single(key => key.Split('#')[0] == "http-get-observation"
                && JsonNode.Parse(records[key])!["address"]!.GetValue<string>() == "http://127.0.0.1:31002/identity");
            var timing = JsonNode.Parse(records[timingKey])!.AsObject();
            timing["began"] = row.Started!.Value; timing["ended"] = row.Started.Value.AddMilliseconds(10); records[timingKey] = timing.ToJsonString();
            int get = fixture.HttpExchanges.FindIndex(exchange => exchange.Method == "GET" && exchange.Address == timing["address"]!.GetValue<string>()
                && exchange.Started >= row.Started && exchange.Ended <= row.Ended);
            fixture.HttpExchanges[get] = fixture.HttpExchanges[get] with { Started = row.Started.Value.AddTicks(1), Ended = row.Started.Value.AddMilliseconds(10).AddTicks(-1) };
        }
        foreach (var post in posts)
        {
            records[post.Key] = post.Value.ToJsonString();
            int actual = fixture.HttpExchanges.FindIndex(exchange => exchange.Started >= post.Value["began"]!.GetValue<DateTimeOffset>()
                && exchange.Ended <= post.Value["ended"]!.GetValue<DateTimeOffset>());
            fixture.HttpExchanges[actual] = fixture.HttpExchanges[actual] with { Address = post.Value["address"]!.GetValue<string>(),
                RequestSemanticsSha256 = Hash(post.Value["request"]!.ToJsonString()), ResponseSemanticsSha256 = Hash(post.Value["response"]!.ToJsonString()) };
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        string cause = mutation switch
        {
            "foreign-aggregate" => "one exact fixture aggregate",
            "unrelated-paths" or "foreign-host" or "reordered-operations" => "ordered POST path",
            "wrong-position" or "wrong-sequence" => "durable model mutation",
            "restart-before-issuance" => "independently reloaded Host",
            _ => "actual cursor replay",
        };
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain(cause);
    }

    /// <summary>Backup hash syntax never substitutes for genuine file observations and the exact owned command chain.</summary>
    [Theory]
    [InlineData("record-hash")]
    [InlineData("record-bytes")]
    [InlineData("missing-save")]
    [InlineData("missing-copy")]
    [InlineData("wrong-restore")]
    [InlineData("missing-restore-observation")]
    [InlineData("changed-bytes")]
    [InlineData("not-destroyed")]
    [InlineData("missing-destruction")]
    [InlineData("destruction-before-restore")]
    [InlineData("foreign-path")]
    [InlineData("saved-fingerprint-drift")]
    public void ProviderBackupRequiresActualFilesAndOwnedSaveRestoreDestruction(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Scenario == "post-upgrade-restore" && row.Case == "3.128.2");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        var backup = JsonNode.Parse(records["provider-backup"])!.AsObject(); string path = backup["path"]!.GetValue<string>();
        if (mutation == "record-hash") backup["hash"] = new string('0', 64);
        if (mutation == "record-bytes") backup["bytes"] = 1;
        if (mutation == "foreign-path") backup["path"] = "/foreign/provider/backup.rdb";
        if (mutation is "missing-save" or "missing-copy" or "wrong-restore")
        {
            int command = fixture.Commands.FindIndex(command => command.Started >= row.Started && command.Ended <= row.Ended
                && (mutation == "missing-save" ? command.Command.Arguments.SequenceEqual(new[] { "exec", "p1r-11111111111111111111111111111111-redis-1", "redis-cli", "SAVE" })
                    : command.Command.Arguments.Length == 3 && command.Command.Arguments[0] == "cp"
                        && command.Command.Arguments[mutation == "missing-copy" ? 2 : 1] == path));
            if (mutation == "wrong-restore") fixture.Commands[command] = fixture.Commands[command] with
                { Command = fixture.Commands[command].Command with { Arguments = ["cp", path, "foreign-redis:/data/dump.rdb"] } };
            else fixture.Commands.RemoveAt(command);
        }
        int before = fixture.Files.FindIndex(file => file.Path == path && file.Purpose == "provider-backup-before-restore");
        int destroyed = fixture.Files.FindIndex(file => file.Path == path && file.Purpose == "provider-backup-destroyed");
        if (mutation == "missing-restore-observation") fixture.Files.RemoveAt(before);
        if (mutation == "changed-bytes") fixture.Files[before] = fixture.Files[before] with { Sha256 = new string('0', 64), Bytes = 1 };
        if (mutation == "not-destroyed") fixture.Files[destroyed] = fixture.Files[destroyed] with { Exists = true, Bytes = 100, Sha256 = backup["hash"]!.GetValue<string>() };
        if (mutation == "missing-destruction") fixture.Files.RemoveAt(destroyed);
        if (mutation == "destruction-before-restore") fixture.Files[destroyed] = fixture.Files[destroyed] with { Started = row.Started!.Value, Ended = row.Started.Value };
        records["provider-backup"] = backup.ToJsonString(); fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        if (mutation == "saved-fingerprint-drift") fixture.Summary["files"]!.AsArray()[0]!["Bytes"] = 1;
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    /// <summary>Neither copied success flags nor erased inventory commands qualify the required two actual cleanup passes.</summary>
    [Theory]
    [InlineData("erased-container-roster")]
    [InlineData("erased-volume-roster")]
    [InlineData("missing-pass")]
    [InlineData("erased-scans")]
    [InlineData("erased-shared-inspect")]
    [InlineData("copied-inventory")]
    [InlineData("changed-shared-image")]
    [InlineData("missing-container-removal")]
    [InlineData("missing-volume-removal")]
    [InlineData("missing-second-absence")]
    [InlineData("failure")]
    [InlineData("remaining-process")]
    [InlineData("foreign-owner")]
    [InlineData("saved-pass-drift")]
    public void MatrixCleanupRequiresActualRepeatedInventoryAndOwnedAbsence(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        if (mutation is "erased-container-roster" or "erased-volume-roster")
        {
            for (int i = 0; i < fixture.CleanupPasses.Count; i++)
                if (fixture.CleanupPasses[i].Owner == "p1r-11111111111111111111111111111111") fixture.CleanupPasses[i] = mutation == "erased-container-roster"
                    ? fixture.CleanupPasses[i] with { ContainerNames = [] } : fixture.CleanupPasses[i] with { VolumeNames = [] };
        }
        else if (mutation == "missing-pass") fixture.CleanupPasses.RemoveAt(1);
        if (mutation == "erased-scans") fixture.Commands.RemoveAll(command => command.Command.Arguments.SequenceEqual(new[] { "ps", "-aq", "--no-trunc" }));
        if (mutation == "erased-shared-inspect") fixture.Commands.RemoveAll(command => command.Command.Arguments.LastOrDefault() == Hash("actual-shared-container"));
        if (mutation == "copied-inventory")
            for (int i = 0; i < fixture.CleanupPasses.Count; i++) fixture.CleanupPasses[i] = fixture.CleanupPasses[i] with { Baseline = "", After = "" };
        if (mutation == "changed-shared-image")
        {
            int index = fixture.Commands.FindLastIndex(command => command.Command.Arguments.LastOrDefault() == Hash("actual-shared-container"));
            var command = fixture.Commands[index]; var data = JsonNode.Parse(command.StandardOutput)!.AsArray(); data[0]!["Image"] = "sha256:" + new string('f', 64);
            string output = data.ToJsonString(); fixture.Commands[index] = command with { StandardOutput = output, OutputHash = Hash(output + command.StandardError) };
        }
        if (mutation == "missing-container-removal") fixture.Commands.RemoveAll(command => command.Command.Arguments.SequenceEqual(new[] { "rm", "-fv", Hash("p1r-11111111111111111111111111111111-redis-1") }));
        if (mutation == "missing-volume-removal") fixture.Commands.RemoveAll(command => command.Command.Arguments.SequenceEqual(new[] { "volume", "rm", "p1r-11111111111111111111111111111111-redis-data-1" }));
        if (mutation == "missing-second-absence")
            fixture.Commands.RemoveAll(command => command.Started >= fixture.CleanupPasses[1].Started
                && command.Command.Arguments.SequenceEqual(new[] { "inspect", "--format", QualificationRuntime.ContainerIdentityFormat, "p1r-11111111111111111111111111111111-redis-1" }));
        if (mutation == "failure") fixture.CleanupPasses[1] = fixture.CleanupPasses[1] with { Failures = 1 };
        if (mutation == "remaining-process") fixture.CleanupPasses[1] = fixture.CleanupPasses[1] with { RemainingProcesses = 1 };
        if (mutation == "foreign-owner") fixture.CleanupPasses[1] = fixture.CleanupPasses[1] with { Owner = "p1r-foreign" };
        fixture.RewriteSummary();
        if (mutation == "saved-pass-drift") fixture.Summary["cleanupPasses"]![0]!["After"] = "";
        Should.Throw<InvalidOperationException>(fixture.Validate);
    }

    private static string Hash(string value) => QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value));
}
