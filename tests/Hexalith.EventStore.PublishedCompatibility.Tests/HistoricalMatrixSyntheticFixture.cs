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
        ["builds"] = "a319176d3d3b27462583f18236ff85c8705a4998", ["consumerInputOrigin"] = "/synthetic/contract-fixture/consumer-inputs",
        ["templateHashes"] = new JsonObject { ["Probe/Program.cs"] = new string('4', 64) }, ["untrackedHashes"] = new JsonObject(),
    };
    /// <summary>Gets saved synthetic results independently mutable from the in-memory observations.</summary>
    internal JsonObject Summary { get; } = new();
    /// <summary>Gets the synthetic prepared consumer inventory.</summary>
    internal List<ConsumerBuild> Builds { get; } = [];
    /// <summary>Gets synthetic completed command observations, including actual input operation fixtures.</summary>
    internal List<CommandObservation> Commands { get; } = [];
    /// <summary>Gets independently mutable synthetic safe file observations, never real provider dump contents.</summary>
    internal List<QualificationFileFingerprint> Files { get; } = [];
    /// <summary>Gets independent synthetic cleanup/inventory observations for contract controls.</summary>
    internal List<QualificationCleanupPass> CleanupPasses { get; } = [];
    internal List<QualificationHttpExchange> HttpExchanges { get; } = [];
    internal List<QualificationScenarioInput> ScenarioInputs { get; } = [];
    internal List<QualificationStateRead> StateReads { get; } = [];
    internal List<QualificationProcessScan> ProcessScans { get; } = [];
    internal List<QualificationDaprLaunch> DaprLaunches { get; } = [];
    internal List<QualificationManagedLaunch> ManagedLaunches { get; } = [];

    /// <summary>Reads the explicit test fixture without deriving cases or outcomes from the validator.</summary>
    internal static HistoricalMatrixSyntheticFixture Create()
    {
        var fixture = new HistoricalMatrixSyntheticFixture();
        fixture.AddInitialBackend();
        foreach (string version in new[] { "3.70.1", "3.110.0", "3.128.2" })
        foreach (string role in new[] { "Host", "Domain", "Probe" }) fixture.Builds.Add(Build(version, role, false));
        foreach (string role in new[] { "Host", "Domain", "Probe" }) fixture.Builds.Add(Build("3.128.2", role, true));
        string path = Path.Combine(PublishedScenarioTests.FindRepository(), "tests", "Hexalith.EventStore.PublishedCompatibility.Tests",
            "Fixtures", "HistoricalMatrixCharacterizationFixture.json");
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(path));
        int index = 0;
        foreach (JsonElement row in source.RootElement.GetProperty("rows").EnumerateArray()) fixture.Add(row, index++);
        fixture.Commands.Add(new(new("/synthetic/contract-fixture/p1r-11111111111111111111111111111111/dapr-1.18.4/daprd", ["--version"], "/synthetic/contract-fixture/p1r-11111111111111111111111111111111", TimeSpan.FromSeconds(30)),
            DateTimeOffset.UnixEpoch.AddSeconds(-3), DateTimeOffset.UnixEpoch.AddSeconds(-2), 0, "1.18.4\n", "", Hash("1.18.4\n")));
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
        Summary["disposed"] = true;
        Summary["owners"] = "pending"; Summary["conformance"] = "pending";
        Summary["assertions"] = Observations.Sum(row => row.Assertions);
        Summary["passed"] = Observations.Count(row => row.Execution == "passed");
        Summary["failed"] = Observations.Count(row => row.Execution == "failed");
        Summary["unavailable"] = Observations.Count(row => row.Execution == "unavailable");
        Summary["incompatible"] = Observations.Count(row => row.Compatibility == "incompatible");
        Summary["observations"] = JsonSerializer.SerializeToNode(Observations);
        Summary["executionCommands"] = JsonSerializer.SerializeToNode(Commands.Select(HistoricalExecutionCommand.Capture).ToArray());
        Summary["files"] = JsonSerializer.SerializeToNode(Files);
        Summary["cleanupPasses"] = JsonSerializer.SerializeToNode(CleanupPasses);
        Summary["httpExchanges"] = JsonSerializer.SerializeToNode(HttpExchanges);
        Summary["scenarioInputs"] = JsonSerializer.SerializeToNode(ScenarioInputs); Summary["stateReads"] = JsonSerializer.SerializeToNode(StateReads);
        Summary["processScans"] = JsonSerializer.SerializeToNode(ProcessScans); Summary["daprLaunches"] = JsonSerializer.SerializeToNode(DaprLaunches);
        Summary["managedLaunches"] = JsonSerializer.SerializeToNode(ManagedLaunches);
    }

    /// <summary>Runs the complete contract against independent synthetic saved and actual captures.</summary>
    internal void Validate()
    {
        using JsonDocument summary = JsonDocument.Parse(Summary.ToJsonString());
        using JsonDocument capture = JsonDocument.Parse(Capture.ToJsonString());
        HistoricalMatrixMeasurementContract.Validate(Observations, summary.RootElement, capture.RootElement, Builds, Commands.Select(HistoricalExecutionCommand.Capture).ToArray(), Files, CleanupPasses, HttpExchanges, ScenarioInputs, StateReads, ProcessScans, DaprLaunches, ManagedLaunches);
    }

    private void Add(JsonElement row, int index)
    {
        string scenario = row.GetProperty("Scenario").GetString()!;
        string name = row.GetProperty("Case").GetString()!;
        var records = new Dictionary<string, string>();
        DateTimeOffset began = DateTimeOffset.UnixEpoch.AddMinutes(index * 2);
        JsonElement[] profiles = row.GetProperty("Profiles").EnumerateArray().ToArray();
        for (int i = 0; i < profiles.Length; i++)
        {
            ConsumerBuild host = Find(profiles[i].GetProperty("host").GetString()!);
            ConsumerBuild domain = Find(profiles[i].GetProperty("domain").GetString()!);
            bool restoredBackend = scenario.Contains("upgrade-restore", StringComparison.Ordinal) && i >= (scenario == "pre-upgrade-restore" ? 2 : 1);
            string backend = "p1r-11111111111111111111111111111111-redis-" + (restoredBackend ? "restore-" + index : "1");
            string volume = backend.Replace("-redis-", "-redis-data-", StringComparison.Ordinal);
            string endpoint = "127.0.0.1:" + (restoredBackend ? 40000 + index : 6379);
            DateTimeOffset profileAt = restoredBackend ? began.AddSeconds(3.95) : began;
            records[Ordinal("runtime-profile", i)] = JsonSerializer.Serialize(new
            {
                profileId = Hash(scenario + "|" + name + "|" + i)[..32], owner = "p1r-11111111111111111111111111111111", root = "/synthetic/contract-fixture/p1r-11111111111111111111111111111111", observedAt = profileAt,
                sidecarAddress = "http://127.0.0.1:" + (33000 + i), redisAddress = endpoint, redisVolume = volume,
                daprdPath = "/synthetic/contract-fixture/p1r-11111111111111111111111111111111/dapr-1.18.4/daprd", componentPath = "/synthetic/contract-fixture/p1r-11111111111111111111111111111111/components/state.yaml",
                appId = "p1r-11111111111111111111111111111111", runtime = "1.18.4", redisImage = PublishedCluster.RedisImage,
                redisContainer = backend,
                componentHash = Hash("component"), daprdHash = Hash("daprd"), host, domain,
                hostAddress = "http://127.0.0.1:" + (31000 + i * 2), domainAddress = "http://127.0.0.1:" + (31001 + i * 2),
                domainSidecarProcessId = host.Version == ConsumerMaterializer.SelectedVersion ? (int?)(40000 + i) : null,
                domainSidecarAddress = host.Version == ConsumerMaterializer.SelectedVersion ? "http://127.0.0.1:" + (34000 + i) : null,
                sidecarProcessId = 30000 + i, hostProcessId = 10000 + i, domainProcessId = 20000 + i,
            });
            string profileId = Hash(scenario + "|" + name + "|" + i)[..32];
            const string inputRoot = "/synthetic/contract-fixture/p1r-11111111111111111111111111111111";
            ScenarioInputs.Add(new(profileId, "p1r-11111111111111111111111111111111", inputRoot, "daprd", inputRoot + "/dapr-1.18.4/daprd", 100, Hash("daprd"), began.AddTicks(-1)));
            ScenarioInputs.Add(new(profileId, "p1r-11111111111111111111111111111111", inputRoot, "component", inputRoot + "/components/state.yaml", 100, Hash("component"), profileAt.AddTicks(-1), new(endpoint, "statestore", "state.redis", "v1", true, "appid")));
            DaprLaunches.Add(new(profileId, "p1r-11111111111111111111111111111111", inputRoot + "/dapr-1.18.4/daprd", inputRoot + "/components", "p1r-11111111111111111111111111111111", 31000 + i * 2, 33000 + i, 30000 + i, profileAt, "Host", began, began.AddSeconds(scenario == "natural-reminder" ? 100 : 30)));
            if (host.Version == ConsumerMaterializer.SelectedVersion)
                DaprLaunches.Add(new(profileId, "p1r-11111111111111111111111111111111", inputRoot + "/dapr-1.18.4/daprd", inputRoot + "/components", "p1r-11111111111111111111111111111111-domain", 31001 + i * 2, 34000 + i, 40000 + i, profileAt, "Domain", began, began.AddSeconds(scenario == "natural-reminder" ? 100 : 30)));
            ManagedLaunches.Add(new(Hash(profileId + "|Host")[..32], "p1r-11111111111111111111111111111111", inputRoot, "Host", host.Assembly, host.PreparedExecutableHashes!.ToDictionary(pair => pair.Key, pair => pair.Value), 10000 + i, profileId, "http://127.0.0.1:" + (31000 + i * 2), began, began.AddSeconds(scenario == "natural-reminder" ? 100 : 30)));
            ManagedLaunches.Add(new(Hash(profileId + "|Domain")[..32], "p1r-11111111111111111111111111111111", inputRoot, "Domain", domain.Assembly, domain.PreparedExecutableHashes!.ToDictionary(pair => pair.Key, pair => pair.Value), 20000 + i, profileId, "http://127.0.0.1:" + (31001 + i * 2), began, began.AddSeconds(scenario == "natural-reminder" ? 100 : 30)));
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
                    string output = "P1R_RESULT " + ProbeOutput(scenario, name, i, build);
                    var completed = new CommandObservation(command, began.AddSeconds(i + 1), began.AddSeconds(i + 1.5), 0, output, "", Hash(output));
                    Commands.Add(completed);
                    value = new { Command = command, completed.Started, completed.Ended, completed.ExitCode, completed.OutputHash };
                }
            }
            else if (kind.Name == "inventory")
            {
                key = Ordinal("inventory:tenant-a:counter:synthetic", i);
                value = new { fixture = "tenant-a:counter:synthetic", appId = "p1r-11111111111111111111111111111111", database = "p1r-11111111111111111111111111111111-redis-1",
                    hashes = new Dictionary<string, string?> { ["tenant-a:counter:synthetic:metadata"] = Hash("metadata"),
                        ["tenant-a:counter:synthetic:snapshot"] = null, ["tenant-a:counter:synthetic:events:1"] = Hash("event") } };
            }
            else if (kind.Name == "http-get")
            {
                var get = HttpGet(row, profiles, i, RecordCount(row, profiles, kind));
                key = get.Key; value = get.Value;
                int ordinal = 1; while (records.ContainsKey(key)) key = get.Key + "#" + ++ordinal;
            }
            else if (kind.Name == "http-post") value = new { address = "http://127.0.0.1:31000/execute", began = began.AddSeconds(0.5 + i * 0.1), ended = began.AddSeconds(0.55 + i * 0.1), requestHash = Hash("request"), responseHash = Hash("response"), status = 200 };
            else if (kind.Name == "constructed-metadata") value = MetadataValue(ProbeBuild(scenario, name, i), null);
            else if (kind.Name == "provider-backup") value = new { sourceContainer = "p1r-11111111111111111111111111111111-redis-1", hash = Hash("backup"), bytes = 100 };
            else if (kind.Name == "provider-created-before-interruption") value = ProviderCreation(name, began, records);
            else if (kind.Name == "production-manifest-command-result") value = new { ok = false, count = -1, sequence = 1,
                metadata = new { currentSequence = 1, lastModified = DateTimeOffset.UnixEpoch, eTag = "p1r-manifest", retainedFloor = 1 },
                error = "CommandProcessingRefusal", message = "Protected data diagnostic details were redacted. ReasonCode=logical-event-read-rejected; Stage=Processing.", deadLetterBookkeeping = 1 };
            else if (kind.Name == "production-manifest-refusal")
            {
                // The real manifest fixture uses its own domain/address. This synthetic witness
                // keeps the same owner binding rather than claiming a successful protected hop.
                value = new { observed = true, cause = new { actor = "tenant-a:counter:synthetic", code = "missing-allow-listed-callable",
                    stage = "production-logical-event-upcast-binding", declaringType = "Hexalith.EventStore.Client.Events.EventUpcastChainExecutor", method = "GetBinding", message = "CapabilityMismatch: an admitted edge lacks an allow-listed callable." } };
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
            else if (kind.Name == "production-handler-discovery") value = new { declared = new[] { "get-counter" }, persisted = new[] { "get-counter" },
                registry = "Hexalith.EventStore.Queries.DaprDomainQueryHandlerRegistry", supported = true, unknown = false };
            else if (kind.Name.StartsWith("production-handler-", StringComparison.Ordinal)) value = new
            {
                errorMessage = kind.Name == "production-handler-valid" ? null : "protected-admission-refused",
                invoker = "Hexalith.EventStore.Queries.DaprDomainQueryInvoker", router = "Hexalith.EventStore.Queries.HandlerAwareQueryRouter",
                success = kind.Name == "production-handler-valid", payload = new { originalActorId = "actor", authenticatedWorkloadId = "workload",
                    delegationId = "delegation", tenantId = "tenant-a", correlationId = "corr", isDelegated = true, scopes = new[] { "counter.read" },
                    audience = new[] { "eventstore" }, identityAdmissionProof = "synthetic-proof", value = new { watermark = 987 } },
            };
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
                foreach (var reader in records.Where(pair => pair.Key.StartsWith("http-get:", StringComparison.Ordinal) && new Uri(pair.Key["http-get:".Length..].Split('#')[0]).AbsolutePath.StartsWith("/reader/", StringComparison.Ordinal)).ToArray())
        {
            string address = reader.Key["http-get:".Length..].Split('#')[0];
            records["actual-reader-target"] = JsonSerializer.Serialize(new { actor = Uri.UnescapeDataString(new Uri(address).AbsolutePath["/reader/".Length..]), address, hostProcessId = 10000 + profiles.Length - 1 });
        }
        if (name.EndsWith("-durably-applied-watermark-cursor", StringComparison.Ordinal) && i >= 2)
                    started = began.AddSeconds(5.6 + (i - 2) * 0.05);
                if (scenario.Contains("upgrade-restore", StringComparison.Ordinal) && i >= 2)
                    started = began.AddSeconds(4 + (i - 2) * 0.1);
                if (name.EndsWith("-production-declared-handler-discovery-transport", StringComparison.Ordinal) && i >= 2)
                {
                    int read = i - 2;
                    started = began.AddSeconds(5 + (2 + read / 2) * 0.4 + (read % 2 == 0 ? -0.1 : 0.15));
                }
                records[Ordinal("http-get-observation", i)] = JsonSerializer.Serialize(new { address = key["http-get:".Length..].Split('#')[0], began = started,
                    ended = started.AddMilliseconds(50), status = 200, bodyHash = Hash(JsonSerializer.Serialize(value)) });
            }
        }
        records = records.Where(pair => !pair.Key.StartsWith("inventory:", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        int inventoryIndex = 0;
        foreach (var geometry in row.GetProperty("InventoryGeometry").EnumerateArray())
        {
            int actorIndex = geometry.GetProperty("Actor").GetInt32(), head = geometry.GetProperty("Head").GetInt32();
            string actor = geometry.GetProperty("Tenant").GetString() + ":counter:synthetic" + (actorIndex == 0 ? "" : actorIndex);
            string[] fields = [actor + ":metadata", actor + ":snapshot", .. Enumerable.Range(1, head).Select(sequence => actor + ":events:" + sequence)];
            int[] classes = geometry.GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var hashes = fields.Select((field, i) => (field, value: classes[i] < 0 ? null : Hash(field + "|" + classes[i])))
                .ToDictionary(pair => pair.field, pair => pair.value, StringComparer.Ordinal);
            string key = "inventory:" + actor; int ordinal = 1; while (records.ContainsKey(key)) key = "inventory:" + actor + "#" + ++ordinal;
            records[key] = JsonSerializer.Serialize(new { fixture = actor, appId = "p1r-11111111111111111111111111111111",
                database = "p1r-11111111111111111111111111111111-redis-" + (scenario.Contains("upgrade-restore", StringComparison.Ordinal) && inventoryIndex >= 2 ? "restore-" + index : "1"), hashes });
            inventoryIndex++;
        }
        if (records.TryGetValue("capability-security-observations", out string? security)) SecurityCapture(records, security, began, name);
        if (name.EndsWith("-durably-applied-watermark-cursor", StringComparison.Ordinal) || name.EndsWith("-production-declared-handler-discovery-transport", StringComparison.Ordinal))
            HttpSequence(records, name, began);
        if (name.EndsWith("-registered-manifest-production-hop", StringComparison.Ordinal))
        {
            string body = records["production-manifest-command-result"];
            records["http-post"] = JsonSerializer.Serialize(new { address = "http://127.0.0.1:31002/execute/" + Uri.EscapeDataString("tenant-a:counter:synthetic"),
                began = began.AddSeconds(5), ended = began.AddSeconds(6), status = 200,
                requestHash = Hash("{\"append\":true}"), responseHash = Hash(body), request = HistoricalHttpOperationCapture.Request(new { append = true }), response = HistoricalHttpOperationCapture.Response(body) });
        }
        if (name.EndsWith("-production-declared-handler-discovery-transport", StringComparison.Ordinal))
        {
            var post = JsonNode.Parse(records["http-post"])!.AsObject(); string body = records["production-handler-discovery"];
            post["response"] = JsonSerializer.SerializeToNode(HistoricalHttpOperationCapture.Response(body)); post["responseHash"] = Hash(body);
            records["http-post"] = post.ToJsonString();
        }
        if (scenario.Contains("upgrade-restore", StringComparison.Ordinal)) ProviderBackup(records, scenario, name, began);
        if (name == "matrix-cleanup-twice") MatrixCleanup(records, began);
        if (scenario == "invalid-evidence") FixtureInput(records, name, began);
        int readOrdinal = 0;
        foreach (string key in records.Keys.Where(key => key.StartsWith("inventory:", StringComparison.Ordinal)).ToArray())
        {
            var inventory = JsonNode.Parse(records[key])!.AsObject(); string actor = inventory["fixture"]!.GetValue<string>();
            var profile = JsonNode.Parse(records["runtime-profile"])!.AsObject();
            // Restore fixtures deliberately change backend; select the independently modelled matching launch.
            var matching = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonNode.Parse(pair.Value)!.AsObject())
                .First(item => item["redisContainer"]!.GetValue<string>() == inventory["database"]!.GetValue<string>());
            inventory["profileId"] = matching["profileId"]!.DeepClone(); var ids = new JsonArray();
            foreach (var field in inventory["hashes"]!.AsObject())
            {
                string id = Hash(scenario + "|" + name + "|read|" + readOrdinal++)[..32]; ids.Add(id);
                string? digest = field.Value?.GetValue<string>();
                StateReads.Add(new(id, matching["profileId"]!.GetValue<string>(), "p1r-11111111111111111111111111111111", "p1r-11111111111111111111111111111111", inventory["database"]!.GetValue<string>(), actor, field.Key,
                    matching["hostAddress"]!.GetValue<string>() + "/read/" + Uri.EscapeDataString(actor) + "/" + Uri.EscapeDataString(field.Key), matching["hostProcessId"]!.GetValue<int>(),
                    began.AddSeconds(20).AddTicks(readOrdinal * 1000), began.AddSeconds(20).AddTicks(readOrdinal * 1000 + 100), digest is null ? 404 : 200, digest is null ? 0 : 100, digest ?? Hash(""), digest));
            }
            inventory["readIds"] = ids; records[key] = inventory.ToJsonString();
        }
        AddSafeOperationResults(records, scenario, began);
        foreach (string key in records.Keys.ToArray())
        {
            string raw = records[key], safe = HistoricalSafeJson.Text(raw); records[key] = safe;
            if (raw != safe) records["record-byte-commitment:" + key] = JsonSerializer.Serialize(new { sha256 = Hash(raw) });
        }
        foreach (string key in records.Keys.Where(key => key.Split('#')[0] is "http-post" or "fixture-http-post").ToArray())
        {
            var post = JsonNode.Parse(records[key])!.AsObject();
            if (!post.ContainsKey("request"))
            {
                post["request"] = JsonSerializer.SerializeToNode(HistoricalHttpOperationCapture.Request(new { }));
                post["response"] = JsonSerializer.SerializeToNode(HistoricalHttpOperationCapture.Response("{\"synthetic\":true}"));
                post["requestHash"] = Hash("{}"); post["responseHash"] = Hash("{\"synthetic\":true}");
            }
            records[key] = post.ToJsonString();
            HttpExchanges.Add(new(post["address"]!.GetValue<string>(), post["began"]!.GetValue<DateTimeOffset>().AddTicks(1),
                post["ended"]!.GetValue<DateTimeOffset>().AddTicks(-1), post["status"]!.GetValue<int>(),
                post["requestHash"]!.GetValue<string>(), post["responseHash"]!.GetValue<string>(),
                Hash(post["request"]!.ToJsonString()), Hash(post["response"]?.ToJsonString() ?? "null")));
        }
        foreach (var timing in records.Where(pair => pair.Key.Split('#')[0] == "http-get-observation").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)))
            HttpExchanges.Add(new(timing.GetProperty("address").GetString()!, timing.GetProperty("began").GetDateTimeOffset().AddTicks(1), timing.GetProperty("ended").GetDateTimeOffset().AddTicks(-1),
                timing.GetProperty("status").GetInt32(), Hash(""), timing.GetProperty("bodyHash").GetString()!, Hash("null"), Hash("null"), "GET"));
        foreach (var read in StateReads.Where(read => read.Started >= began && read.Ended <= began.AddSeconds(30)))
            HttpExchanges.Add(new(read.Address, read.Started.AddTicks(1), read.Ended.AddTicks(-1), read.Status, Hash(""), read.BodySha256, Hash("null"), Hash("null"), "GET"));
        string? error = null;
        if (row.GetProperty("XmlError").GetBoolean())
        {
            string[] pair = name[..^4].Split("-to-", StringSplitOptions.None);
            string consumer = pair[0].StartsWith("3.128.2", StringComparison.Ordinal) ? pair[1] : pair[0];
            ConsumerBuild build = Find(consumer);
            string value = WireInputValue(scenario, name, records.Count(pair => pair.Key.Split('#')[0] == "probe-command"));
            string input = consumer == pair[0] ? JsonSerializer.Serialize(new { operation = "make-wire", kind = "projection", value })
                : JsonSerializer.Serialize(new { operation = "wire", kind = "projection", serializer = "xml", value });
            string diagnostic = "Unhandled exception. System.Runtime.Serialization.InvalidDataContractException: Type 'Hexalith.EventStore.Contracts.Projections.ProjectionEventDto' cannot be serialized.";
            var command = new QualificationCommand("dotnet", [build.Assembly], Path.GetDirectoryName(build.Project)!, TimeSpan.FromSeconds(30), Input: input);
            Commands.Add(new(command, began.AddSeconds(25), began.AddSeconds(26), 134, "", diagnostic, Hash(diagnostic)));
            error = $"System.InvalidOperationException: Exit 134: dotnet {build.Assembly}\n\n{diagnostic}";
        }
        foreach (var command in Commands.Where(command => command.Started >= began && command.Ended <= began.AddSeconds(30) && command.Command.FileName == "dotnet"))
        {
            var consumer = Builds.SingleOrDefault(build => command.Command.Arguments.FirstOrDefault() == build.Assembly);
            if (consumer is not null) ManagedLaunches.Add(new(Hash(name + "|" + command.Started.ToString("O") + "|" + consumer.Assembly)[..32], "p1r-11111111111111111111111111111111", "/synthetic/contract-fixture/p1r-11111111111111111111111111111111",
                consumer.Role, consumer.Assembly, consumer.PreparedExecutableHashes!.ToDictionary(pair => pair.Key, pair => pair.Value), 50000 + ManagedLaunches.Count, null, null, command.Started.AddTicks(1), command.Ended.AddTicks(-1)));
        }
        Observations.Add(new(scenario, name, row.GetProperty("Execution").GetString()!, row.GetProperty("Compatibility").GetString()!,
            row.GetProperty("Assertions").GetInt32(), row.GetProperty("Failures").EnumerateArray().Select(value => value.GetString()!).ToArray(),
            error, records, began, name == "matrix-cleanup-twice" ? CleanupPasses.Max(pass => pass.Ended).AddSeconds(1) : began.AddSeconds(scenario == "natural-reminder" ? 100 : 30)));
    }

    private static void AddSafeOperationResults(Dictionary<string, string> records, string scenario, DateTimeOffset began)
    {
        var profiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        if (profiles.Length == 0) return;
        var inventories = records.Where(pair => pair.Key.StartsWith("inventory:", StringComparison.Ordinal)).Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        string actor = inventories.Length == 0 ? "tenant-a:counter:synthetic" : inventories[0].GetProperty("fixture").GetString()!;
        void Post(string address, object request, string body, double at, bool replace = false)
        {
            string key = "http-post";
            if (!replace) { int i = 1; while (records.ContainsKey(key)) key = "http-post#" + ++i; }
            records[key] = JsonSerializer.Serialize(new { address, began = began.AddSeconds(at), ended = began.AddSeconds(at + 1), status = 200,
                requestHash = QualificationRuntime.Hash(JsonSerializer.SerializeToUtf8Bytes(request, new JsonSerializerOptions(JsonSerializerDefaults.Web))), responseHash = Hash(body),
                request = HistoricalHttpOperationCapture.Request(request), response = HistoricalHttpOperationCapture.Response(body) });
        }
        if (records.TryGetValue("actual-writer-admission-result", out var writer))
            Post(profiles[^1].GetProperty("hostAddress").GetString() + "/writer-admission/" + Uri.EscapeDataString(actor), new { }, writer, 15, replace: true);
        if (records.TryGetValue("capability-security-observations", out var security))
            Post(profiles[0].GetProperty("hostAddress").GetString() + "/capabilities/" + actor.Split(':')[2], new { }, security, 5, replace: true);
        if (scenario is "full-replay" or "snapshot-tail" or "retained-covered" or "retained-uncovered" or "missing-event" or "checkout")
        {
            var profile = profiles[^1]; string address = profile.GetProperty("hostAddress").GetString() + "/execute/" + Uri.EscapeDataString(actor);
            bool ok = scenario is not ("retained-uncovered" or "missing-event");
            var result = JsonSerializer.SerializeToElement(new { ok, count = ok ? 12 : -1, sequence = ok ? 12 : 1 }); string body = result.GetRawText();
            Post(address, new { append = false }, body, 21);
            records["actual-execute-result"] = JsonSerializer.Serialize(new { profileId = profile.GetProperty("profileId").GetString(), actor, address,
                append = false, hostPid = profile.GetProperty("hostProcessId").GetInt32(), result, responseHash = Hash(body) });
        }
    }

    private static void FixtureInput(Dictionary<string, string> records, string name, DateTimeOffset began)
    {
        string actor = records.Where(record => record.Key.StartsWith("inventory:", StringComparison.Ordinal))
            .Select(record => JsonSerializer.Deserialize<JsonElement>(record.Value).GetProperty("fixture").GetString()!).First();
        bool floor = name.Contains("-floor-", StringComparison.Ordinal) || name.Contains("-head12-floor", StringComparison.Ordinal);
        var value = new JsonObject { ["currentSequence"] = 12, ["retainedFloor"] = 1, ["metadataVersion"] = 1,
            ["serializationFormat"] = "json", ["eventTypeName"] = "CounterIncremented", ["payload"] = Convert.ToBase64String("{}"u8.ToArray()) };
        if (floor) value["retainedFloor"] = name.Contains("floor-14", StringComparison.Ordinal) || name.EndsWith("floor14", StringComparison.Ordinal) ? 14 : 0;
        if (name.Contains("-version-987-", StringComparison.Ordinal)) value["metadataVersion"] = 987;
        if (name.Contains("-protected-mismatch-", StringComparison.Ordinal)) value["serializationFormat"] = "protected+json";
        if (name.Contains("-unknown-type-", StringComparison.Ordinal)) value["eventTypeName"] = "Unknown.Event";
        if (name.Contains("-unreadable-", StringComparison.Ordinal)) value["payload"] = Convert.ToBase64String("invalid-json"u8.ToArray());
        var input = new[] { new { operation = "upsert", key = actor + (floor ? ":metadata" : ":events:2"), value } };
        string host = JsonSerializer.Deserialize<JsonElement>(records["runtime-profile"]).GetProperty("hostAddress").GetString()!;
        records["fixture-http-post"] = JsonSerializer.Serialize(new { address = host + "/fixture/" + Uri.EscapeDataString(actor),
            began = began.AddSeconds(10), ended = began.AddSeconds(10.5), status = 200, requestHash = Hash(JsonSerializer.Serialize(input)),
            responseHash = Hash(""), request = HistoricalHttpOperationCapture.Request(input), response = (JsonElement?)null });
    }

    private void SecurityCapture(Dictionary<string, string> records, string raw, DateTimeOffset began, string name)
    {
        var data = JsonNode.Parse(raw)!.AsObject();
        var inventoryKeys = records.Keys.Where(key => key.StartsWith("inventory:tenant-a:counter:synthetic", StringComparison.Ordinal)).ToArray();
        var first = JsonNode.Parse(records[inventoryKeys[0]])!["hashes"]!.AsObject();
        var final = JsonNode.Parse(records[inventoryKeys[1]])!["hashes"]!.AsObject();
        var intermediate = first.DeepClone().AsObject();
        intermediate["tenant-a:counter:synthetic:metadata"] = Hash("accepted-head2:" + name);
        intermediate["tenant-a:counter:synthetic:events:2"] = final["tenant-a:counter:synthetic:events:2"]!.DeepClone();
        string owner = "p1r-11111111111111111111111111111111", root = "/synthetic/contract-fixture/" + owner;
        string path = root + "/security-inventory-" + Hash("capture:" + name)[..32] + ".jsonl";
        var captureProfile = JsonSerializer.Deserialize<JsonElement>(records.First(pair => pair.Key.Split('#')[0] == "runtime-profile").Value);
        int pid = captureProfile.GetProperty("hostProcessId").GetInt32();
        string profileId = captureProfile.GetProperty("profileId").GetString()!;
        var snapshots = new List<object>();
        string[] fields = ["stale", "forged", "completedStale", "unauthorized", "forgedEffect"];
        for (int i = 0; i < fields.Length; i++)
        foreach (string phase in new[] { "before", "after" })
        {
            var state = i < 2 ? first : intermediate;
            data[fields[i]]![phase] = state.DeepClone();
            DateTimeOffset at = began.AddSeconds(10 + snapshots.Count * 0.1);
            snapshots.Add(new { owner, pid, code = data[fields[i]]!["code"]!.GetValue<string>(), phase, began = at, ended = at.AddMilliseconds(25), hashes = state.DeepClone() });
        }
        records["capability-security-observations"] = data.ToJsonString();
        records["capability-security-capture"] = JsonSerializer.Serialize(new { owner, path, pid, profileId, snapshots });
        string output = JsonSerializer.Serialize(snapshots);
        Commands.Add(new(new("python3", ["-c", HistoricalSecurityCapture.ReadScript, path, owner, pid.ToString(System.Globalization.CultureInfo.InvariantCulture)], root, TimeSpan.FromSeconds(30)),
            began.AddSeconds(12), began.AddSeconds(13), 0, output, "", Hash(output)));
    }

    private object ProviderCreation(string name, DateTimeOffset began, Dictionary<string, string> records)
    {
        string owner = "p1r-" + Hash(name)[..32], root = "/synthetic/" + owner, container = owner + "-creation-lifecycle", id = Hash(container);
        string imageId = "sha256:" + Hash("local-configuration:" + name);
        string image = JsonSerializer.Serialize(new[] { new { Id = imageId, RepoDigests = new[] { PublishedCluster.RedisImage } } });
        records["provider-local-image"] = image;
        object[] observed = [new { Id = id, Name = "/" + container, Image = imageId,
            Config = new { Image = PublishedCluster.RedisImage, Labels = new Dictionary<string, string> { ["p1r.owner"] = owner } }, State = new { Running = false } }];
        string output = JsonSerializer.Serialize(observed);
        void Add(string executable, string[] args, double start, double end, int exit, string stdout = "", string stderr = "", TimeSpan? timeout = null, string? interruption = null, double? deadline = null)
            => Commands.Add(new(new(executable, args, root, timeout ?? TimeSpan.FromSeconds(15)), began.AddSeconds(start), began.AddSeconds(end), exit,
                stdout, stderr, Hash(stdout + stderr), DeadlineStarted: deadline is null ? null : began.AddSeconds(deadline.Value), Interruption: interruption));
        Add("python3", ["-c", ContainerCreationDrill.LaunchScript, container, owner, PublishedCluster.RedisImage, root + "/provider-created.json"],
            1, 12, -1, stderr: "Command input/execution was interrupted or exceeded its deadline.",
            timeout: TimeSpan.FromSeconds(name.StartsWith("cancellation", StringComparison.Ordinal) ? 30 : 8),
            interruption: name.StartsWith("cancellation", StringComparison.Ordinal) ? "cancelled" : "timeout", deadline: 4);
        string[] inspect = ["inspect", "--format", QualificationRuntime.ContainerIdentityFormat, container];
        Add("docker", inspect, 2, 3, 0, output);
        Add("docker", ["image", "inspect", "--format", QualificationImageIdentity.Format, PublishedCluster.RedisImage], 3.1, 3.2, 0, image);
        records["cleanup-runtime"] = JsonSerializer.Serialize(new { owner, root });
        string sharedId = Hash("preserved-child-shared"), sharedImage = "sha256:" + Hash("preserved-child-image");
        const string started = "2000-01-01T00:00:00Z";
        string normalized = sharedId + "|" + sharedImage + "|" + started + "|True";
        void Inventory(double at)
        {
            Add("docker", ["ps", "-aq", "--no-trunc"], at, at + 0.05, 0, sharedId + "\n");
            Add("docker", ["inspect", "--format", QualificationRuntime.ContainerIdentityFormat, sharedId], at + 0.1, at + 0.15, 0,
                JsonSerializer.Serialize(new[] { new { Id = sharedId, Name = "/preserved-shared", Image = sharedImage,
                    Config = new { Labels = new Dictionary<string, string?> { ["p1r.owner"] = null } }, State = new { StartedAt = started, Running = true } } }));
        }
        Inventory(0.1);
        for (int pass = 0; pass < 3; pass++)
        {
            double at = 12.1 + pass * 5;
            if (pass == 0)
            {
                Add("docker", inspect, at + 0.1, at + 0.2, 0, output);
                Add("docker", ["rm", "-fv", id], at + 0.3, at + 0.4, 0);
            }
            Add("docker", inspect, at + 0.5, at + 0.6, 1, stderr: "Error: no such object: " + container);
            Inventory(at + 1);
            string scanId = Hash(name + "|cleanup|" + pass)[..32];
            ProcessScans.Add(new(scanId, owner, began.AddSeconds(at + 0.7), began.AddSeconds(at + 0.8), []));
            CleanupPasses.Add(new(owner, root, began.AddSeconds(at), began.AddSeconds(at + 2), began.AddSeconds(0.1), began.AddSeconds(0.5),
                normalized, normalized, [container], [], 0, 0, scanId, pass == 2));
        }
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
            path = kind == "stats" ? "/stats" : "/" + kind + "/synthetic";
            string name = row.GetProperty("Case").GetString()!;
            if (kind == "reader")
            {
                var actor = row.GetProperty("InventoryGeometry")[0];
                string id = actor.GetProperty("Tenant").GetString() + ":counter:synthetic" + (actor.GetProperty("Actor").GetInt32() == 0 ? "" : actor.GetProperty("Actor").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture));
                path = "/reader/" + Uri.EscapeDataString(id);
            }
            value = kind switch
            {
                "reader" => ReaderResult(name),
                "stats" => new { applyCalls = 0, handleCalls = 0,
                    queryReads = name.EndsWith("-production-declared-handler-discovery-transport", StringComparison.Ordinal) && index > profiles.Length * 2 ? 1 : 0,
                    queryRequests = name.EndsWith("-production-declared-handler-discovery-transport", StringComparison.Ordinal) ? (index - profiles.Length * 2 + 1) / 2 : 0 },
                "manifest" => new { registered = true, fingerprint = Hash("registered-manifest") },
                "manifest-refusal" => new { observed = true, cause = new { actor = "tenant-a:counter:synthetic", code = "missing-allow-listed-callable",
                    stage = "production-logical-event-upcast-binding", declaringType = "Hexalith.EventStore.Client.Events.EventUpcastChainExecutor", method = "GetBinding", message = "CapabilityMismatch: an admitted edge lacks an allow-listed callable." } },
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
        int port = index < profiles.Length * 2 ? 31000 + index : 31000 + (profiles.Length - 1) * 2;
        if (path == "/stats") port = 31001 + (profiles.Length - 1) * 2;
        if (path.StartsWith("/reader/", StringComparison.Ordinal)) port = 31000 + (profiles.Length - 1) * 2;
        return ("http-get:http://127.0.0.1:" + port + path, value);
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

    private static void HttpSequence(Dictionary<string, string> records, string name, DateTimeOffset began)
    {
        bool cursor = name.EndsWith("-durably-applied-watermark-cursor", StringComparison.Ordinal);
        string aggregate = "synthetic", token = "[redacted]";
        var operations = new List<(string Path, object Request, object Response)>();
        void Project(long position, long sequence) => operations.Add(("project-model/" + aggregate,
            new { position, sequence }, new { watermark = position, sequence, count = position == 987 ? 1 : 2 }));
        if (cursor)
        {
            Project(987, 12);
            operations.Add(("cursor-model/" + aggregate, new { mode = "valid" }, new { cursor = token, decoded = true, position = "987", model = new { watermark = 987 } }));
            operations.Add(("cursor-model/" + aggregate, new { mode = "valid", cursor = token }, new { cursor = token, decoded = true, position = "987", model = new { watermark = 987 } }));
            foreach (string mode in new[] { "foreign-actor", "foreign-tenant", "foreign-domain", "changed-filter", "changed-query", "tampered" })
                operations.Add(("cursor-model/" + aggregate, new { mode, cursor = token }, new { cursor = mode == "tampered" ? token + "-tampered" : token, decoded = false, position = (string?)null, model = new { watermark = 987 } }));
            Project(988, 13);
            operations.Add(("cursor-model/" + aggregate, new { mode = "valid", cursor = token }, new { cursor = token, decoded = false, position = (string?)null, model = new { watermark = 988 } }));
        }
        else
        {
            operations.Add(("declare-query-handlers", new { }, JsonSerializer.Deserialize<JsonElement>(records["production-handler-discovery"])));
            Project(987, 12);
            foreach (string mode in new[] { "valid", "missing", "forged", "foreign-tenant", "foreign-actor" })
                operations.Add(("query-route/" + aggregate, new { serializer = "json", route = "declared-handler", mode },
                    JsonSerializer.Deserialize<JsonElement>(records["production-handler-" + mode])));
        }
        for (int i = 0; i < operations.Count; i++)
        {
            var operation = operations[i]; string body = JsonSerializer.Serialize(operation.Response);
            records[Ordinal("http-post", i)] = JsonSerializer.Serialize(new
            {
                address = "http://127.0.0.1:" + (cursor && i >= 2 ? 31002 : 31000) + "/" + operation.Path,
                began = began.AddSeconds(5 + i * 0.4), ended = began.AddSeconds(5.1 + i * 0.4), status = 200,
                requestHash = Hash(JsonSerializer.Serialize(operation.Request)), responseHash = Hash(body),
                request = HistoricalHttpOperationCapture.Request(operation.Request), response = HistoricalHttpOperationCapture.Response(body),
            });
        }
    }

    private void ProviderBackup(Dictionary<string, string> records, string scenario, string name, DateTimeOffset began)
    {
        string owner = "p1r-11111111111111111111111111111111", root = "/synthetic/contract-fixture/" + owner;
        string path = root + "/backup-" + Hash(scenario + "|" + name)[..32] + ".rdb";
        string digest = Hash("actual-sensitive-backup:" + scenario + "|" + name);
        Files.Add(new(owner, path, "provider-backup-copied", true, 100, digest, began.AddSeconds(2.4), began.AddSeconds(2.5)));
        Files.Add(new(owner, path, "provider-backup-before-restore", true, 100, digest, began.AddSeconds(3.4), began.AddSeconds(3.5)));
        Files.Add(new(owner, path, "provider-backup-destroyed", false, 0, null, began.AddSeconds(3.9), began.AddSeconds(3.91)));
        records["provider-backup"] = JsonSerializer.Serialize(new { sourceContainer = owner + "-redis-1", hash = digest, bytes = 100, path });
        void Add(string[] arguments, double start, double end, string output = "")
            => Commands.Add(new(new("docker", arguments, root, TimeSpan.FromSeconds(30)), began.AddSeconds(start), began.AddSeconds(end), 0, output, "", Hash(output)));
        Add(["exec", owner + "-redis-1", "redis-cli", "SAVE"], 2, 2.1, "OK\n");
        Add(["cp", owner + "-redis-1:/data/dump.rdb", path], 2.2, 2.3);
        var target = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value))
            .First(profile => profile.GetProperty("redisContainer").GetString() != owner + "-redis-1");
        string container = target.GetProperty("redisContainer").GetString()!, volume = target.GetProperty("redisVolume").GetString()!;
        Add(["volume", "create", "--label", "p1r.owner=" + owner, volume], 2.6, 2.7, volume + "\n");
        Add(["create", "--name", container, "--label", "p1r.owner=" + owner, "--mount", "type=volume,source=" + volume + ",destination=/data", "-p", target.GetProperty("redisAddress").GetString() + ":6379",
            PublishedCluster.RedisImage, "redis-server", "--appendonly", "no"], 2.8, 2.9, Hash(container) + "\n");
        Add(["cp", path, container + ":/data/dump.rdb"], 3.6, 3.7);
        Add(["start", container], 3.71, 3.75, container + "\n");
        Add(["exec", container, "redis-cli", "PING"], 3.8, 3.85, "PONG\n");
    }

    private void AddInitialBackend()
    {
        const string owner = "p1r-11111111111111111111111111111111", root = "/synthetic/contract-fixture/" + owner;
        string container = owner + "-redis-1", volume = owner + "-redis-data-1";
        void Add(string[] args, int at, string output) => Commands.Add(new(new("docker", args, root, TimeSpan.FromSeconds(30)), DateTimeOffset.UnixEpoch.AddSeconds(at), DateTimeOffset.UnixEpoch.AddSeconds(at + 0.5), 0, output, "", Hash(output)));
        Add(["volume", "create", "--label", "p1r.owner=" + owner, volume], -20, volume + "\n");
        Add(["create", "--name", container, "--label", "p1r.owner=" + owner, "--mount", "type=volume,source=" + volume + ",destination=/data", "-p", "127.0.0.1:6379:6379",
            PublishedCluster.RedisImage, "redis-server", "--appendonly", "no"], -19, Hash(container) + "\n");
        Add(["start", container], -18, container + "\n"); Add(["exec", container, "redis-cli", "PING"], -17, "PONG\n");
    }

    private void MatrixCleanup(Dictionary<string, string> records, DateTimeOffset began)
    {
        string owner = "p1r-11111111111111111111111111111111", root = "/synthetic/contract-fixture/" + owner;
        string sharedId = Hash("actual-shared-container"), sharedImage = "sha256:" + Hash("actual-shared-image");
        string started = "2000-01-01T00:00:00Z";
        string normalized = sharedId + "|" + sharedImage + "|" + started + "|True";
        string[] containers = [owner + "-redis-1", owner + "-redis-2"], volumes = [owner + "-redis-data-1", owner + "-redis-data-2"];
        records["cleanup-runtime"] = JsonSerializer.Serialize(new { owner, root });
        void Add(string[] args, DateTimeOffset start, string output = "", int exit = 0, string error = "")
            => Commands.Add(new(new("docker", args, root, TimeSpan.FromSeconds(15)), start, start.AddMilliseconds(50), exit, output, error, Hash(output + error)));
        void Inventory(DateTimeOffset start)
        {
            Add(["ps", "-aq", "--no-trunc"], start, sharedId + "\n");
            Add(["inspect", "--format", QualificationRuntime.ContainerIdentityFormat, sharedId], start.AddMilliseconds(100),
                JsonSerializer.Serialize(new[] { new { Id = sharedId, Name = "/preserved-shared", Image = sharedImage,
                    Config = new { Image = "foreign-image:stable", Labels = new Dictionary<string, string?> { ["p1r.owner"] = null } },
                    State = new { StartedAt = started, Running = true } } }));
        }
        DateTimeOffset baseline = DateTimeOffset.UnixEpoch.AddSeconds(-30);
        Inventory(baseline);
        Add(["create", "--name", owner + "-redis-2", "--label", "p1r.owner=" + owner, PublishedCluster.RedisImage], began.AddSeconds(-4));
        Add(["volume", "create", "--label", "p1r.owner=" + owner, owner + "-redis-data-2"], began.AddSeconds(-3));
        containers = Commands.Where(command => command.Command.FileName == "docker" && command.Command.WorkingDirectory == root && command.Command.Arguments.FirstOrDefault() == "create")
            .Select(command => command.Command.Arguments[Array.IndexOf(command.Command.Arguments, "--name") + 1]).Distinct(StringComparer.Ordinal).ToArray();
        volumes = Commands.Where(command => command.Command.FileName == "docker" && command.Command.WorkingDirectory == root && command.Command.Arguments.Take(2).SequenceEqual(new[] { "volume", "create" }))
            .Select(command => command.Command.Arguments[^1]).Distinct(StringComparer.Ordinal).ToArray();
        double cleanupSpan = (containers.Length + volumes.Length) * 0.6 + 2;
        for (int pass = 0; pass < 3; pass++)
        {
            DateTimeOffset start = began.AddSeconds(pass * (cleanupSpan + 1)); int resource = 0;
            foreach (string name in containers)
            {
                DateTimeOffset time = start.AddSeconds(0.2 + resource++ * 0.6);
                string[] inspect = ["inspect", "--format", QualificationRuntime.ContainerIdentityFormat, name];
                if (pass == 0)
                {
                    string id = Hash(name);
                    Add(inspect, time, JsonSerializer.Serialize(new[] { new { Id = id, Name = "/" + name, Image = "sha256:" + Hash("owned-image"),
                        Config = new { Image = PublishedCluster.RedisImage, Labels = new Dictionary<string, string> { ["p1r.owner"] = owner } },
                        State = new { StartedAt = started, Running = false } } }));
                    Add(["rm", "-fv", id], time.AddMilliseconds(100));
                    Add(inspect, time.AddMilliseconds(200), exit: 1, error: "Error: no such object: " + name);
                }
                else Add(inspect, time, exit: 1, error: "Error: no such object: " + name);
            }
            foreach (string name in volumes)
            {
                DateTimeOffset time = start.AddSeconds(0.2 + resource++ * 0.6); string[] inspect = ["volume", "inspect", name];
                if (pass == 0)
                {
                    Add(inspect, time, JsonSerializer.Serialize(new[] { new { Name = name, Labels = new Dictionary<string, string> { ["p1r.owner"] = owner } } }));
                    Add(["volume", "rm", name], time.AddMilliseconds(100));
                    Add(inspect, time.AddMilliseconds(200), exit: 1, error: "Error: no such volume: " + name);
                }
                else Add(inspect, time, exit: 1, error: "Error: no such volume: " + name);
            }
            Inventory(start.AddSeconds(cleanupSpan - 1));
            string scanId = Hash(owner + "|" + start.ToString("O"))[..32];
            ProcessScans.Add(new(scanId, owner, start.AddSeconds(cleanupSpan - 1.5), start.AddSeconds(cleanupSpan - 1.4), []));
            CleanupPasses.Add(new(owner, root, start, start.AddSeconds(cleanupSpan), baseline, baseline.AddSeconds(1), normalized, normalized, containers, volumes, 0, 0, scanId, pass == 2));
        }
    }

    private static object MetadataValue(ConsumerBuild build, long? floor)
        => build.Version == "3.70.1" ? new { currentSequence = 12, eTag = "old", lastModified = DateTimeOffset.UnixEpoch }
            : (object)new { currentSequence = 12, eTag = "old", lastModified = DateTimeOffset.UnixEpoch, retainedFloor = floor ?? 1 };

    private static string ProbeOutput(string scenario, string name, int index, ConsumerBuild build)
    {
        if (scenario == "mixed-api" && name.StartsWith("client-", StringComparison.Ordinal))
            return JsonSerializer.Serialize(CrossClient(name, CrossAction(name, index), build));
        var result = JsonNode.Parse(Identities(build))!.AsObject();
        if (scenario is "legacy-metadata" or "metadata-read")
            result["value"] = JsonSerializer.SerializeToNode(MetadataValue(build, scenario == "metadata-read"
                ? long.Parse(name.Split('-')[^1], System.Globalization.CultureInfo.InvariantCulture) : null));
        else if (name == "real-signed-admission-and-refusals")
        {
            foreach (string field in new[] { "verified", "principalsBound", "missingRefused", "forgedRefused", "foreignTenantRefused" }) result[field] = true;
            result["value"] = JsonNode.Parse(HistoricalMatrixWireContract.ReadFixture().GetProperty("querySyntheticSource").GetRawText());
        }
        else if (scenario == "mixed-api")
        {
            result["supported"] = build.Version != "3.70.1";
            if (build.Version == "3.70.1")
            { result["denial"] = "incapable-published-endpoint"; foreach (string field in new[] { "protectedAccess", "effect", "write" }) result[field] = false; }
            else
            { foreach (string field in new[] { "decoded", "changed", "foreignCaller", "foreignTenant", "foreignDomain", "changedFilter", "changedQuery", "tamperDenied" }) result[field] = true;
              result["position"] = "987"; result["unknownDenied"] = 3; }
        }
        else if (scenario is "query-wire" or "projection-wire")
        {
            result["wire"] = WireOutput(scenario, name, index); result["value"] = WireValue(scenario, name, index);
        }
        return result.ToJsonString();
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
        if (scenario == "legacy-metadata")
        {
            if (name.EndsWith("-three-argument-constructor", StringComparison.Ordinal)) return "{\"operation\":\"legacy-constructor\"}";
            string value = name.EndsWith("-True", StringComparison.Ordinal)
                ? "{\"CurrentSequence\":12,\"LastModified\":\"1970-01-01T00:00:00+00:00\",\"ETag\":\"old\"}"
                : "{\"currentSequence\":12,\"lastModified\":\"1970-01-01T00:00:00+00:00\",\"eTag\":\"old\"}";
            return JsonSerializer.Serialize(new { operation = "metadata", value });
        }
        if (scenario == "metadata-read") return JsonSerializer.Serialize(new { operation = "metadata", value = JsonSerializer.Serialize(new
            { currentSequence = 12, lastModified = DateTimeOffset.UnixEpoch, eTag = "old", retainedFloor = int.Parse(name.Split('-')[^1], System.Globalization.CultureInfo.InvariantCulture) }) });
        if (scenario == "mixed-api") return name.StartsWith("client-", StringComparison.Ordinal)
            ? JsonSerializer.Serialize(new { operation = "cross-client", sidecar = "http://127.0.0.1:33000", actor = "tenant-a:counter:synthetic", action = CrossAction(name, index) }) : "{\"operation\":\"cursor\"}";
        if (name == "real-signed-admission-and-refusals") return "{\"operation\":\"signed-query\"}";
        string kind = scenario == "query-wire" ? "query" : "projection";
        if (name.EndsWith("-xml", StringComparison.Ordinal) && index == 0) return JsonSerializer.Serialize(new { operation = "make-wire", kind, value = WireInputValue(scenario, name, index) });
        return JsonSerializer.Serialize(new { operation = "wire", kind, serializer = name.EndsWith("-xml", StringComparison.Ordinal) ? "xml" : "json", value = WireInputValue(scenario, name, index) });
    }

    private static JsonObject WireValue(string scenario, string name, int index)
    {
        var fixture = HistoricalMatrixWireContract.ReadFixture();
        var source = JsonNode.Parse(fixture.GetProperty(scenario == "query-wire" ? "querySyntheticSource" : "projectionSource").GetRawText())!.AsObject();
        var fields = fixture.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("Scenario").GetString() == scenario && row.GetProperty("Case").GetString() == name).GetProperty("Hops")[index];
        var result = new JsonObject();
        foreach (var field in fields.EnumerateObject()) result[field.Name] = field.Value.GetString() switch
        {
            "source" => source[field.Name]?.DeepClone(), "null" => null, "false" => JsonValue.Create(false), "true" => JsonValue.Create(true),
            "zero" => JsonValue.Create(0), "empty" => JsonValue.Create(""), "empty-array" => new JsonArray(), _ => throw new InvalidOperationException("Unknown synthetic historical state."),
        };
        return result;
    }

    private static string WireOutput(string scenario, string name, int index)
    {
        string json = WireValue(scenario, name, index).ToJsonString();
        return name.EndsWith("-xml", StringComparison.Ordinal) ? Convert.ToBase64String(Encoding.UTF8.GetBytes("synthetic-xml:" + json)) : json;
    }

    private static string WireInputValue(string scenario, string name, int index)
        => index == 0 ? JsonNode.Parse(HistoricalMatrixWireContract.ReadFixture().GetProperty(scenario == "query-wire" ? "querySyntheticSource" : "projectionSource").GetRawText())!.ToJsonString()
            : WireOutput(scenario, name, index - 1);

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
        string root = "/synthetic/contract-fixture/p1r-11111111111111111111111111111111/consumers/" + key;
        string configuration = source ? "Debug" : "Release";
        string[] names = new[] { "Client", "Contracts", "DomainService", "Server", "ServiceDefaults" }
            .Concat(role == "Host" && version == "3.128.2" ? ["Gateway"] : []).Select(name => "Hexalith.EventStore." + name).ToArray();
        return new(role, version, configuration, root + "/" + role + ".csproj", root + "/bin/" + configuration + "/net10.0/" + role + ".dll", root + "/packages",
            Hash("assets"), Hash("lock"), source ? "935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8" : null,
            names.ToDictionary(name => name, _ => "935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8"), names.ToDictionary(name => name, _ => source ? "3.128.0" : version),
            Hash("project"), names.ToDictionary(name => name, Hash), Hash("sdk-policy"), "10.0.301", "/synthetic/global.json",
            source ? names.ToDictionary(name => name, Hash) : null,
            new[] { role + ".dll", role + ".deps.json", role + ".runtimeconfig.json" }.ToDictionary(name => name, name => Hash(key + ":" + name)));
    }
}
