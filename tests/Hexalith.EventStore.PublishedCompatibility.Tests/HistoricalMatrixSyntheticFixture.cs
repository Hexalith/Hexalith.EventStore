using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Builds independent complete synthetic contract-test data, never live qualification evidence.</summary>
internal sealed class HistoricalMatrixSyntheticFixture
{
    /// <summary>Gets synthetic observations from the independently stored complete case fixture.</summary>
    internal List<ScenarioObservation> Observations { get; } = [];
    /// <summary>Gets the separate synthetic executor capture.</summary>
    internal JsonObject Capture { get; } = new()
    {
        ["executor"] = new string('1', 40), ["diffHash"] = new string('2', 64), ["coordinatorHash"] = new string('3', 64),
        ["builds"] = "a22b55f0a4f6d2aaa3a5ccc505896064ac595364", ["consumerInputOrigin"] = "/synthetic/contract-fixture/consumer-inputs",
        ["templateHashes"] = new JsonObject { ["Probe/Program.cs"] = new string('4', 64) }, ["untrackedHashes"] = new JsonObject(),
    };
    /// <summary>Gets saved synthetic results independently mutable from the in-memory observations.</summary>
    internal JsonObject Summary { get; } = new();
    /// <summary>Gets the synthetic prepared consumer inventory.</summary>
    internal List<ConsumerBuild> Builds { get; } = [];
    /// <summary>Gets synthetic completed command observations, including actual input operation fixtures.</summary>
    internal List<CommandObservation> Commands { get; } = [];

    /// <summary>Reads the explicit test fixture without deriving cases or outcomes from the validator.</summary>
    internal static HistoricalMatrixSyntheticFixture Create()
    {
        var fixture = new HistoricalMatrixSyntheticFixture();
        foreach (string version in new[] { "3.70.1", "3.110.0", "3.128.2" })
        foreach (string role in new[] { "Host", "Domain", "Probe" }) fixture.Builds.Add(Build(version, role, false));
        foreach (string role in new[] { "Host", "Domain", "Probe" }) fixture.Builds.Add(Build("3.128.2", role, true));
        string path = Path.Combine(PublishedScenarioTests.FindRepository(), "tests", "Hexalith.EventStore.PublishedCompatibility.Tests",
            "Fixtures", "HistoricalMatrixCharacterizationFixture.json");
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(path));
        int index = 0;
        foreach (JsonElement row in source.RootElement.GetProperty("rows").EnumerateArray()) fixture.Add(row, index++);
        fixture.RewriteSummary();
        return fixture;
    }

    /// <summary>Writes synthetic totals and rows while keeping their separate false qualification claims.</summary>
    internal void RewriteSummary()
    {
        Summary["selected"] = "3.128.2"; Summary["source"] = "935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8";
        Summary["builds"] = "db897eac9f2819220b32d237109d7255218b46a9";
        Summary["executor"] = Capture["executor"]!.DeepClone(); Summary["executorDiffHash"] = Capture["diffHash"]!.DeepClone();
        Summary["executorInputs"] = Capture.DeepClone();
        Summary["qualified"] = false; Summary["p1rUsable"] = false; Summary["rollback"] = null;
        Summary["owners"] = "pending"; Summary["conformance"] = "pending";
        Summary["assertions"] = Observations.Sum(row => row.Assertions);
        Summary["passed"] = Observations.Count(row => row.Execution == "passed");
        Summary["failed"] = Observations.Count(row => row.Execution == "failed");
        Summary["unavailable"] = Observations.Count(row => row.Execution == "unavailable");
        Summary["incompatible"] = Observations.Count(row => row.Compatibility == "incompatible");
        Summary["observations"] = JsonSerializer.SerializeToNode(Observations);
    }

    /// <summary>Runs the complete contract against independent synthetic saved and actual captures.</summary>
    internal void Validate()
    {
        using JsonDocument summary = JsonDocument.Parse(Summary.ToJsonString());
        using JsonDocument capture = JsonDocument.Parse(Capture.ToJsonString());
        HistoricalMatrixMeasurementContract.Validate(Observations, summary.RootElement, capture.RootElement, Builds, Commands.Select(HistoricalExecutionCommand.Capture).ToArray());
    }

    private void Add(JsonElement row, int index)
    {
        string scenario = row.GetProperty("Scenario").GetString()!;
        string name = row.GetProperty("Case").GetString()!;
        var records = new Dictionary<string, string>();
        DateTimeOffset began = DateTimeOffset.UnixEpoch.AddMinutes(index);
        JsonElement[] profiles = row.GetProperty("Profiles").EnumerateArray().ToArray();
        for (int i = 0; i < profiles.Length; i++)
        {
            ConsumerBuild host = Find(profiles[i].GetProperty("host").GetString()!);
            ConsumerBuild domain = Find(profiles[i].GetProperty("domain").GetString()!);
            records[Ordinal("runtime-profile", i)] = JsonSerializer.Serialize(new
            {
                appId = "p1r-synthetic", runtime = "1.18.4", redisImage = PublishedCluster.RedisImage,
                redisContainer = "p1r-synthetic-redis-1", componentHash = Hash("component"), daprdHash = Hash("daprd"), host, domain,
            });
            records[Ordinal("loaded-Host", i)] = Identities(host);
            records[Ordinal("loaded-Domain", i)] = Identities(domain);
        }
        foreach (JsonProperty kind in row.GetProperty("Records").EnumerateObject())
        for (int i = 0; i < kind.Value.GetInt32(); i++)
        {
            string key = Ordinal(kind.Name, i);
            if (records.ContainsKey(key)) continue;
            object value = new { synthetic = true };
            if (kind.Name is "consumer-build" or "probe-command")
            {
                ConsumerBuild build = ProbeBuild(scenario, name, i);
                if (kind.Name == "consumer-build") value = build;
                else
                {
                    var command = new QualificationCommand("dotnet", build.Role == "Probe" ? [build.Assembly] : [build.Assembly, "--identity"],
                        Path.GetDirectoryName(build.Project)!, TimeSpan.FromSeconds(30), Input: "{\"operation\":\"identity\"}");
                    var completed = new CommandObservation(command, began.AddSeconds(i + 1), began.AddSeconds(i + 1.5), 0, "synthetic", "", Hash("synthetic"));
                    Commands.Add(completed);
                    value = new { Command = command, completed.Started, completed.Ended, completed.ExitCode, completed.OutputHash };
                }
            }
            else if (kind.Name == "inventory")
            {
                key = Ordinal("inventory:tenant-a:counter:synthetic", i);
                value = new { fixture = "tenant-a:counter:synthetic", appId = "p1r-synthetic", database = "p1r-synthetic-redis-1",
                    hashes = new Dictionary<string, string?> { ["tenant-a:counter:synthetic:metadata"] = Hash("metadata"),
                        ["tenant-a:counter:synthetic:snapshot"] = null, ["tenant-a:counter:synthetic:events:1"] = Hash("event") } };
            }
            else if (kind.Name == "http-get") key = "http-get:http://127.0.0.1:" + (31000 + i) + "/identity";
            else if (kind.Name == "http-post") value = new { address = "http://127.0.0.1:31000/execute", began, ended = began.AddSeconds(1), requestHash = Hash("request"), responseHash = Hash("response"), status = 200 };
            else if (kind.Name == "constructed-metadata") value = new { currentSequence = 12, eTag = "old", lastModified = DateTimeOffset.UnixEpoch };
            else if (kind.Name == "provider-backup") value = new { sourceContainer = "p1r-synthetic-redis-1", hash = Hash("backup"), bytes = 100 };
            else if (kind.Name == "provider-created-before-interruption") value = new[] { new { Id = Hash("container"), Name = "synthetic-container", Image = Hash("image"), Config = new { Labels = new Dictionary<string, string> { ["p1r.owner"] = "p1r-synthetic" } }, State = new { Running = false } } };
            else if (kind.Name == "production-manifest-refusal")
            {
                // The real manifest fixture uses its own domain/address. This synthetic witness
                // keeps the same owner binding rather than claiming a successful protected hop.
                value = new { observed = true, cause = new { actor = "tenant-a:counter:synthetic", code = "missing-allow-listed-callable",
                    stage = "production-logical-event-upcast-binding", declaringType = "Hexalith.EventStore.Client.Events.EventUpcastChainExecutor+Synthetic" } };
            }
            else if (kind.Name == "actual-writer-admission-result") value = new { refused = false, code = "synthetic", stage = "synthetic", protectionCalls = 1, allocations = 1, staging = 1 };
            else if (kind.Name == "actual-versioned-reader-result") value = new { ok = false };
            else if (kind.Name == "production-manifest-command-result") value = new { count = 1, deadLetterBookkeeping = 0, error = "synthetic", message = "synthetic", metadata = new { synthetic = true }, ok = false, sequence = 1 };
            else if (kind.Name == "actual-host-protected-contract-refusal") value = new { actualType = "SyntheticRefusal", code = "synthetic", stage = "synthetic", message = "synthetic", requestBody = "synthetic" };
            else if (kind.Name == "production-handler-discovery") value = new { declared = true, persisted = true, registry = new { synthetic = true }, supported = true, unknown = false };
            else if (kind.Name.StartsWith("production-handler-", StringComparison.Ordinal)) value = new { errorMessage = "synthetic", invoker = new { synthetic = true }, payload = new { synthetic = true }, router = new { synthetic = true }, success = false };
            else if (kind.Name.StartsWith("cross-client-", StringComparison.Ordinal)) value = new { action = kind.Name["cross-client-".Length..], identities = new object[] { new { synthetic = true } }, ok = true, outcome = new { synthetic = true }, supported = true, actualType = "SyntheticRefusal", cause = "synthetic", error = "synthetic", message = "synthetic" };
            else if (kind.Name == "bounded-reminder-registration-restart-window") value = new
            { preparationStarted = began, preparationFinished = began.AddSeconds(1), due = began.AddSeconds(10), preparationBudgetSeconds = 5, naturalDeliveryMarginSeconds = 1 };
            else if (kind.Name == "capability-security-observations")
            {
                var proof = new { expected = true, code = "synthetic", stage = "synthetic", before = new { synthetic = true }, after = new { synthetic = true } };
                value = new { supported = true, stale = proof, forged = proof, completedStale = proof, unauthorized = proof, forgedEffect = proof };
            }
            records[key] = JsonSerializer.Serialize(value);
        }
        string? error = null;
        if (row.GetProperty("XmlError").GetBoolean())
        {
            string[] pair = name[..^4].Split("-to-", StringSplitOptions.None);
            string consumer = pair[0].StartsWith("3.128.2", StringComparison.Ordinal) ? pair[1] : pair[0];
            ConsumerBuild build = Find(consumer);
            string input = JsonSerializer.Serialize(new { operation = consumer == pair[0] ? "make-wire" : "wire", kind = "projection", serializer = "xml", value = "synthetic" });
            string diagnostic = "Unhandled exception. System.Runtime.Serialization.InvalidDataContractException: Type 'Hexalith.EventStore.Contracts.Projections.ProjectionEventDto' cannot be serialized.";
            var command = new QualificationCommand("dotnet", [build.Assembly], Path.GetDirectoryName(build.Project)!, TimeSpan.FromSeconds(30), Input: input);
            Commands.Add(new(command, began.AddSeconds(25), began.AddSeconds(26), 134, "", diagnostic, Hash(diagnostic)));
            error = $"System.InvalidOperationException: Exit 134: dotnet {build.Assembly}\n\n{diagnostic}";
        }
        Observations.Add(new(scenario, name, row.GetProperty("Execution").GetString()!, row.GetProperty("Compatibility").GetString()!,
            row.GetProperty("Assertions").GetInt32(), row.GetProperty("Failures").EnumerateArray().Select(value => value.GetString()!).ToArray(),
            error, records, began, began.AddSeconds(30)));
    }

    private ConsumerBuild ProbeBuild(string scenario, string name, int index)
    {
        if (scenario == "provenance") return Find(name);
        if (name.Contains("-to-", StringComparison.Ordinal) && !name.StartsWith("client-", StringComparison.Ordinal))
        {
            string[] pair = name[..^(name.EndsWith("-xml", StringComparison.Ordinal) ? 4 : 5)].Split("-to-", StringSplitOptions.None);
            return Find(index % 3 == 1 ? pair[1] : pair[0]);
        }
        if (name.Contains("-source-Probe", StringComparison.Ordinal)) return Find("3.128.2-source-Probe");
        string version = name.StartsWith("3.70.1", StringComparison.Ordinal) ? "3.70.1" : name.StartsWith("3.110.0", StringComparison.Ordinal) ? "3.110.0" : "3.128.2";
        return Find(version + "-package-Probe");
    }

    private ConsumerBuild Find(string key) => Builds.Single(build => build.Version + (build.Source is null ? "-package-" : "-source-") + build.Role == key);
    private static string Ordinal(string key, int index) => index == 0 ? key : key + "#" + (index + 1);
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Identities(ConsumerBuild build) => JsonSerializer.Serialize(new { identities = build.ResolvedVersions!.Select(pair => new
    { name = pair.Key, version = pair.Value + ".0", informationalVersion = pair.Value + (build.Source is null ? "+" + build.ArchiveSources![pair.Key] : ""),
        configuration = build.Configuration, path = Path.Combine(Path.GetDirectoryName(build.Assembly)!, pair.Key + ".dll"), sha256 = build.VerifiedAssemblyHashes![pair.Key] }) });
    private static ConsumerBuild Build(string version, string role, bool source)
    {
        string key = version + (source ? "-source-" : "-package-") + role;
        string root = "/synthetic/contract-fixture/" + key;
        string configuration = source ? "Debug" : "Release";
        string[] names = new[] { "Client", "Contracts", "DomainService", "Server", "ServiceDefaults" }
            .Concat(role == "Host" && version == "3.128.2" ? ["Gateway"] : []).Select(name => "Hexalith.EventStore." + name).ToArray();
        return new(role, version, configuration, root + "/" + role + ".csproj", root + "/bin/" + configuration + "/net10.0/" + role + ".dll", root + "/packages",
            Hash("assets"), Hash("lock"), source ? "935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8" : null,
            names.ToDictionary(name => name, _ => "935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8"), names.ToDictionary(name => name, _ => source ? "3.128.0" : version),
            Hash("project"), names.ToDictionary(name => name, Hash), Hash("sdk-policy"), "10.0.301", "/synthetic/global.json");
    }
}
