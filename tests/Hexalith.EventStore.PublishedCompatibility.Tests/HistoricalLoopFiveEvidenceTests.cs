using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Coherent focused counterexamples for the separately triaged loop5 bindings, never live qualification.</summary>
public sealed class HistoricalLoopFiveEvidenceTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("body")]
    [InlineData("status")]
    [InlineData("address")]
    public void ConsumedGetRequiresItsIndependentPhysicalTransportReceipt(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var row = fixture.Observations.First(row => row.Scenario == "full-replay");
        var timing = JsonSerializer.Deserialize<JsonElement>(row.Measurements!.First(pair => pair.Key.Split('#')[0] == "http-get-observation").Value);
        int index = fixture.HttpExchanges.FindIndex(exchange => exchange.Method == "GET" && exchange.Address == timing.GetProperty("address").GetString()
            && exchange.Started >= timing.GetProperty("began").GetDateTimeOffset() && exchange.Ended <= timing.GetProperty("ended").GetDateTimeOffset());
        if (mutation == "missing") fixture.HttpExchanges.RemoveAt(index);
        else fixture.HttpExchanges[index] = mutation switch
        {
            "body" => fixture.HttpExchanges[index] with { ResponseSha256 = new string('0', 64) },
            "status" => fixture.HttpExchanges[index] with { Status = 500 },
            _ => fixture.HttpExchanges[index] with { Address = "http://127.0.0.1:1/identity" },
        };
        fixture.RewriteSummary(); Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("GET record lacks");
    }

    [Theory]
    [InlineData("pid")]
    [InlineData("address")]
    [InlineData("assembly")]
    [InlineData("lifetime")]
    [InlineData("program")]
    [InlineData("probe-lifetime")]
    public void ManagedIdentityAndOperationLifetimeMustComeFromTheActualLaunch(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var row = fixture.Observations.First(row => row.Scenario == "full-replay");
        string id = JsonSerializer.Deserialize<JsonElement>(row.Measurements!["runtime-profile"]).GetProperty("profileId").GetString()!;
        int index = mutation == "probe-lifetime" ? fixture.ManagedLaunches.FindIndex(launch => launch.Profile is null)
            : fixture.ManagedLaunches.FindIndex(launch => launch.Profile == id && launch.Role == "Host");
        var launch = fixture.ManagedLaunches[index];
        fixture.ManagedLaunches[index] = mutation switch
        {
            "pid" => launch with { Pid = 999999 },
            "address" => launch with { Address = "http://127.0.0.1:1" },
            "assembly" => launch with { Assembly = "/foreign/Host.dll" },
            "lifetime" => launch with { Ended = row.Started!.Value.AddSeconds(19) },
            "probe-lifetime" => launch with { Ended = launch.Started.AddTicks(-1) },
            _ => launch with { ExecutableHashes = launch.ExecutableHashes.ToDictionary(pair => pair.Key, pair => pair.Key.EndsWith(".dll", StringComparison.Ordinal) ? new string('0', 64) : pair.Value) },
        };
        fixture.RewriteSummary(); Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain(mutation switch
        {
            "program" => "prepared executable identity",
            "lifetime" => "consumer lifetime",
            "probe-lifetime" => "bounded process lifetime",
            _ => "managed consumer launch",
        });
    }

    [Theory]
    [InlineData("endpoint")]
    [InlineData("prefix")]
    [InlineData("actor")]
    [InlineData("type")]
    public void ActualComponentSemanticsCannotBeInferredFromItsDigest(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.ScenarioInputs.FindIndex(input => input.Kind == "component"); var input = fixture.ScenarioInputs[index];
        fixture.ScenarioInputs[index] = input with { Component = mutation switch
        {
            "endpoint" => input.Component! with { RedisAddress = "127.0.0.1:1" },
            "prefix" => input.Component! with { KeyPrefix = "none" },
            "actor" => input.Component! with { ActorStateStore = false },
            _ => input.Component! with { Type = "state.foreign" },
        } };
        fixture.RewriteSummary(); Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("backend semantics");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("start-before-copy")]
    [InlineData("copy-before-create")]
    [InlineData("reused-volume")]
    public void RestoreRequiresItsFreshOwnedTargetAndCopyBeforeStart(string mutation)
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Scenario == "post-upgrade-restore"); var row = fixture.Observations[index];
        var records = new Dictionary<string, string>(row.Measurements!);
        var profile = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonNode.Parse(pair.Value)!).First(profile => profile["redisContainer"]!.GetValue<string>().Contains("restore-", StringComparison.Ordinal));
        string target = profile["redisContainer"]!.GetValue<string>(), volume = profile["redisVolume"]!.GetValue<string>();
        int create = fixture.Commands.FindIndex(command => command.Command.Arguments.Take(3).SequenceEqual(new[] { "create", "--name", target }));
        int copy = fixture.Commands.FindIndex(command => command.Command.Arguments.FirstOrDefault() == "cp" && command.Command.Arguments.Last() == target + ":/data/dump.rdb");
        if (mutation == "create") fixture.Commands.RemoveAt(create);
        else if (mutation == "start-before-copy")
        {
            int start = fixture.Commands.FindIndex(command => command.Command.Arguments.SequenceEqual(new[] { "start", target }));
            fixture.Commands[start] = fixture.Commands[start] with { Started = row.Started!.Value.AddSeconds(3), Ended = row.Started.Value.AddSeconds(3.1) };
        }
        else if (mutation == "copy-before-create")
        {
            string path = fixture.Commands[copy].Command.Arguments[1]; int file = fixture.Files.FindIndex(file => file.Path == path && file.Purpose == "provider-backup-before-restore");
            fixture.Files[file] = fixture.Files[file] with { Started = row.Started!.Value.AddSeconds(2.55), Ended = row.Started.Value.AddSeconds(2.56) };
            fixture.Commands[copy] = fixture.Commands[copy] with { Started = row.Started!.Value.AddSeconds(2.57), Ended = row.Started.Value.AddSeconds(2.58) };
        }
        else
        {
            string sourceVolume = JsonNode.Parse(records["runtime-profile"])!["redisVolume"]!.GetValue<string>();
            foreach (string key in records.Keys.Where(key => key.Split('#')[0] == "runtime-profile").ToArray())
            { var value = JsonNode.Parse(records[key])!; if (value["redisContainer"]!.GetValue<string>() == target) { value["redisVolume"] = sourceVolume; records[key] = value.ToJsonString(); } }
            var command = fixture.Commands[create]; var args = command.Command.Arguments.Select(arg => arg.Replace("source=" + volume + ",", "source=" + sourceVolume + ",", StringComparison.Ordinal)).ToArray();
            fixture.Commands[create] = command with { Command = command.Command with { Arguments = args } };
            fixture.Commands.RemoveAll(command => command.Command.Arguments.SequenceEqual(new[] { "volume", "create", "--label", "p1r.owner=" + profile["owner"]!.GetValue<string>(), volume }));
        }
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain(mutation == "create" ? "fresh target command sequence" : "genuinely fresh");
    }

    [Fact]
    public void CoherentProtectedCaptureCannotBorrowAnotherOwnerAndRoot()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var row = fixture.Observations.First(row => row.Measurements?.ContainsKey("capability-security-capture") == true); var records = new Dictionary<string, string>(row.Measurements!);
        var capture = JsonNode.Parse(records["capability-security-capture"])!; string path = capture["path"]!.GetValue<string>();
        string owner = "p1r-22222222222222222222222222222222", root = "/synthetic/contract-fixture/" + owner, changed = root + "/" + Path.GetFileName(path);
        capture["owner"] = owner; capture["path"] = changed; foreach (var snapshot in capture["snapshots"]!.AsArray()) snapshot!["owner"] = owner;
        int index = fixture.Commands.FindIndex(command => command.Command.Arguments.Contains(path, StringComparer.Ordinal)); var command = fixture.Commands[index];
        var args = command.Command.Arguments.Select(arg => arg == path ? changed : arg == JsonNode.Parse(records["capability-security-capture"])!["owner"]!.GetValue<string>() ? owner : arg).ToArray();
        string output = capture["snapshots"]!.ToJsonString(); fixture.Commands[index] = command with { Command = command.Command with { Arguments = args, WorkingDirectory = root }, StandardOutput = output, OutputHash = Hash(output + command.StandardError) };
        records["capability-security-capture"] = capture.ToJsonString();
        int rowIndex = fixture.Observations.IndexOf(row); fixture.Observations[rowIndex] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("runtime Host PID/owner/root");
    }

    [Fact]
    public void StatsContractRequiresTheActualDomainEndpointEvenWithCoherentReceipts()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        var row = fixture.Observations.Single(row => row.Case == "Release-production-declared-handler-discovery-transport"); var records = new Dictionary<string, string>(row.Measurements!);
        foreach (string key in records.Keys.Where(key => key.StartsWith("http-get:", StringComparison.Ordinal) && key.Contains("/stats", StringComparison.Ordinal)).ToArray())
        { string changed = "http-get:http://127.0.0.1:1/stats" + (key.Contains('#', StringComparison.Ordinal) ? key[key.IndexOf('#')..] : ""); records[changed] = records[key]; records.Remove(key); }
        foreach (string key in records.Keys.Where(key => key.Split('#')[0] == "http-get-observation").ToArray())
        { var value = JsonNode.Parse(records[key])!; if (value["address"]!.GetValue<string>().EndsWith("/stats", StringComparison.Ordinal)) { value["address"] = "http://127.0.0.1:1/stats"; records[key] = value.ToJsonString(); } }
        for (int i = 0; i < fixture.HttpExchanges.Count; i++)
        {
            var exchange = fixture.HttpExchanges[i];
            if (exchange.Method == "GET" && exchange.Address.EndsWith("/stats", StringComparison.Ordinal) && exchange.Started >= row.Started && exchange.Ended <= row.Ended)
                fixture.HttpExchanges[i] = exchange with { Address = "http://127.0.0.1:1/stats" };
        }
        // Isolate this direct endpoint guard from the additional managed-launch guard.
        Should.Throw<InvalidOperationException>(() => HistoricalMatrixHttpContract.Validate(row, records)).Message.ShouldContain("domainAddress /stats");
    }

    [Fact]
    public void GenuineTransportForAnotherReaderActorCannotBorrowTheOriginalInventory()
    {
        var fixture = HistoricalMatrixSyntheticFixture.Create(); fixture.Validate();
        int index = fixture.Observations.FindIndex(row => row.Case == "3.128.2-Release-floor-0-tenant-a");
        var row = fixture.Observations[index]; var records = new Dictionary<string, string>(row.Measurements!);
        string key = records.Keys.Single(key => key.StartsWith("http-get:", StringComparison.Ordinal) && key.Contains("/reader/", StringComparison.Ordinal));
        string before = key["http-get:".Length..], actor = "tenant-a:counter:other", after = new Uri(before).GetLeftPart(UriPartial.Authority) + "/reader/" + Uri.EscapeDataString(actor);
        records["http-get:" + after] = records[key]; records.Remove(key);
        foreach (string timing in records.Keys.Where(key => key.Split('#')[0] == "http-get-observation").ToArray())
        { var value = JsonNode.Parse(records[timing])!; if (value["address"]!.GetValue<string>() == before) { value["address"] = after; records[timing] = value.ToJsonString(); } }
        for (int i = 0; i < fixture.HttpExchanges.Count; i++)
        { var exchange = fixture.HttpExchanges[i]; if (exchange.Method == "GET" && exchange.Address == before && exchange.Started >= row.Started && exchange.Ended <= row.Ended) fixture.HttpExchanges[i] = exchange with { Address = after }; }
        var target = JsonNode.Parse(records["actual-reader-target"])!; target["actor"] = actor; target["address"] = after; records["actual-reader-target"] = target.ToJsonString();
        fixture.Observations[index] = row with { Measurements = records }; fixture.RewriteSummary();
        Should.Throw<InvalidOperationException>(fixture.Validate).Message.ShouldContain("exact persisted actor");
    }

    private static string Hash(string value) => QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value));
}
