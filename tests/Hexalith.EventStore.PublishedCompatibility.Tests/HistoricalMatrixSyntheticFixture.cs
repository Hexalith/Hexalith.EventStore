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
        ["builds"] = "d21e366bc5450ec636ab16a5177a1fea7bf8ee9b", ["consumerInputOrigin"] = "/synthetic/contract-fixture/consumer-inputs",
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
        for (int i = 0; i < RecordCount(row, profiles, kind); i++)
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
                        Path.GetDirectoryName(build.Project)!, TimeSpan.FromSeconds(30), Input: ProbeInput(scenario, name, i));
                    string output = scenario == "mixed-api" && name.StartsWith("client-", StringComparison.Ordinal)
                        ? "P1R_RESULT " + JsonSerializer.Serialize(CrossClient(name, CrossAction(name, i), build)) : "synthetic";
                    var completed = new CommandObservation(command, began.AddSeconds(i + 1), began.AddSeconds(i + 1.5), 0, output, "", Hash(output));
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
            else if (kind.Name == "http-get")
            {
                var get = HttpGet(row, profiles, i, RecordCount(row, profiles, kind));
                key = get.Key; value = get.Value;
            }
            else if (kind.Name == "http-post") value = new { address = "http://127.0.0.1:31000/execute", began, ended = began.AddSeconds(1), requestHash = Hash("request"), responseHash = Hash("response"), status = 200 };
            else if (kind.Name == "constructed-metadata") value = new { currentSequence = 12, eTag = "old", lastModified = DateTimeOffset.UnixEpoch };
            else if (kind.Name == "provider-backup") value = new { sourceContainer = "p1r-synthetic-redis-1", hash = Hash("backup"), bytes = 100 };
            else if (kind.Name == "provider-created-before-interruption") value = ProviderCreation(name, began);
            else if (kind.Name == "production-manifest-refusal")
            {
                // The real manifest fixture uses its own domain/address. This synthetic witness
                // keeps the same owner binding rather than claiming a successful protected hop.
                value = new { observed = true, cause = new { actor = "tenant-a:counter:synthetic", code = "missing-allow-listed-callable",
                    stage = "production-logical-event-upcast-binding", declaringType = "Hexalith.EventStore.Client.Events.EventUpcastChainExecutor+Synthetic" } };
            }
            else if (kind.Name == "actual-writer-admission-result")
            {
                bool selected = name.StartsWith("3.128.2-", StringComparison.Ordinal);
                value = new { refused = selected, code = selected ? "SourceHeadChanged" : "unexpected-fault",
                    stage = selected ? "event-persister-metadata-floor-admission" : "unverified",
                    error = "System.InvalidOperationException", message = "SourceHeadChanged: actor metadata has an invalid retained floor.",
                    protectionCalls = selected ? 0 : 1, allocations = selected ? 0 : 1, staging = selected ? 0 : 1 };
            }
            else if (kind.Name == "actual-versioned-reader-result") value = ReaderResult(name);
            else if (kind.Name == "production-manifest-command-result") value = new { count = 1, deadLetterBookkeeping = 0, error = "synthetic", message = "synthetic", metadata = new { synthetic = true }, ok = false, sequence = 1 };
            else if (kind.Name == "actual-host-protected-contract-refusal") value = new { actualType = "SyntheticRefusal", code = "synthetic", stage = "synthetic", message = "synthetic", requestBody = "synthetic" };
            else if (kind.Name == "production-handler-discovery") value = new { declared = true, persisted = true, registry = new { synthetic = true }, supported = true, unknown = false };
            else if (kind.Name.StartsWith("production-handler-", StringComparison.Ordinal)) value = new { errorMessage = "synthetic", invoker = new { synthetic = true }, payload = new { synthetic = true }, router = new { synthetic = true }, success = false };
            else if (kind.Name.StartsWith("cross-client-", StringComparison.Ordinal)) value = CrossClient(name, kind.Name["cross-client-".Length..], ProbeBuild(scenario, name, 0));
            else if (kind.Name == "bounded-reminder-registration-restart-window") value = new
            { preparationStarted = began, preparationFinished = began.AddSeconds(1), due = began.AddSeconds(80), preparationBudgetSeconds = 60, naturalDeliveryMarginSeconds = 20 };
            else if (kind.Name == "capability-security-observations")
            {
                object Refusal(string code, string stage, string message)
                {
                    var state = new Dictionary<string, string?> { ["tenant-a:counter:synthetic:metadata"] = Hash("metadata"),
                        ["tenant-a:counter:synthetic:snapshot"] = null, ["tenant-a:counter:synthetic:events:1"] = Hash("event") };
                    return new { expected = true, actualType = "System.InvalidOperationException", code, stage, message, before = state, after = new Dictionary<string, string?>(state) };
                }
                value = new { supported = true,
                    stale = Refusal("stale-execution-authority", "execution-authority", "The idempotency execution authority is no longer current."),
                    forged = Refusal("invalid-execution-fence", "execution-fence-proof", "The idempotency execution fence is missing, stale, or invalid."),
                    completedStale = Refusal("completed-execution-authority", "execution-authority", "The idempotency execution authority is no longer current."),
                    unauthorized = Refusal("unauthorized-effect-delegation", "trusted-effect-delegation", "Synthetic trusted-effect delegation refused."),
                    forgedEffect = Refusal("invalid-effect-gateway-proof", "trusted-effect-gateway-proof", "Trusted effect gateway proof is invalid."),
                    accepted = true, replayed = true, baseline = 1, sequence = 3 };
            }
            records[key] = JsonSerializer.Serialize(value);
            if (kind.Name == "http-get")
            {
                DateTimeOffset started = scenario == "natural-reminder" && i == RecordCount(row, profiles, kind) - 1
                    ? began.AddSeconds(81) : began.AddSeconds(1 + i * 0.1);
                records[Ordinal("http-get-observation", i)] = JsonSerializer.Serialize(new { address = key["http-get:".Length..], began = started,
                    ended = started.AddMilliseconds(50), status = 200, bodyHash = Hash(JsonSerializer.Serialize(value)) });
            }
        }
        records = records.Where(pair => !pair.Key.StartsWith("inventory:", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var geometry in row.GetProperty("InventoryGeometry").EnumerateArray())
        {
            int actorIndex = geometry.GetProperty("Actor").GetInt32(), head = geometry.GetProperty("Head").GetInt32();
            string actor = geometry.GetProperty("Tenant").GetString() + ":counter:synthetic" + (actorIndex == 0 ? "" : actorIndex);
            string[] fields = [actor + ":metadata", actor + ":snapshot", .. Enumerable.Range(1, head).Select(sequence => actor + ":events:" + sequence)];
            int[] classes = geometry.GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var hashes = fields.Select((field, i) => (field, value: classes[i] < 0 ? null : Hash(field + "|" + classes[i])))
                .ToDictionary(pair => pair.field, pair => pair.value, StringComparer.Ordinal);
            string key = "inventory:" + actor; int ordinal = 1; while (records.ContainsKey(key)) key = "inventory:" + actor + "#" + ++ordinal;
            records[key] = JsonSerializer.Serialize(new { fixture = actor, appId = "p1r-synthetic", database = "p1r-synthetic-redis-1", hashes });
        }
        if (records.TryGetValue("capability-security-observations", out string? security))
        {
            var data = JsonNode.Parse(security)!.AsObject();
            int head = row.GetProperty("InventoryGeometry").EnumerateArray().First().GetProperty("Head").GetInt32();
            foreach (string refusal in new[] { "stale", "forged", "completedStale", "unauthorized", "forgedEffect" })
                foreach (string side in new[] { "before", "after" })
                    for (int sequence = 2; sequence <= head; sequence++) data[refusal]![side]!.AsObject()["tenant-a:counter:synthetic:events:" + sequence] = null;
            records["capability-security-observations"] = data.ToJsonString();
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
            error, records, began, began.AddSeconds(scenario == "natural-reminder" ? 100 : 30)));
    }

    private object ProviderCreation(string name, DateTimeOffset began)
    {
        string owner = "p1r-" + Hash(name)[..32], root = "/synthetic/" + owner, container = owner + "-creation-lifecycle", id = Hash(container);
        object[] observed = [new { Id = id, Name = "/" + container, Image = PublishedCluster.RedisImage.Split('@')[1],
            Config = new { Image = PublishedCluster.RedisImage, Labels = new Dictionary<string, string> { ["p1r.owner"] = owner } }, State = new { Running = false } }];
        string output = JsonSerializer.Serialize(observed);
        void Add(string executable, string[] args, int start, int end, int exit, string stdout = "", string stderr = "", TimeSpan? timeout = null)
            => Commands.Add(new(new(executable, args, root, timeout ?? TimeSpan.FromSeconds(15)), began.AddSeconds(start), began.AddSeconds(end), exit,
                stdout, stderr, Hash(stdout + stderr)));
        Add("python3", ["-c", ContainerCreationDrill.LaunchScript, container, owner, PublishedCluster.RedisImage, root + "/provider-created.json"],
            1, 10, -1, stderr: "Command input/execution was interrupted or exceeded its deadline.",
            timeout: TimeSpan.FromSeconds(name.StartsWith("cancellation", StringComparison.Ordinal) ? 30 : 8));
        string[] inspect = ["inspect", "--format", QualificationRuntime.ContainerIdentityFormat, container];
        Add("docker", inspect, 2, 3, 0, output);
        Add("docker", ["rm", "-fv", id], 11, 12, 0);
        for (int i = 0; i < 3; i++) Add("docker", inspect, 13 + i * 2, 14 + i * 2, 1, stderr: "Error: no such object: " + container);
        return observed;
    }

    private static int RecordCount(JsonElement row, JsonElement[] profiles, JsonProperty kind)
    {
        if (kind.Name != "http-get") return kind.Value.GetInt32();
        // Earlier minimum-record fixtures omitted some actual GET bodies. The independent
        // positive fixture now models every fixed producer path, plus both natural-delivery states.
        return Math.Max(kind.Value.GetInt32(), profiles.Length * 2 + row.GetProperty("HttpGetPaths").EnumerateObject()
            .Where(path => path.Name != "identity").Sum(path => path.Value.GetInt32()));
    }

    private (string Key, object Value) HttpGet(JsonElement row, JsonElement[] profiles, int index, int total)
    {
        string path; object value;
        if (index < profiles.Length * 2)
        {
            int profile = index / 2; string role = index % 2 == 0 ? "host" : "domain";
            path = "/identity"; value = JsonSerializer.Deserialize<JsonElement>(Identities(Find(profiles[profile].GetProperty(role).GetString()!)));
        }
        else
        {
            var kinds = new List<string>();
            foreach (var pathKind in row.GetProperty("HttpGetPaths").EnumerateObject().Where(item => item.Name is not ("identity" or "reminder")))
                kinds.AddRange(Enumerable.Repeat(pathKind.Name, pathKind.Value.GetInt32()));
            if (row.GetProperty("HttpGetPaths").TryGetProperty("reminder", out _)) kinds.AddRange(Enumerable.Repeat("reminder", total - profiles.Length * 2 - kinds.Count));
            string kind = kinds[index - profiles.Length * 2];
            path = "/" + kind + "/synthetic";
            string name = row.GetProperty("Case").GetString()!;
            value = kind switch
            {
                "reader" => ReaderResult(name),
                "stats" => new { applyCalls = 0, handleCalls = 0, queryReads = 0, queryRequests = 0 },
                "manifest" => new { registered = true, fingerprint = Hash("registered-manifest") },
                "manifest-refusal" => new { observed = true, cause = new { actor = "tenant-a:counter:synthetic", code = "missing-allow-listed-callable",
                    stage = "production-logical-event-upcast-binding", declaringType = "Hexalith.EventStore.Client.Events.EventUpcastChainExecutor+Synthetic" } },
                "reminder" => new { submitted = index == total - 1 ? 1 : 0 },
                "status-fixture" => new Dictionary<string, object?>(),
                _ => throw new InvalidOperationException("The independent synthetic case has no such HTTP response."),
            };
            if (kind == "status-fixture")
            {
                int statusIndex = kinds.Take(index - profiles.Length * 2).Count(kind => kind == "status-fixture");
                string suffix = new[] { "legacy", "terminal", "automatic" }[statusIndex];
                string message = "status-synthetic-" + suffix; path = "/status-fixture/" + message;
                var body = (Dictionary<string, object?>)value;
                body["status"] = 4; body["aggregateId"] = "fixture-counter"; body["eventCount"] = 1; body["messageId"] = message;
                if (!name.StartsWith("3.70.1-", StringComparison.Ordinal)) body["retryable"] = statusIndex == 0 ? null : statusIndex == 2;
            }
        }
        return ("http-get:http://127.0.0.1:" + (31000 + index) + path, value);
    }

    private static object ReaderResult(string name)
    {
        bool selected = name.StartsWith("3.128.2-", StringComparison.Ordinal);
        bool floor = name.Contains("-floor-0-", StringComparison.Ordinal) || name.Contains("-floor-14-", StringComparison.Ordinal);
        if (selected && (floor || name.Contains("-version-987-", StringComparison.Ordinal)))
            return new { ok = false, error = "System.InvalidOperationException", message = floor
                ? "SourceHeadChanged: actor metadata has an invalid retained floor."
                : "RollbackReaderCapabilityHold: a typed reader cannot consume unsupported event metadata." };
        return new { ok = true, sequence = 2, count = 2 };
    }

    private static string CrossAction(string name, int index) => name.Contains("3.70.1", StringComparison.Ordinal)
        ? new[] { "common", "fenced-valid", "trusted-valid" }[index]
        : new[] { "common", "fenced-forged", "fenced-valid", "trusted-forged", "trusted-valid", "trusted-replay" }[index];

    private static object CrossClient(string name, string action, ConsumerBuild client)
    {
        var identities = JsonSerializer.Deserialize<JsonElement>(Identities(client)).GetProperty("identities").Clone();
        if (action == "common") return new { action, identities, ok = true, supported = true, outcome = new { accepted = true, before = 0, sequence = 1 } };
        if (client.Version == "3.70.1") return new { action, identities, ok = false, supported = false, code = "client-contract-lacks-protected-methods",
            missingMethods = new[] { "ProcessFencedCommandAsync", "ProcessTrustedEffectAsync" } };
        if (name.Contains("-to-host-3.70.1-", StringComparison.Ordinal))
        {
            string request = action.StartsWith("fenced", StringComparison.Ordinal) ? "ProcessFencedCommandAsyncReqBody" : "ProcessTrustedEffectAsyncReqBody";
            string message = "data of the 'urn:actors:" + request + "' data contract. The deserializer has no knowledge of any type that maps to this contract.";
            return new { action, identities, ok = false, supported = true, actualType = "System.Runtime.Serialization.SerializationException", message,
                error = "Dapr.Actors.ActorMethodInvocationException", cause = "System.Runtime.Serialization.SerializationException: " + message };
        }
        if (action.EndsWith("forged", StringComparison.Ordinal))
        {
            string message = action == "fenced-forged" ? "The idempotency execution fence is missing, stale, or invalid." : "Trusted effect gateway proof is invalid.";
            return new { action, identities, ok = false, supported = true, actualType = "System.InvalidOperationException", message,
                error = "Dapr.Actors.ActorMethodInvocationException", cause = "System.InvalidOperationException: " + message };
        }
        return new { action, identities, ok = true, supported = true, outcome = new { accepted = true, sequence = action == "fenced-valid" ? 2 : 3, replayed = action == "trusted-replay" } };
    }

    private static string ProbeInput(string scenario, string name, int index)
    {
        if (scenario == "provenance") return "{\"operation\":\"identity\"}";
        if (scenario == "legacy-metadata") return JsonSerializer.Serialize(new { operation = name.EndsWith("-three-argument-constructor", StringComparison.Ordinal) ? "legacy-constructor" : "metadata" });
        if (scenario == "metadata-read") return "{\"operation\":\"metadata\"}";
        if (scenario == "mixed-api") return name.StartsWith("client-", StringComparison.Ordinal)
            ? JsonSerializer.Serialize(new { operation = "cross-client", action = CrossAction(name, index) }) : "{\"operation\":\"cursor\"}";
        if (name == "real-signed-admission-and-refusals") return "{\"operation\":\"signed-query\"}";
        string kind = scenario == "query-wire" ? "query" : "projection";
        if (name.EndsWith("-xml", StringComparison.Ordinal) && index == 0) return JsonSerializer.Serialize(new { operation = "make-wire", kind, value = "synthetic-wire" });
        return JsonSerializer.Serialize(new { operation = "wire", kind, serializer = name.EndsWith("-xml", StringComparison.Ordinal) ? "xml" : "json", value = "synthetic-wire" });
    }

    private ConsumerBuild ProbeBuild(string scenario, string name, int index)
    {
        if (scenario == "provenance") return Find(name);
        if (scenario == "mixed-api" && name.StartsWith("client-", StringComparison.Ordinal))
        {
            string[] parts = name.Split('-');
            return Find(parts[1] + (parts[2] == "Debug" ? "-source-Probe" : "-package-Probe"));
        }
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
