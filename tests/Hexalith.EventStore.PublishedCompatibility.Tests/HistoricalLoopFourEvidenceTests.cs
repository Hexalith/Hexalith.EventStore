using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Independent coherent loop4 counterexamples; none is live qualification evidence.</summary>
public sealed class HistoricalLoopFourEvidenceTests
{
    [Fact]
    public void DeclarationCaptureOmitsUnexpectedRawFields()
    {
        string body = "{\"declared\":[\"get-counter\"],\"persisted\":[\"get-counter\"],\"registry\":\"Hexalith.EventStore.Queries.DaprDomainQueryHandlerRegistry\",\"supported\":true,\"unknown\":false,\"unexpected\":\"private-material-never-retained\"}";
        JsonSerializer.Serialize(HistoricalHttpOperationCapture.Response(body)).ShouldNotContain("private-material-never-retained");
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("sidecar")]
    public void CrossClientMustUseItsActualOwnedDestination(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Commands.FindIndex(command => command.Command.Input?.Contains("cross-client", StringComparison.Ordinal) == true);
        var command = fixture.Commands[index]; var input = JsonNode.Parse(command.Command.Input!)!;
        input[field] = field == "actor" ? "foreign:counter:actor" : "http://127.0.0.1:1";
        fixture.Commands[index] = command with { Command = command.Command with { Input = input.ToJsonString() } };
        fixture.RewriteSummary(); Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("cross-client destination");
    }

    [Theory]
    [InlineData("daprdHash")]
    [InlineData("componentHash")]
    [InlineData("sidecarProcessId")]
    public void RuntimeProfileRequiresActualPreparedInputsAndLaunch(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        Rewrite(fixture, "full-replay", null, records =>
        {
            string key = records.Keys.First(key => key.Split('#')[0] == "runtime-profile"); var profile = JsonNode.Parse(records[key])!;
            if (field == "sidecarProcessId") profile[field] = 999999; else profile[field] = new string('0', 64);
            records[key] = profile.ToJsonString();
        });
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain(field == "sidecarProcessId" ? "actual primary sidecar launch" : "independently prepared");
    }

    [Theory]
    [InlineData("hashes")]
    [InlineData("target")]
    [InlineData("status")]
    [InlineData("value")]
    [InlineData("missing")]
    public void InventoryRequiresActualReadTargetStatusAndValueBytes(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int row = fixture.Observations.FindIndex(item => item.Scenario == "full-replay");
        var original = fixture.Observations[row]; var records = new Dictionary<string, string>(original.Measurements!);
        string key = records.Keys.First(key => key.StartsWith("inventory:", StringComparison.Ordinal)); var data = JsonNode.Parse(records[key])!;
        string id = data["readIds"]![0]!.GetValue<string>(); int index = fixture.StateReads.FindIndex(read => read.Id == id);
        if (field == "hashes")
        {
            foreach (string inventory in records.Keys.Where(key => key.StartsWith("inventory:", StringComparison.Ordinal)).ToArray())
            {
                var value = JsonNode.Parse(records[inventory])!;
                foreach (var property in value["hashes"]!.AsObject().ToArray()) if (property.Value is not null) value["hashes"]![property.Key] = Hash(property.Key);
                records[inventory] = value.ToJsonString();
            }
        }
        else if (field == "target") fixture.StateReads[index] = fixture.StateReads[index] with { Address = "http://127.0.0.1:1/read/foreign/key" };
        else if (field == "status") fixture.StateReads[index] = fixture.StateReads[index] with { Status = 404 };
        else if (field == "value") fixture.StateReads[index] = fixture.StateReads[index] with { ValueSha256 = new string('0', 64) };
        else fixture.StateReads.RemoveAt(index);
        if (field is "target" or "status")
        {
            string readId = data["readIds"]![0]!.GetValue<string>(); var changedRead = fixture.StateReads.Single(read => read.Id == readId);
            int exchange = fixture.HttpExchanges.FindIndex(exchange => exchange.Method == "GET" && exchange.Started >= changedRead.Started && exchange.Ended <= changedRead.Ended);
            fixture.HttpExchanges[exchange] = fixture.HttpExchanges[exchange] with { Address = changedRead.Address, Status = changedRead.Status };
        }
        fixture.Observations[row] = original with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain(field == "missing" ? "inventory read is absent" : "actual owned state-read");
    }

    [Theory]
    [InlineData("all-child-passes")]
    [InlineData("one-pass")]
    [InlineData("process-scan")]
    [InlineData("remaining-process")]
    [InlineData("shared-inventory")]
    [InlineData("third-roster")]
    [InlineData("third-baseline")]
    public void InterruptedChildrenRequireTheirActualDisposalAndSharedProcessObservations(string field)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var observation = fixture.Observations.Single(item => item.Case == "timeout-and-detached-descendant");
        string owner = JsonSerializer.Deserialize<JsonElement>(observation.Measurements!["cleanup-runtime"]).GetProperty("owner").GetString()!;
        int index = fixture.CleanupPasses.FindIndex(pass => pass.Owner == owner);
        if (field == "all-child-passes") fixture.CleanupPasses.RemoveAll(pass => pass.Owner == owner);
        else if (field == "one-pass") fixture.CleanupPasses.RemoveAt(index);
        else if (field == "third-roster") fixture.CleanupPasses[index + 2] = fixture.CleanupPasses[index + 2] with { ContainerNames = Array.Empty<string>() };
        else if (field == "third-baseline") fixture.CleanupPasses[index + 2] = fixture.CleanupPasses[index + 2] with { Baseline = "foreign" };
        else if (field == "shared-inventory") fixture.CleanupPasses[index] = fixture.CleanupPasses[index] with { After = "foreign" };
        else
        {
            int scan = fixture.ProcessScans.FindIndex(scan => scan.Id == fixture.CleanupPasses[index].ProcessScanId);
            if (field == "process-scan") fixture.ProcessScans.RemoveAt(scan);
            else fixture.ProcessScans[scan] = fixture.ProcessScans[scan] with { Pids = new[] { 999999 } };
        }
        fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain(field switch
        {
            "process-scan" or "remaining-process" => "owned-process observation",
            "shared-inventory" => "shared container identity",
            _ => "genuine independent cleanup passes",
        });
    }

    [Fact]
    public void EmptyPhysicalDeclarationResponseCannotCarryUnchangedDiscoveryCredit()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        Rewrite(fixture, null, "Release-production-declared-handler-discovery-transport", records =>
        {
            var post = JsonNode.Parse(records["http-post"])!.AsObject(); post["responseHash"] = Hash("{}");
            post["response"] = JsonSerializer.SerializeToNode(HistoricalHttpOperationCapture.Response("{}"));
            BindExchange(fixture, post); records["http-post"] = post.ToJsonString();
        });
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("declaration POST");
    }

    [Fact]
    public void CoherentSecurityReaderPidCannotReplaceTheActualHostPid()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        Rewrite(fixture, null, "3.128.2-Release", records =>
        {
            var capture = JsonNode.Parse(records["capability-security-capture"])!; capture["pid"] = 999999;
            foreach (var snapshot in capture["snapshots"]!.AsArray()) snapshot!["pid"] = 999999;
            records["capability-security-capture"] = capture.ToJsonString();
            string path = capture["path"]!.GetValue<string>();
            int index = fixture.Commands.FindIndex(command => command.Command.Arguments.Contains(path, StringComparer.Ordinal));
            var old = fixture.Commands[index]; var args = old.Command.Arguments.ToArray(); args[^1] = "999999";
            string output = capture["snapshots"]!.ToJsonString();
            fixture.Commands[index] = old with { Command = old.Command with { Arguments = args }, StandardOutput = output, OutputHash = Hash(output + old.StandardError) };
        });
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("runtime Host PID");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void ManifestRefusalRequiresTheActualAttemptedAppend(bool? append)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        Rewrite(fixture, null, "Release-registered-manifest-production-hop", records =>
        {
            var post = JsonNode.Parse(records["http-post"])!.AsObject(); post["request"]!["append"] = append;
            post["requestHash"] = Hash(JsonSerializer.Serialize(new { append }));
            BindExchange(fixture, post); records["http-post"] = post.ToJsonString();
        });
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("manifest command");
    }

    private static void BindExchange(HistoricalMatrixSyntheticFixture fixture, JsonObject post)
    {
        int index = fixture.HttpExchanges.FindIndex(exchange => exchange.Address == post["address"]!.GetValue<string>()
            && exchange.Started >= post["began"]!.GetValue<DateTimeOffset>() && exchange.Ended <= post["ended"]!.GetValue<DateTimeOffset>());
        fixture.HttpExchanges[index] = fixture.HttpExchanges[index] with { RequestSha256 = post["requestHash"]!.GetValue<string>(),
            ResponseSha256 = post["responseHash"]!.GetValue<string>(), RequestSemanticsSha256 = Hash(post["request"]!.ToJsonString()),
            ResponseSemanticsSha256 = Hash(post["response"]?.ToJsonString() ?? "null") };
    }

    private static void Rewrite(HistoricalMatrixSyntheticFixture fixture, string? scenario, string? name, Action<Dictionary<string, string>> edit)
    {
        int index = fixture.Observations.FindIndex(item => scenario is not null ? item.Scenario == scenario : item.Case == name);
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!); edit(records);
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
    }

    private static string Hash(string value) => QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value));
}
