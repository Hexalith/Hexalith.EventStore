using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Runs versioned actors in a private Dapr 1.18.4/Redis topology, including provider-level fresh-database restores.</summary>
internal sealed class PublishedCluster(QualificationRuntime runtime, IReadOnlyList<ConsumerBuild> builds) : IAsyncDisposable
{
    internal const string RedisImage = "redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly List<OwnedProcess> _writers = [];
    private string _redis = string.Empty;
    private string _binaries = string.Empty;
    private int _redisPort;
    private int _placementPort;
    private int _schedulerPort;
    private int _domainPort;
    private int _hostPort;
    private int _sidecarPort;
    private string _appId = string.Empty;
    private int _databaseNumber;
    private string _querySigningKeyPath = string.Empty;
    private string _queryVerificationKey = string.Empty;

    /// <summary>Downloads private pinned binaries without touching the installed Dapr runtime.</summary>
    internal async Task InitializeAsync()
    {
        await runtime.CaptureSharedContainersAsync();
        using (RSA queryKey = RSA.Create(2048))
        {
            _querySigningKeyPath = Path.Combine(runtime.Root, "query-signing-key.pem");
            runtime.RegisterSensitivePath(_querySigningKeyPath);
            await File.WriteAllTextAsync(_querySigningKeyPath, queryKey.ExportRSAPrivateKeyPem());
            _queryVerificationKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(queryKey.ExportRSAPublicKeyPem()));
        }
        _binaries = Path.Combine(runtime.Root, "dapr-1.18.4");
        Directory.CreateDirectory(_binaries);
        using var download = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        foreach (string binary in new[] { "daprd", "placement", "scheduler" })
        {
            string name = binary + "_linux_amd64.tar.gz";
            byte[] bytes = await download.GetByteArrayAsync("https://github.com/dapr/dapr/releases/download/v1.18.4/" + name);
            string checksum = (await download.GetStringAsync("https://github.com/dapr/dapr/releases/download/v1.18.4/" + name + ".sha256"))
                .Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
            if (!string.Equals(QualificationRuntime.Hash(bytes), checksum, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Dapr archive checksum mismatch.");
            string archive = Path.Combine(_binaries, name);
            await File.WriteAllBytesAsync(archive, bytes);
            await runtime.RequireAsync("tar", ["-xf", archive, "-C", _binaries]);
        }
        string version = await runtime.RequireAsync(Path.Combine(_binaries, "daprd"), ["--version"]);
        if (version.Trim() != "1.18.4") throw new InvalidOperationException("Wrong private daprd runtime version: " + version);
        await runtime.RequireAsync("docker", ["image", "inspect", RedisImage]);
        _placementPort = Port();
        int raft = Port();
        runtime.Start(new(Path.Combine(_binaries, "placement"), ["--port", _placementPort.ToString(), "--listen-address", "127.0.0.1",
            "--healthz-port", Port().ToString(), "--metrics-port", Port().ToString(), "--initial-cluster", "dapr-placement-0=127.0.0.1:" + raft,
            "--log-level", "warn"], runtime.Root, TimeSpan.FromMinutes(1)));
        _schedulerPort = Port();
        string schedulerData = Path.Combine(runtime.Root, "scheduler-data");
        runtime.RegisterSensitivePath(schedulerData);
        runtime.Start(new(Path.Combine(_binaries, "scheduler"), ["--port", _schedulerPort.ToString(), "--listen-address", "127.0.0.1",
            "--healthz-port", Port().ToString(), "--metrics-port", Port().ToString(), "--etcd-client-port", Port().ToString(),
            "--etcd-initial-cluster", "dapr-scheduler-server-0=http://127.0.0.1:" + Port(), "--etcd-data-dir", schedulerData,
            "--override-broadcast-host-port", "127.0.0.1:" + _schedulerPort, "--log-level", "warn"], runtime.Root, TimeSpan.FromMinutes(1)));
        _appId = runtime.Id;
        await FreshDatabaseAsync();
        await StartWriterAsync(Build("Host", ConsumerMaterializer.SelectedVersion), Build("Domain", ConsumerMaterializer.SelectedVersion));
        JsonElement warmup = await ExecuteAsync(Address("tenant-a"), append: true);
        if (!IsOk(warmup)) throw new InvalidOperationException("Production actor prerequisite failed: " + warmup.GetRawText());
    }

    /// <summary>Executes both canonical package directions plus independent matching tagged-source semantics.</summary>
    internal async Task ExecuteMatrixAsync(List<ScenarioObservation> observations)
    {
        ConsumerBuild selected = Build("Host", ConsumerMaterializer.SelectedVersion);
        foreach (string old in new[] { "3.70.1", "3.110.0", "tagged-source" })
        foreach ((string writer, string reader) in new[] { (old, ConsumerMaterializer.SelectedVersion), (ConsumerMaterializer.SelectedVersion, old) })
        {
            string direction = writer + "-to-" + reader;
            await PublishedScenarioTests.ObserveAsync(observations, "full-replay", direction, async assertions =>
            {
                await StartWriterAsync(Build("Host", writer), Build("Domain", writer));
                string actor = Address("tenant-a");
                string other = "tenant-b:" + actor.Split(':', 2)[1];
                await AppendAsync(actor, 12, assertions);
                await AppendAsync(other, 3, assertions);
                var before = await InventoryAsync(actor, 12);
                var isolated = await InventoryAsync(other, 3);
                await StartWriterAsync(Build("Host", reader), Build("Domain", reader));
                JsonElement result = await ExecuteAsync(actor, append: false);
                assertions.Check(IsOk(result) && result.GetProperty("count").GetInt32() == 12 && result.GetProperty("sequence").GetInt64() == 12, "fresh-reader-full-fold");
                assertions.Check(Equal(before, await InventoryAsync(actor, 12)), "original-event-metadata-inventory");
                assertions.Check(Equal(isolated, await InventoryAsync(other, 3)), "second-tenant-unchanged");
                return IsOk(result) ? "compatible" : "incompatible";
            });
            foreach (string variant in new[] { "snapshot-tail", "retained-covered", "retained-uncovered", "retained-uncovered-absent", "missing-event", "missing-event-tail", "metadata-write" })
            {
                string scenario = variant == "retained-uncovered-absent" ? "retained-uncovered" : variant == "missing-event-tail" ? "missing-event" : variant;
                await PublishedScenarioTests.ObserveAsync(observations, scenario, direction + "-" + variant, async assertions =>
                {
                    await StartWriterAsync(Build("Host", writer), Build("Domain", writer));
                    string actor = Address("tenant-a");
                    string other = "tenant-b:" + actor.Split(':', 2)[1];
                    await AppendAsync(actor, 12, assertions);
                    await AppendAsync(other, 3, assertions);
                    await ConfigureHistoryAsync(actor, variant);
                    var before = await InventoryAsync(actor, 12);
                    var isolated = await InventoryAsync(other, 3);
                    await StartWriterAsync(Build("Host", reader), Build("Domain", reader));
                    bool append = scenario == "metadata-write";
                    JsonElement result = await ExecuteAsync(actor, append);
                    bool refused = scenario is "retained-uncovered" or "missing-event";
                    assertions.Check(IsOk(result) != refused, refused ? "required-history-refused" : "covering-history-folded");
                    if (IsOk(result)) assertions.Check(result.GetProperty("count").GetInt32() == 12, "snapshot-plus-tail-equals-full-fold");
                    if (!append || !IsOk(result)) assertions.Check(Equal(before, await InventoryAsync(actor, 12)), "history-unchanged");
                    assertions.Check(Equal(isolated, await InventoryAsync(other, 3)), "second-tenant-unchanged");
                    bool capable = true;
                    if (append && IsOk(result))
                    {
                        assertions.Check(result.GetProperty("sequence").GetInt64() == 13, "actual-writer-appended-thirteen");
                        JsonElement metadata = result.GetProperty("metadata");
                        capable = metadata.TryGetProperty("retainedFloor", out JsonElement floor) && floor.GetInt64() == 5;
                        var after = await InventoryAsync(actor, 13);
                        foreach ((string key, string? hash) in before.Where(pair => !pair.Key.EndsWith(":metadata", StringComparison.Ordinal)))
                            assertions.Check(after.GetValueOrDefault(key) == hash, "prior-bytes-preserved-" + key);
                        await StartWriterAsync(Build("Host", reader), Build("Domain", reader));
                        JsonElement replay = await ExecuteAsync(actor, false);
                        assertions.Check(IsOk(replay) && replay.GetProperty("count").GetInt32() == 13, "append-survives-fresh-host-sidecar");
                        assertions.Check(Equal(after, await InventoryAsync(actor, 13)), "post-restart-inventory");
                    }
                    return assertions.Failures.Count == 0 && capable ? "compatible" : "incompatible";
                });
            }
        }
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host"))
        {
            ConsumerBuild domain = builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source);
            foreach (string invalid in new[] { "floor-0", "floor-14", "unreadable", "protected-mismatch", "unknown-type", "version-987" })
            foreach (string tenant in new[] { "tenant-a", "tenant-b" })
            {
                await PublishedScenarioTests.ObserveAsync(observations, "invalid-evidence", host.Version + "-" + host.Configuration + "-" + invalid + "-" + tenant, async assertions =>
                {
                    await StartWriterAsync(host, domain);
                    string actor = Address(tenant);
                    await AppendAsync(actor, 2, assertions);
                    await InvalidHistoryAsync(actor, invalid);
                    var before = await InventoryAsync(actor, 3);
                    await StartWriterAsync(host, domain);
                    JsonElement reader = await GetAsync("http://127.0.0.1:" + _hostPort + "/reader/" + Uri.EscapeDataString(actor));
                    assertions.Check(reader.TryGetProperty("ok", out _), "actual-versioned-reader-executed");
                    JsonElement domainResult = await ReplayDomainFixtureAsync(actor);
                    if (!invalid.StartsWith("floor-", StringComparison.Ordinal))
                    {
                        assertions.Check(!IsOk(domainResult), "actual-domain-invalid-replay-refused");
                        if (domainResult.TryGetProperty("applyCalls", out JsonElement applied)) assertions.Check(applied.GetInt32() == 0, "whole-batch-admitted-before-apply");
                        if (domainResult.TryGetProperty("handleCalls", out JsonElement handled)) assertions.Check(handled.GetInt32() == 0, "invalid-batch-never-handled");
                    }
                    JsonElement result = await ExecuteAsync(actor, true);
                    assertions.Check(!IsOk(result), "invalid-history-typed-refusal");
                    assertions.Check(result.TryGetProperty("error", out JsonElement error) && !string.IsNullOrWhiteSpace(error.GetString()), "typed-error-observed");
                    assertions.Check(Equal(before, await InventoryAsync(actor, 3)), "refusal-preserves-domain-inventory");
                    return IsOk(result) ? "incompatible" : "compatible";
                });
            }
        }
        await FloorBoundaryCasesAsync(observations);
        await RestoreCasesAsync(observations, selected);
        await CapabilityCasesAsync(observations);
        await PublishedScenarioTests.ObserveAsync(observations, "checkout", "package-to-exact-tag-source", async assertions =>
        {
            string actor = Address("tenant-a");
            await StartWriterAsync(selected, Build("Domain", selected.Version));
            await AppendAsync(actor, 12, assertions);
            var before = await InventoryAsync(actor, 12);
            JsonElement package = await ExecuteAsync(actor, false);
            await StartWriterAsync(Build("Host", selected.Version, source: true), Build("Domain", selected.Version, source: true));
            JsonElement source = await ExecuteAsync(actor, false);
            assertions.Check(package.GetRawText() == source.GetRawText(), "matching-persisted-state-semantics");
            assertions.Check(Equal(before, await InventoryAsync(actor, 12)), "same-persisted-coordinates-and-bytes");
            return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
        });
    }

    private async Task FloorBoundaryCasesAsync(List<ScenarioObservation> observations)
    {
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host"))
        foreach (int floor in new[] { 0, 1, 5, 13, 14 })
        {
            ConsumerBuild domain = builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source);
            await PublishedScenarioTests.ObserveAsync(observations, floor is 0 or 14 ? "invalid-evidence" : "metadata-write",
                host.Version + "-" + host.Configuration + "-head12-floor" + floor, async assertions =>
            {
                await StartWriterAsync(host, domain);
                string actor = Address("tenant-a");
                await AppendAsync(actor, 12, assertions);
                JsonNode metadata = JsonNode.Parse((await ReadAsync(actor, actor + ":metadata"))!)!;
                metadata["retainedFloor"] = floor;
                if (metadata["sourceProof"] is JsonNode proof) proof["retainedFloor"] = floor;
                var operations = new List<object> { new { operation = "upsert", key = actor + ":metadata", value = (object)metadata } };
                if (floor is >= 1 and <= 13)
                {
                    int snapshot = floor == 13 ? 12 : 9;
                    operations.Add(new { operation = "upsert", key = actor + ":snapshot", value = (object)new
                    {
                        sequenceNumber = snapshot, state = new { count = snapshot }, createdAt = DateTimeOffset.UnixEpoch,
                        tenantId = "tenant-a", domain = "counter", aggregateId = actor.Split(':')[2],
                    } });
                    for (int sequence = 1; sequence < floor; sequence++) operations.Add(new { operation = "delete", key = actor + ":events:" + sequence });
                }
                await FixtureAsync(actor, operations);
                var before = await InventoryAsync(actor, 13);
                await StartWriterAsync(host, domain);
                if (floor is 0 or 14)
                {
                    JsonElement writer = await PostAsync("http://127.0.0.1:" + _hostPort + "/writer-admission/" + Uri.EscapeDataString(actor), new { });
                    assertions.Check(writer.GetProperty("refused").GetBoolean(), "actual-writer-malformed-floor-refused");
                    foreach (string field in new[] { "protectionCalls", "allocations", "staging" })
                        assertions.Check(writer.GetProperty(field).GetInt32() == 0, "malformed-floor-before-" + field);
                    assertions.Check(Equal(before, await InventoryAsync(actor, 13)), "writer-admission-inventory-unchanged");
                }
                else
                {
                    JsonElement result = await ExecuteAsync(actor, true);
                    assertions.Check(IsOk(result) && result.GetProperty("sequence").GetInt64() == 13 && result.GetProperty("count").GetInt32() == 12, "boundary-floor-real-append");
                    assertions.Check(IsOk(result) && result.GetProperty("metadata").TryGetProperty("retainedFloor", out JsonElement retained) && retained.GetInt32() == floor, "boundary-floor-preserved");
                    var committed = await InventoryAsync(actor, 13);
                    foreach ((string key, string? hash) in before.Where(pair => !pair.Key.EndsWith(":metadata", StringComparison.Ordinal) && !pair.Key.EndsWith(":events:13", StringComparison.Ordinal)))
                        assertions.Check(committed.GetValueOrDefault(key) == hash, "boundary-prior-bytes-" + key);
                    await StartWriterAsync(host, domain);
                    JsonElement replay = await ExecuteAsync(actor, false);
                    assertions.Check(IsOk(replay) && replay.GetProperty("count").GetInt32() == 13, "boundary-floor-fresh-replay");
                }
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
        }
    }

    private async Task<JsonElement> ReplayDomainFixtureAsync(string actor)
    {
        var events = new List<object>();
        for (int sequence = 1; sequence <= 2; sequence++)
        {
            using JsonDocument stored = JsonDocument.Parse((await ReadAsync(actor, actor + ":events:" + sequence))!);
            var metadata = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (JsonProperty property in stored.RootElement.EnumerateObject())
                if (property.Name is not ("payload" or "extensions")) metadata.Add(property.Name, property.Value.Clone());
            events.Add(new { metadata, payload = stored.RootElement.GetProperty("payload").GetString(),
                extensions = stored.RootElement.TryGetProperty("extensions", out JsonElement extensions) ? extensions.Clone() : (JsonElement?)null });
        }
        string[] address = actor.Split(':');
        return await PostAsync("http://127.0.0.1:" + _domainPort + "/process", new
        {
            command = new { messageId = "probe", tenantId = address[0], domain = address[1], aggregateId = address[2], commandType = "Inspect",
                payload = "e30=", correlationId = "corr", causationId = "cause", userId = "fixture-user", extensions = (object?)null },
            currentState = new { snapshotState = (object?)null, events, lastSnapshotSequence = 0, currentSequence = 2 },
        });
    }

    private async Task CapabilityCasesAsync(List<ScenarioObservation> observations)
    {
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host"))
        {
            ConsumerBuild domain = builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source);
            await PublishedScenarioTests.ObserveAsync(observations, "mixed-api", host.Version + "-" + host.Configuration + "-persisted-status-tristate", async assertions =>
            {
                await StartWriterAsync(host, domain);
                string prefix = "status-" + Guid.NewGuid().ToString("N");
                var expected = new Dictionary<string, bool?> { [prefix + "-legacy"] = null, [prefix + "-terminal"] = false, [prefix + "-automatic"] = true };
                var original = new Dictionary<string, string>();
                foreach ((string message, bool? retryable) in expected)
                {
                    JsonElement value = await PostAsync("http://127.0.0.1:" + _hostPort + "/status-fixture/" + message, new { retryable });
                    original.Add(message, value.GetRawText());
                    assertions.Check(value.TryGetProperty("retryable", out JsonElement flag) && (retryable is null
                        ? flag.ValueKind == JsonValueKind.Null : flag.ValueKind is JsonValueKind.True or JsonValueKind.False && flag.GetBoolean() == retryable.Value), "persisted-status-tristate-" + retryable);
                }
                await StartWriterAsync(host, domain);
                foreach ((string message, string prior) in original)
                {
                    JsonElement restored = await GetAsync("http://127.0.0.1:" + _hostPort + "/status-fixture/" + message);
                    assertions.Check(restored.GetRawText() == prior, "status-tristate-fresh-process-readback");
                }
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
            await PublishedScenarioTests.ObserveAsync(observations, "mixed-api", host.Version + "-" + host.Configuration, async assertions =>
            {
                await StartWriterAsync(host, domain);
                string actor = Address("tenant-a");
                var before = await InventoryAsync(actor, 2);
                JsonElement result = await PostAsync("http://127.0.0.1:" + _hostPort + "/capabilities/" + actor.Split(':')[2], new { });
                bool supported = result.GetProperty("supported").GetBoolean();
                if (!supported)
                {
                    assertions.Check(result.GetProperty("denial").GetString() == "incapable-published-endpoint", "typed-old-endpoint-refusal");
                    assertions.Check(Equal(before, await InventoryAsync(actor, 2)), "unsupported-route-inventory-unchanged");
                    return "incompatible";
                }
                foreach (string field in new[] { "stale", "forged", "completedStale", "accepted", "unauthorized", "forgedEffect", "replayed" })
                    assertions.Check(result.TryGetProperty(field, out JsonElement value) && value.GetBoolean(), "actual-fenced-trusted-" + field);
                assertions.Check(result.TryGetProperty("sequence", out JsonElement sequence) && sequence.GetInt64() == 2, "two-actual-persisted-effects");
                assertions.Check(result.TryGetProperty("before", out JsonElement prior) && result.TryGetProperty("afterDenial", out JsonElement after) && prior.GetInt64() == after.GetInt64(), "denied-effects-preserve-sequence");
                var committed = await InventoryAsync(actor, 2);
                await StartWriterAsync(host, domain);
                JsonElement replay = await ExecuteAsync(actor, false);
                assertions.Check(IsOk(replay) && replay.GetProperty("count").GetInt32() == 2, "protected-effects-survive-fresh-processes");
                assertions.Check(Equal(committed, await InventoryAsync(actor, 2)), "protected-effects-restart-preserves-bytes");
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
            if (host.Version != ConsumerMaterializer.SelectedVersion) continue;
            await ExecuteQueryBoundaryAsync(observations, host, domain);
            await PublishedScenarioTests.ObserveAsync(observations, "registered-evolution", host.Configuration + "-registered-manifest-production-hop", async assertions =>
            {
                await StartWriterAsync(host, domain);
                JsonElement manifest = await GetAsync("http://127.0.0.1:" + _hostPort + "/manifest");
                assertions.Check(manifest.GetProperty("registered").GetBoolean()
                    && manifest.GetProperty("fingerprint").GetString()!.Length == 64, "manifest-candidate-registered-through-published-api");
                string aggregate = Guid.NewGuid().ToString("N");
                string actor = "tenant-a:d:" + aggregate;
                await FixtureAsync(actor, new object[]
                {
                    new { operation = "upsert", key = actor + ":metadata", value = (object)new
                        { currentSequence = 1, lastModified = DateTimeOffset.UnixEpoch, eTag = "p1r-manifest", retainedFloor = 1 } },
                    new { operation = "upsert", key = actor + ":events:1", value = (object)new
                        { messageId = "manifest-event", aggregateId = aggregate, aggregateType = "r", tenantId = "tenant-a", domain = "d",
                            sequenceNumber = 1, globalPosition = 987, timestamp = DateTimeOffset.UnixEpoch, correlationId = "corr", causationId = "cause",
                            userId = "fixture-user", domainServiceVersion = "v1", eventTypeName = "Legacy.Event", metadataVersion = 1,
                            serializationFormat = "json", payload = Convert.ToBase64String("{}"u8.ToArray()), extensions = (object?)null } },
                });
                var before = await InventoryAsync(actor, 2);
                await StartWriterAsync(host, domain);
                JsonElement result = await ExecuteAsync(actor, true);
                assertions.Check(!IsOk(result), "production-required-logical-hop-refused-without-bound-callable");
                assertions.Check(result.TryGetProperty("error", out JsonElement error) && !string.IsNullOrWhiteSpace(error.GetString()), "typed-production-evolution-refusal");
                assertions.Check(Equal(before, await InventoryAsync(actor, 2)), "production-manifest-refusal-preserves-original-event-bytes");
                JsonElement stats = await GetAsync("http://127.0.0.1:" + _domainPort + "/stats");
                assertions.Check(stats.GetProperty("applyCalls").GetInt32() == 0 && stats.GetProperty("handleCalls").GetInt32() == 0, "manifest-refusal-before-domain-apply-handle");
                return "incompatible";
            });
            await PublishedScenarioTests.ObserveAsync(observations, "registered-evolution", host.Configuration, async assertions =>
            {
                await StartWriterAsync(host, domain);
                string actor = Address("tenant-a");
                await AppendAsync(actor, 1, assertions);
                var before = await InventoryAsync(actor, 1);
                byte[] bytes = "{\"Amount\":7}"u8.ToArray();
                JsonElement persisted = await PostAsync("http://127.0.0.1:" + _hostPort + "/evolution", new { payload = Convert.ToBase64String(bytes) });
                JsonElement result = persisted.GetProperty("domain");
                assertions.Check(IsOk(result) && result.GetProperty("count").GetInt32() == 7, "registered-upcaster-used-by-sdk-aggregate");
                assertions.Check(result.TryGetProperty("inputUnchanged", out JsonElement stable) && stable.GetBoolean(), "original-evolution-bytes-unchanged");
                assertions.Check(persisted.GetProperty("persistedBytesUnchanged").GetBoolean()
                    && persisted.GetProperty("beforeHash").GetString() == persisted.GetProperty("afterHash").GetString(), "dapr-stored-original-evolution-bytes-unchanged");
                assertions.Check(Equal(before, await InventoryAsync(actor, 1)), "stored-event-inventory-unchanged");
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
            await PublishedScenarioTests.ObserveAsync(observations, "natural-reminder", host.Configuration, async assertions =>
            {
                await StartWriterAsync(host, domain);
                string aggregate = Guid.NewGuid().ToString("N");
                string actor = "tenant-a:counter:" + aggregate;
                DateTimeOffset due = DateTimeOffset.UtcNow.AddSeconds(15);
                JsonElement registered = await PostAsync("http://127.0.0.1:" + _hostPort + "/reminder/" + aggregate, new { dueUtc = due });
                assertions.Check(registered.GetProperty("accepted").GetBoolean() && registered.GetProperty("armed").GetInt32() == 1, "stream-derived-reminder-armed");
                assertions.Check(registered.GetProperty("submitted").GetInt32() == 0, "submitted-zero-before-natural-delivery");
                string name = registered.GetProperty("reminderName").GetString()!;
                var before = await InventoryAsync(actor, 1);
                await StartWriterAsync(host, domain);
                JsonElement restarted = await GetAsync("http://127.0.0.1:" + _hostPort + "/reminder/" + aggregate + "/" + name);
                assertions.Check(restarted.GetProperty("submitted").GetInt32() == 0, "submitted-zero-after-restart-before-due");
                DateTimeOffset deadline = due.AddSeconds(40);
                JsonElement observed = restarted;
                while (DateTimeOffset.UtcNow < deadline && observed.GetProperty("submitted").GetInt32() == 0)
                {
                    await Task.Delay(500, TestContext.Current.CancellationToken);
                    observed = await GetAsync("http://127.0.0.1:" + _hostPort + "/reminder/" + aggregate + "/" + name);
                }
                assertions.Check(observed.GetProperty("submitted").GetInt32() == 1, "natural-scheduler-delivery-persisted-submission");
                JsonElement result = await ExecuteAsync(actor, false);
                assertions.Check(IsOk(result) && result.GetProperty("sequence").GetInt64() == 2 && result.GetProperty("count").GetInt32() == 2, "natural-delivery-target-effect-persisted");
                assertions.Check((await InventoryAsync(actor, 2)).GetValueOrDefault(actor + ":events:1") == before[actor + ":events:1"], "reminder-preserves-source-event-bytes");
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
        }
    }

    /// <summary>Executes the same actual capability/addition operations with the selected consumers for focused diagnostics.</summary>
    internal Task ExecuteSelectedCapabilitiesAsync(List<ScenarioObservation> observations) => CapabilityCasesAsync(observations);

    private async Task ExecuteQueryBoundaryAsync(List<ScenarioObservation> observations, ConsumerBuild host, ConsumerBuild domain)
    {
        await PublishedScenarioTests.ObserveAsync(observations, "query-wire", host.Configuration + "-supported-handler-and-actor-routes", async assertions =>
        {
            await StartWriterAsync(host, domain);
            string aggregate = Guid.NewGuid().ToString("N");
            string projection = "http://127.0.0.1:" + _hostPort + "/project-model/" + aggregate;
            JsonElement applied = await PostAsync(projection, new { position = 987, sequence = 12 });
            assertions.Check(applied.GetProperty("watermark").GetInt64() == 987 && applied.GetProperty("sequence").GetInt64() == 12, "read-model-durably-applied-987-distinct-from-sequence12");
            JsonElement duplicate = await PostAsync(projection, new { position = 987, sequence = 12 });
            assertions.Check(duplicate.GetProperty("count").GetInt32() == 1, "duplicate-projection-delivery-converges");
            foreach (string serializer in new[] { "json", "xml" })
            {
                JsonElement response = await PostAsync("http://127.0.0.1:" + _hostPort + "/query-route/" + aggregate, new { serializer, mode = "valid" });
                assertions.Check(response.GetProperty("success").GetBoolean(), "supported-" + serializer + "-query-route-succeeded");
                if (response.GetProperty("success").GetBoolean())
                {
                    JsonElement payload = response.GetProperty("payload");
                    foreach ((string field, string expected) in new[] { ("originalActorId", "actor"), ("authenticatedWorkloadId", "workload"),
                        ("delegationId", "delegation"), ("tenantId", "tenant-a"), ("correlationId", "corr") })
                        assertions.Check(payload.GetProperty(field).GetString() == expected, serializer + "-handler-preserves-" + field);
                    assertions.Check(payload.GetProperty("isDelegated").GetBoolean(), serializer + "-delegation-preserved");
                    assertions.Check(payload.GetProperty("scopes")[0].GetString() == "counter.read" && payload.GetProperty("audience")[0].GetString() == "eventstore", serializer + "-scopes-audience-preserved");
                    assertions.Check(!string.IsNullOrWhiteSpace(payload.GetProperty("identityAdmissionProof").GetString()), serializer + "-real-signed-proof-reaches-handler");
                    assertions.Check(payload.GetProperty("value").GetProperty("watermark").GetInt64() == 987, serializer + "-handler-reads-durable-watermark");
                }
                JsonElement before = await GetAsync("http://127.0.0.1:" + _domainPort + "/stats");
                foreach (string mode in new[] { "missing", "forged", "foreign-tenant", "foreign-actor" })
                {
                    JsonElement denied = await PostAsync("http://127.0.0.1:" + _hostPort + "/query-route/" + aggregate, new { serializer, mode });
                    assertions.Check(!denied.GetProperty("success").GetBoolean(), serializer + "-protected-query-refuses-" + mode);
                }
                JsonElement after = await GetAsync("http://127.0.0.1:" + _domainPort + "/stats");
                assertions.Check(after.GetProperty("queryReads").GetInt32() == before.GetProperty("queryReads").GetInt32(), serializer + "-denied-before-read-model-access");
            }
            return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
        });
        await PublishedScenarioTests.ObserveAsync(observations, "projection-wire", host.Configuration + "-durably-applied-watermark-cursor", async assertions =>
        {
            await StartWriterAsync(host, domain);
            string aggregate = Guid.NewGuid().ToString("N");
            await PostAsync("http://127.0.0.1:" + _hostPort + "/project-model/" + aggregate, new { position = 987, sequence = 12 });
            JsonElement first = await PostAsync("http://127.0.0.1:" + _hostPort + "/cursor-model/" + aggregate, new { mode = "valid" });
            string cursor = first.GetProperty("cursor").GetString()!;
            assertions.Check(first.GetProperty("decoded").GetBoolean() && first.GetProperty("position").GetString() == "987", "cursor-from-durably-applied-positive-watermark");
            await StartWriterAsync(host, domain);
            JsonElement restarted = await PostAsync("http://127.0.0.1:" + _hostPort + "/cursor-model/" + aggregate, new { mode = "valid", cursor });
            assertions.Check(restarted.GetProperty("decoded").GetBoolean() && restarted.GetProperty("model").GetProperty("watermark").GetInt64() == 987, "persisted-model-watermark-and-sdk-keyring-survive-restart");
            foreach (string mode in new[] { "foreign-actor", "foreign-tenant", "foreign-domain", "changed-filter", "changed-query", "tampered" })
            {
                JsonElement denied = await PostAsync("http://127.0.0.1:" + _hostPort + "/cursor-model/" + aggregate, new { mode, cursor });
                assertions.Check(!denied.GetProperty("decoded").GetBoolean(), "durable-cursor-refuses-" + mode);
            }
            await PostAsync("http://127.0.0.1:" + _hostPort + "/project-model/" + aggregate, new { position = 988, sequence = 13 });
            JsonElement advanced = await PostAsync("http://127.0.0.1:" + _hostPort + "/cursor-model/" + aggregate, new { mode = "valid", cursor });
            assertions.Check(!advanced.GetProperty("decoded").GetBoolean(), "old-cursor-refuses-after-durably-applied-watermark-advances");
            return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
        });
    }

    private async Task<JsonElement> PostAsync(string address, object request)
    {
        using var response = await _http.PostAsJsonAsync(address, request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private async Task<JsonElement> GetAsync(string address)
    {
        using var response = await _http.GetAsync(address, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private async Task RestoreCasesAsync(List<ScenarioObservation> observations, ConsumerBuild selected)
    {
        foreach (string old in new[] { "3.70.1", "3.110.0" })
        foreach (string scenario in new[] { "post-upgrade-restore", "pre-upgrade-restore" })
        {
            await PublishedScenarioTests.ObserveAsync(observations, scenario, old, async assertions =>
            {
                bool containment = scenario == "pre-upgrade-restore";
                ConsumerBuild writer = containment ? Build("Host", old) : selected;
                await StartWriterAsync(writer, Build("Domain", writer.Version));
                string actor = Address("tenant-a");
                string other = "tenant-b:" + actor.Split(':', 2)[1];
                await AppendAsync(actor, 12, assertions);
                await AppendAsync(other, 3, assertions);
                if (!containment) await ConfigureHistoryAsync(actor, "metadata-write");
                var before = await InventoryAsync(actor, 12);
                var isolated = await InventoryAsync(other, 3);
                await StopWritersAsync();
                string dump = Path.Combine(runtime.Root, "backup-" + Guid.NewGuid().ToString("N") + ".rdb");
                runtime.RegisterSensitivePath(dump);
                await runtime.RequireAsync("docker", ["exec", _redis, "redis-cli", "SAVE"]);
                await runtime.RequireAsync("docker", ["cp", _redis + ":/data/dump.rdb", dump]);
                string hash = QualificationRuntime.Hash(await File.ReadAllBytesAsync(dump));
                assertions.Check(new FileInfo(dump).Length > 0 && hash.Length == 64, "actual-provider-backup-hash");
                await FreshDatabaseAsync(dump);
                File.Delete(dump);
                assertions.Check(!File.Exists(dump), "dump-destroyed-after-restore");
                await StartWriterAsync(Build("Host", old), Build("Domain", old));
                assertions.Check(Equal(before, await InventoryAsync(actor, 12)), "fresh-database-restored-inventory");
                assertions.Check(Equal(isolated, await InventoryAsync(other, 3)), "fresh-database-restored-second-tenant");
                JsonElement result = await ExecuteAsync(actor, !containment);
                assertions.Check(IsOk(result) && result.GetProperty("count").GetInt32() == 12, "old-host-rehydration");
                bool floor = containment || IsOk(result) && result.GetProperty("metadata").TryGetProperty("retainedFloor", out JsonElement retained) && retained.GetInt64() == 5;
                if (!containment && IsOk(result))
                {
                    assertions.Check(result.GetProperty("sequence").GetInt64() == 13, "restored-database-actual-append");
                    await StartWriterAsync(Build("Host", old), Build("Domain", old));
                    JsonElement restart = await ExecuteAsync(actor, false);
                    assertions.Check(IsOk(restart) && restart.GetProperty("count").GetInt32() == 13, "restored-append-restarts");
                }
                return !containment && floor && assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
        }
    }

    private async Task StartWriterAsync(ConsumerBuild host, ConsumerBuild domain)
    {
        await StopWritersAsync();
        _hostPort = Port();
        _sidecarPort = Port();
        int domainPort = Port();
        _domainPort = domainPort;
        int grpc = Port();
        string components = Path.Combine(runtime.Root, "components");
        Directory.CreateDirectory(components);
        await File.WriteAllTextAsync(Path.Combine(components, "state.yaml"), $"apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n  - name: redisHost\n    value: 127.0.0.1:{_redisPort}\n  - name: actorStateStore\n    value: \"true\"\n  - name: keyPrefix\n    value: appid\n");
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:" + _hostPort,
            ["P1R_DOMAIN_URL"] = "http://127.0.0.1:" + domainPort,
            ["DAPR_HTTP_PORT"] = _sidecarPort.ToString(), ["DAPR_GRPC_PORT"] = grpc.ToString(),
            ["ASPNETCORE_ENVIRONMENT"] = "Testing", ["APP_API_TOKEN"] = "p1r-synthetic-app-token",
            ["P1R_QUERY_SIGNING_KEY_PATH"] = _querySigningKeyPath,
        };
        _writers.Add(runtime.Start(new("dotnet", [domain.Assembly, "--urls", "http://127.0.0.1:" + domainPort], Path.GetDirectoryName(domain.Project)!, TimeSpan.FromMinutes(1),
            new Dictionary<string, string> { ["P1R_QUERY_VERIFICATION_KEY"] = _queryVerificationKey,
                ["DAPR_HTTP_PORT"] = _sidecarPort.ToString(), ["DAPR_GRPC_PORT"] = grpc.ToString() })));
        _writers.Add(runtime.Start(new("dotnet", [host.Assembly], Path.GetDirectoryName(host.Project)!, TimeSpan.FromMinutes(1), environment)));
        await WaitHttpAsync("http://127.0.0.1:" + domainPort + "/identity");
        await WaitHttpAsync("http://127.0.0.1:" + _hostPort + "/identity");
        _writers.Add(runtime.Start(new(Path.Combine(_binaries, "daprd"), ["--app-id", _appId, "--app-port", _hostPort.ToString(),
            "--app-channel-address", "127.0.0.1", "--dapr-http-port", _sidecarPort.ToString(), "--dapr-grpc-port", grpc.ToString(),
            "--dapr-internal-grpc-port", Port().ToString(), "--metrics-port", Port().ToString(), "--profile-port", Port().ToString(),
            "--placement-host-address", "127.0.0.1:" + _placementPort, "--scheduler-host-address", "127.0.0.1:" + _schedulerPort, "--resources-path", components,
            "--log-level", "warn"], runtime.Root, TimeSpan.FromMinutes(1), new Dictionary<string, string> { ["APP_API_TOKEN"] = "p1r-synthetic-app-token" })));
        await WaitHttpAsync("http://127.0.0.1:" + _sidecarPort + "/v1.0/healthz");
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    }

    private async Task FreshDatabaseAsync(string? dump = null)
    {
        _redis = runtime.ReserveContainer("redis-" + ++_databaseNumber);
        _redisPort = Port();
        string volume = runtime.ReserveVolume("redis-data-" + _databaseNumber);
        await runtime.RequireAsync("docker", ["volume", "create", "--label", "p1r.owner=" + runtime.Id, volume]);
        await runtime.RequireAsync("docker", ["create", "--name", _redis, "--label", "p1r.owner=" + runtime.Id,
            "--mount", "type=volume,source=" + volume + ",destination=/data",
            "-p", "127.0.0.1:" + _redisPort + ":6379", RedisImage, "redis-server", "--appendonly", "no"]);
        if (dump is not null) await runtime.RequireAsync("docker", ["cp", dump, _redis + ":/data/dump.rdb"]);
        await runtime.RequireAsync("docker", ["start", _redis]);
        DateTimeOffset until = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            CommandObservation ping = await runtime.RunAsync(new("docker", ["exec", _redis, "redis-cli", "PING"], runtime.Root, TimeSpan.FromSeconds(5)));
            if (ping.ExitCode == 0 && ping.StandardOutput.Trim() == "PONG") break;
            if (DateTimeOffset.UtcNow >= until) throw new TimeoutException("Owned Redis did not become ready.");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
    }

    private async Task AppendAsync(string actor, int count, MeasuredAssertions assertions)
    {
        for (int index = 1; index <= count; index++)
        {
            JsonElement result = await ExecuteAsync(actor, true);
            assertions.Check(IsOk(result) && result.GetProperty("sequence").GetInt64() == index, "writer-committed-" + index);
            if (!IsOk(result)) throw new InvalidOperationException("Actual actor writer refused: " + result.GetRawText());
        }
    }

    private async Task ConfigureHistoryAsync(string actor, string scenario)
    {
        var operations = new List<object>();
        if (scenario is not ("missing-event" or "missing-event-tail" or "retained-uncovered-absent"))
        {
            var snapshot = new { sequenceNumber = scenario == "retained-uncovered" ? 2 : 9, state = new { count = scenario == "retained-uncovered" ? 2 : 9 },
                createdAt = DateTimeOffset.UnixEpoch, domain = "counter", aggregateId = actor.Split(':')[2], tenantId = actor.Split(':')[0] };
            operations.Add(new { operation = "upsert", key = actor + ":snapshot", value = (object)snapshot });
        }
        if (scenario != "snapshot-tail" && scenario is not ("missing-event" or "missing-event-tail"))
        {
            JsonNode metadata = JsonNode.Parse((await ReadAsync(actor, actor + ":metadata"))!)!;
            metadata["retainedFloor"] = 5;
            if (metadata["sourceProof"] is JsonNode proof) proof["retainedFloor"] = 5;
            operations.Add(new { operation = "upsert", key = actor + ":metadata", value = (object)metadata });
            for (int sequence = 1; sequence < 5; sequence++) operations.Add(new { operation = "delete", key = actor + ":events:" + sequence });
        }
        if (scenario == "missing-event") operations.Add(new { operation = "delete", key = actor + ":events:11" });
        if (scenario == "missing-event-tail") operations.Add(new { operation = "delete", key = actor + ":events:12" });
        await FixtureAsync(actor, operations);
    }

    private async Task InvalidHistoryAsync(string actor, string invalid)
    {
        string key = actor + (invalid.StartsWith("floor-", StringComparison.Ordinal) ? ":metadata" : ":events:2");
        JsonNode value = JsonNode.Parse((await ReadAsync(actor, key))!)!;
        switch (invalid)
        {
            case "floor-0": value["retainedFloor"] = 0; break;
            case "floor-14": value["retainedFloor"] = 14; break;
            case "unreadable": value["payload"] = Convert.ToBase64String("invalid-json"u8.ToArray()); break;
            case "protected-mismatch": value["serializationFormat"] = "protected+json"; break;
            case "unknown-type": value["eventTypeName"] = "Unknown.Event"; break;
            case "version-987": value["metadataVersion"] = 987; break;
        }
        await FixtureAsync(actor, new[] { new { operation = "upsert", key, value } });
    }

    private async Task<JsonElement> ExecuteAsync(string actor, bool append)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("http://127.0.0.1:" + _hostPort + "/execute/" + Uri.EscapeDataString(actor), new { append }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private async Task FixtureAsync(string actor, object operations)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("http://127.0.0.1:" + _hostPort + "/fixture/" + Uri.EscapeDataString(actor), operations, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string?> ReadAsync(string actor, string key)
    {
        using HttpResponseMessage response = await _http.GetAsync("http://127.0.0.1:" + _hostPort + "/read/" + Uri.EscapeDataString(actor) + "/" + Uri.EscapeDataString(key), TestContext.Current.CancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<Dictionary<string, string?>> InventoryAsync(string actor, int head)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (string key in new[] { actor + ":metadata", actor + ":snapshot" }.Concat(Enumerable.Range(1, head).Select(sequence => actor + ":events:" + sequence)))
        {
            string? raw = await ReadAsync(actor, key);
            result.Add(key, raw is null ? null : QualificationRuntime.Hash(Encoding.UTF8.GetBytes(raw)));
        }
        return result;
    }

    private async Task WaitHttpAsync(string address)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try
            {
                using HttpResponseMessage response = await _http.GetAsync(address, deadline.Token);
                if (response.IsSuccessStatusCode) return;
                if ((int)response.StatusCode >= 500) throw new InvalidOperationException("Host readiness returned " + response.StatusCode + ": " + await response.Content.ReadAsStringAsync(deadline.Token));
            }
            catch (HttpRequestException) { }
            await Task.Delay(200, deadline.Token);
        }
    }

    private async Task StopWritersAsync()
    {
        foreach (OwnedProcess process in _writers.AsEnumerable().Reverse()) await QualificationRuntime.StopAsync(process);
        _writers.Clear();
    }

    private ConsumerBuild Build(string role, string version, bool source = false)
        => builds.Single(build => build.Role == role && build.Version == (version == "tagged-source" ? ConsumerMaterializer.SelectedVersion : version)
            && (build.Source is not null) == (source || version == "tagged-source"));

    private static bool IsOk(JsonElement result) => result.TryGetProperty("ok", out JsonElement ok) && ok.GetBoolean();

    private static bool Equal(Dictionary<string, string?> left, Dictionary<string, string?> right)
        => left.Count == right.Count && left.All(pair => right.GetValueOrDefault(pair.Key) == pair.Value);

    private static string Address(string tenant) => tenant + ":counter:" + Guid.NewGuid().ToString("N");

    private static int Port()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopWritersAsync();
        _http.Dispose();
    }
}
