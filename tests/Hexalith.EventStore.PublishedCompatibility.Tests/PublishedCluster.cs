using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Runs versioned actors in a private Dapr 1.18.4/Redis topology, including provider-level fresh-database restores.</summary>
/// <param name="runtime">The invocation owner.</param>
/// <param name="builds">The independently materialized consumers.</param>
internal sealed class PublishedCluster(QualificationRuntime runtime, IReadOnlyList<ConsumerBuild> builds) : IAsyncDisposable
{
    /// <summary>Binds the original Redis image by its immutable digest.</summary>
    internal const string RedisImage = "redis@sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf";
    private readonly HttpClient _http = new(new QualificationHttpTransportObserver(runtime)) { Timeout = TimeSpan.FromSeconds(45) };
    private readonly List<OwnedProcess> _writers = [];
    private readonly HashSet<int> _allocatedPorts = [];
    private string _redis = string.Empty;
    private string _redisVolume = string.Empty;
    private string _binaries = string.Empty;
    private int _redisPort;
    private int _placementPort;
    private int _schedulerPort;
    private int _domainPort;
    private int _hostPort;
    private int _sidecarPort;
    private string _appId = string.Empty;
    private int _databaseNumber;
    private string _securityInventoryPath = string.Empty;
    private int _securityHostPid;
    private string _profileId = string.Empty;
    private string _querySigningKeyPath = string.Empty;
    private string _queryVerificationKey = string.Empty;
    private CancellationToken _operationCancellation;
    private CancellationToken OperationCancellation => _operationCancellation.CanBeCanceled ? _operationCancellation : TestContext.Current.CancellationToken;

    /// <summary>Downloads private pinned binaries without touching the installed Dapr runtime.</summary>
    internal async Task InitializeAsync()
    {
        await runtime.CaptureSharedContainersAsync();
        using (RSA queryKey = RSA.Create(2048))
        {
            _querySigningKeyPath = Path.Combine(runtime.Root, "query-signing-key.pem");
            runtime.RegisterSensitivePath(_querySigningKeyPath);
            var keyOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) keyOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var keyFile = new FileStream(_querySigningKeyPath, keyOptions))
            await using (var writer = new StreamWriter(keyFile))
                await writer.WriteAsync(queryKey.ExportRSAPrivateKeyPem());
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
        await InvalidEvidenceCasesAsync(observations);
        await FloorBoundaryCasesAsync(observations);
        await RestoreCasesAsync(observations, selected);
        await CapabilityCasesAsync(observations);
        await ExecuteCrossClientCasesAsync(observations);
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

    /// <summary>Executes actual independently versioned actor clients against opposite-version live hosts.</summary>
    internal async Task ExecuteCrossClientCasesAsync(List<ScenarioObservation> observations)
    {
        var directions = new List<(ConsumerBuild Client, ConsumerBuild Host)>();
        foreach (string old in new[] { "3.70.1", "3.110.0" })
        {
            directions.Add((Build("Probe", old), Build("Host", ConsumerMaterializer.SelectedVersion)));
            directions.Add((Build("Probe", ConsumerMaterializer.SelectedVersion), Build("Host", old)));
        }
        directions.Add((Build("Probe", ConsumerMaterializer.SelectedVersion), Build("Host", ConsumerMaterializer.SelectedVersion, source: true)));
        directions.Add((Build("Probe", ConsumerMaterializer.SelectedVersion, source: true), Build("Host", ConsumerMaterializer.SelectedVersion)));
        foreach ((ConsumerBuild client, ConsumerBuild host) in directions)
            await PublishedScenarioTests.ObserveAsync(observations, "mixed-api", "client-" + client.Version + "-" + client.Configuration + "-to-host-" + host.Version + "-" + host.Configuration, async assertions =>
            {
                ConsumerBuild domain = builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source);
                await StartWriterAsync(host, domain);
                string actor = Address("tenant-a");
                async Task<JsonElement> InvokeAsync(string action, JsonElement prepared)
                {
                    JsonElement result = await PublishedScenarioTests.ProbeAsync(runtime, client,
                        new { operation = "cross-client", sidecar = "http://127.0.0.1:" + _sidecarPort, actor, action, prepared });
                    PublishedScenarioTests.ValidateIdentity(client, result, assertions);
                    assertions.Record("cross-client-" + action, result.GetRawText());
                    return result;
                }
                JsonElement common = await InvokeAsync("common", JsonSerializer.SerializeToElement(new { supported = false }));
                assertions.Check(IsOk(common) && common.GetProperty("outcome").GetProperty("accepted").GetBoolean()
                    && common.GetProperty("outcome").GetProperty("before").GetInt64() == 0
                    && common.GetProperty("outcome").GetProperty("sequence").GetInt64() == 1, "opposite-version-client-real-common-actor-append");
                var baseline = await InventoryAsync(actor, 4);
                assertions.Check(baseline[actor + ":events:1"] is not null, "cross-client-common-event-committed");
                JsonElement prepared = await PostAsync("http://127.0.0.1:" + _hostPort + "/prepare-cross-client/" + actor.Split(':')[2], new { });
                if (client.Version == "3.70.1" || host.Version == "3.70.1")
                {
                    foreach (string action in new[] { "fenced-valid", "trusted-valid" })
                    {
                        JsonElement refused = await InvokeAsync(action, prepared);
                        bool missingClient = client.Version == "3.70.1" && !refused.GetProperty("supported").GetBoolean()
                            && refused.GetProperty("code").GetString() == "client-contract-lacks-protected-methods"
                            && refused.GetProperty("missingMethods").EnumerateArray().Select(value => value.GetString())
                                .Order().SequenceEqual(new[] { "ProcessFencedCommandAsync", "ProcessTrustedEffectAsync" });
                        string requestBody = action.StartsWith("fenced", StringComparison.Ordinal) ? "ProcessFencedCommandAsyncReqBody" : "ProcessTrustedEffectAsyncReqBody";
                        bool missingHost = refused.TryGetProperty("actualType", out JsonElement actual)
                            && actual.GetString() == typeof(System.Runtime.Serialization.SerializationException).FullName
                            && refused.GetProperty("message").GetString()!.Contains("data of the 'urn:actors:" + requestBody + "' data contract.", StringComparison.Ordinal)
                            && refused.GetProperty("message").GetString()!.Contains("The deserializer has no knowledge of any type that maps to this contract.", StringComparison.Ordinal);
                        if (missingHost) assertions.Record("actual-host-protected-contract-refusal", JsonSerializer.Serialize(new
                        {
                            code = "host-remoting-contract-unavailable", stage = "actor-remoting-request-deserialization", requestBody,
                            actualType = actual.GetString(), message = refused.GetProperty("message").GetString(),
                        }));
                        assertions.Check(!IsOk(refused) && (missingClient || missingHost), "opposite-version-actual-missing-protected-capability-" + action);
                        assertions.Check(Equal(baseline, await InventoryAsync(actor, 4)), "missing-protected-operation-preserves-committed-bytes-" + action);
                    }
                    await StartWriterAsync(host, domain);
                    JsonElement replay = await ExecuteAsync(actor, false);
                    assertions.Check(IsOk(replay) && replay.GetProperty("count").GetInt32() == 1, "opposite-version-common-effect-survives-host-restart");
                    return assertions.Failures.Count == 0 ? "incompatible" : "unverified";
                }
                foreach ((string action, string message) in new[]
                {
                    ("fenced-forged", "The idempotency execution fence is missing, stale, or invalid."),
                    ("fenced-valid", ""),
                    ("trusted-forged", "Trusted effect gateway proof is invalid."),
                    ("trusted-valid", ""),
                    ("trusted-replay", ""),
                })
                {
                    var before = await InventoryAsync(actor, 4);
                    JsonElement result = await InvokeAsync(action, prepared);
                    var after = await InventoryAsync(actor, 4);
                    if (action.EndsWith("forged", StringComparison.Ordinal))
                    {
                        assertions.Check(!IsOk(result) && result.GetProperty("actualType").GetString() == typeof(InvalidOperationException).FullName
                            && result.GetProperty("message").GetString() == message, "cross-client-exact-security-refusal-" + action);
                        assertions.Check(Equal(before, after), "cross-client-denial-preserves-committed-bytes-" + action);
                    }
                    else
                    {
                        int expectedSequence = action == "fenced-valid" ? 2 : 3;
                        assertions.Check(IsOk(result) && result.GetProperty("outcome").GetProperty("accepted").GetBoolean()
                            && result.GetProperty("outcome").GetProperty("sequence").GetInt32() == expectedSequence, "cross-client-protected-effect-committed-" + action);
                        assertions.Check(after[actor + ":events:1"] == baseline[actor + ":events:1"]
                            && after[actor + ":events:" + expectedSequence] is not null, "cross-client-preserves-prior-and-records-new-event-" + action);
                        if (action == "trusted-replay") assertions.Check(result.GetProperty("outcome").GetProperty("replayed").GetBoolean()
                            && Equal(before, after), "cross-client-effect-replay-is-byte-stable");
                    }
                }
                var committed = await InventoryAsync(actor, 4);
                await StartWriterAsync(host, domain);
                JsonElement restarted = await ExecuteAsync(actor, false);
                assertions.Check(IsOk(restarted) && restarted.GetProperty("count").GetInt32() == 3, "cross-client-protected-effects-survive-host-restart");
                assertions.Check(Equal(committed, await InventoryAsync(actor, 4)), "cross-client-protected-restart-preserves-bytes");
                return assertions.Failures.Count == 0 ? "compatible" : "unverified";
            });
    }

    private async Task InvalidEvidenceCasesAsync(List<ScenarioObservation> observations, bool selectedOnly = false)
    {
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host" && (!selectedOnly || build.Version == ConsumerMaterializer.SelectedVersion)))
        {
            ConsumerBuild domain = builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source);
            foreach (string invalid in new[] { "floor-0", "floor-14", "unreadable", "protected-mismatch", "unknown-type", "version-987" })
            foreach (string tenant in selectedOnly ? new[] { "tenant-a" } : new[] { "tenant-a", "tenant-b" })
            {
                await PublishedScenarioTests.ObserveAsync(observations, "invalid-evidence", host.Version + "-" + host.Configuration + "-" + invalid + "-" + tenant, async assertions =>
                {
                    await StartWriterAsync(host, domain);
                    string actor = Address(tenant);
                    await AppendAsync(actor, 2, assertions);
                    await InvalidHistoryAsync(actor, invalid);
                    var before = await InventoryAsync(actor, 3);
                    await StartWriterAsync(host, domain);
                    string readerAddress = "http://127.0.0.1:" + _hostPort + "/reader/" + Uri.EscapeDataString(actor);
                    assertions.Record("actual-reader-target", JsonSerializer.Serialize(new { actor, address = readerAddress, hostProcessId = _securityHostPid }));
                    JsonElement reader = await GetAsync(readerAddress);
                    assertions.Record("actual-versioned-reader-result", reader.GetRawText());
                    bool ownsFloor = host.Version == ConsumerMaterializer.SelectedVersion && invalid.StartsWith("floor-", StringComparison.Ordinal);
                    bool ownsVersion = host.Version == ConsumerMaterializer.SelectedVersion && invalid == "version-987";
                    if (ownsFloor || ownsVersion)
                    {
                        string expected = ownsFloor ? "SourceHeadChanged: actor metadata has an invalid retained floor."
                            : "RollbackReaderCapabilityHold: a typed reader cannot consume unsupported event metadata.";
                        assertions.Check(!IsOk(reader) && reader.TryGetProperty("error", out JsonElement readerError)
                            && readerError.GetString() == typeof(InvalidOperationException).FullName
                            && reader.TryGetProperty("message", out JsonElement readerMessage) && readerMessage.GetString() == expected,
                            "actual-reader-owned-metadata-refusal-type-and-code");
                    }
                    else
                        assertions.Check(IsOk(reader) && reader.GetProperty("sequence").GetInt64() == 2 && reader.GetProperty("count").GetInt32() == 2,
                            "raw-reader-does-not-claim-domain-payload-or-protection-decoding");
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
                    return assertions.Failures.Count == 0 && !IsOk(result) ? "compatible" : "unverified";
                });
            }
        }
    }

    /// <summary>Executes only affected malformed reader/writer operations in the selected package and exact source.</summary>
    internal async Task ExecuteSelectedMalformedBoundariesAsync(List<ScenarioObservation> observations)
    {
        await InvalidEvidenceCasesAsync(observations, selectedOnly: true);
        await FloorBoundaryCasesAsync(observations, malformedOnly: true);
    }

    /// <summary>Exercises the existing actual malformed-writer path with the independently selected consumers.</summary>
    internal Task ExecuteSelectedWriterBoundariesAsync(List<ScenarioObservation> observations) => FloorBoundaryCasesAsync(observations, malformedOnly: true);

    private async Task FloorBoundaryCasesAsync(List<ScenarioObservation> observations, bool malformedOnly = false)
    {
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host" && (!malformedOnly || build.Version == ConsumerMaterializer.SelectedVersion)))
        foreach (int floor in malformedOnly ? new[] { 0, 14 } : new[] { 0, 1, 5, 13, 14 })
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
                    assertions.Record("actual-writer-admission-result", writer.GetRawText());
                    assertions.Check(writer.GetProperty("refused").GetBoolean() && writer.GetProperty("error").GetString() == typeof(InvalidOperationException).FullName
                        && writer.GetProperty("message").GetString() == "SourceHeadChanged: actor metadata has an invalid retained floor."
                        && writer.GetProperty("code").GetString() == "SourceHeadChanged" && writer.GetProperty("stage").GetString() == "event-persister-metadata-floor-admission",
                        "actual-writer-malformed-floor-production-type-code-stage");
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
                return assertions.Failures.Count == 0 ? "compatible" : "unverified";
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
            await ExecuteSecurityBoundaryAsync(observations, host, domain);
            if (host.Version != ConsumerMaterializer.SelectedVersion) continue;
            await ExecuteQueryBoundaryAsync(observations, host, domain);
            await ExecuteManifestBoundaryAsync(observations, host, domain);
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
            await ExecuteNaturalReminderAsync(observations, host, domain);
        }
    }

    private async Task ExecuteNaturalReminderAsync(List<ScenarioObservation> observations, ConsumerBuild host, ConsumerBuild domain)
    {
            await PublishedScenarioTests.ObserveAsync(observations, "natural-reminder", host.Configuration, async assertions =>
            {
                await StartWriterAsync(host, domain);
                string aggregate = Guid.NewGuid().ToString("N");
                string actor = "tenant-a:counter:" + aggregate;
                TimeSpan preparationBudget = TimeSpan.FromSeconds(60);
                DateTimeOffset preparationStarted = DateTimeOffset.UtcNow;
                DateTimeOffset due = preparationStarted.Add(preparationBudget).AddSeconds(20);
                JsonElement restarted;
                Dictionary<string, string?> before;
                string name;
                using (var preparation = CancellationTokenSource.CreateLinkedTokenSource(OperationCancellation))
                {
                    preparation.CancelAfter(preparationBudget);
                    _operationCancellation = preparation.Token;
                    try
                    {
                        JsonElement registered = await PostAsync("http://127.0.0.1:" + _hostPort + "/reminder/" + aggregate, new { dueUtc = due });
                        assertions.Check(registered.GetProperty("accepted").GetBoolean() && registered.GetProperty("armed").GetInt32() == 1, "stream-derived-reminder-armed");
                        assertions.Check(registered.GetProperty("submitted").GetInt32() == 0, "submitted-zero-before-natural-delivery");
                        name = registered.GetProperty("reminderName").GetString()!;
                        before = await InventoryAsync(actor, 1);
                        await StartWriterAsync(host, domain);
                        restarted = await GetAsync("http://127.0.0.1:" + _hostPort + "/reminder/" + aggregate + "/" + name);
                        preparation.Token.ThrowIfCancellationRequested();
                        assertions.Check(DateTimeOffset.UtcNow < due, "restart-and-zero-observation-completed-within-pre-due-budget");
                        assertions.Check(restarted.GetProperty("submitted").GetInt32() == 0, "submitted-zero-after-restart-before-due");
                        assertions.Record("bounded-reminder-registration-restart-window", JsonSerializer.Serialize(new
                        {
                            preparationStarted, preparationBudgetSeconds = preparationBudget.TotalSeconds,
                            preparationFinished = DateTimeOffset.UtcNow, due, naturalDeliveryMarginSeconds = 20,
                        }));
                    }
                    finally { _operationCancellation = default; }
                }
                DateTimeOffset deadline = due.AddSeconds(40);
                JsonElement observed = restarted;
                while (DateTimeOffset.UtcNow < deadline && observed.GetProperty("submitted").GetInt32() == 0)
                {
                    await Task.Delay(500, OperationCancellation);
                    observed = await GetAsync("http://127.0.0.1:" + _hostPort + "/reminder/" + aggregate + "/" + name);
                }
                assertions.Check(observed.GetProperty("submitted").GetInt32() == 1, "natural-scheduler-delivery-persisted-submission");
                JsonElement result = await ExecuteAsync(actor, false);
                assertions.Check(IsOk(result) && result.GetProperty("sequence").GetInt64() == 2 && result.GetProperty("count").GetInt32() == 2, "natural-delivery-target-effect-persisted");
                assertions.Check((await InventoryAsync(actor, 2)).GetValueOrDefault(actor + ":events:1") == before[actor + ":events:1"], "reminder-preserves-source-event-bytes");
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
    }

    /// <summary>Executes the same actual capability/addition operations with the selected consumers for focused diagnostics.</summary>
    internal Task ExecuteSelectedCapabilitiesAsync(List<ScenarioObservation> observations) => CapabilityCasesAsync(observations);

    /// <summary>Executes only actual fenced/trusted security operations for each independently built host.</summary>
    internal async Task ExecuteSecurityBoundariesAsync(List<ScenarioObservation> observations)
    {
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host"))
            await ExecuteSecurityBoundaryAsync(observations, host,
                builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source));
    }

    private async Task ExecuteSecurityBoundaryAsync(List<ScenarioObservation> observations, ConsumerBuild host, ConsumerBuild domain)
    {
    await PublishedScenarioTests.ObserveAsync(observations, "mixed-api", host.Version + "-" + host.Configuration, async assertions =>
    {
        await StartWriterAsync(host, domain);
        string actor = Address("tenant-a");
        await AppendAsync(actor, 1, assertions);
        var before = await InventoryAsync(actor, 4);
        JsonElement result = await PostAsync("http://127.0.0.1:" + _hostPort + "/capabilities/" + actor.Split(':')[2], new { });
        bool supported = result.GetProperty("supported").GetBoolean();
        if (!supported)
        {
            assertions.Check(result.GetProperty("denial").GetString() == "incapable-published-endpoint", "typed-old-endpoint-refusal");
            assertions.Check(Equal(before, await InventoryAsync(actor, 4)), "unsupported-route-inventory-unchanged");
            return "incompatible";
        }
        assertions.Record("capability-security-observations", result.GetRawText());
        if (result.TryGetProperty("error", out _))
        {
            assertions.Check(false, "capability-operation-completed-without-unrelated-fault");
            return "unverified";
        }
        string capture = await runtime.RequireAsync("python3", ["-c", HistoricalSecurityCapture.ReadScript, _securityInventoryPath, runtime.Id, _securityHostPid.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        assertions.Record("capability-security-capture", JsonSerializer.Serialize(new { owner = runtime.Id, path = _securityInventoryPath, pid = _securityHostPid, profileId = _profileId, snapshots = JsonSerializer.Deserialize<JsonElement>(capture) }));
        foreach ((string field, string code, string stage, string message) in new[]
        {
            ("stale", "stale-execution-authority", "execution-authority", "The idempotency execution authority is no longer current."),
            ("forged", "invalid-execution-fence", "execution-fence-proof", "The idempotency execution fence is missing, stale, or invalid."),
            ("completedStale", "completed-execution-authority", "execution-authority", "The idempotency execution authority is no longer current."),
            ("unauthorized", "unauthorized-effect-delegation", "trusted-effect-delegation", "Synthetic trusted-effect delegation refused."),
            ("forgedEffect", "invalid-effect-gateway-proof", "trusted-effect-gateway-proof", "Trusted effect gateway proof is invalid."),
        })
        {
            JsonElement refusal = result.GetProperty(field);
            assertions.Check(refusal.GetProperty("expected").GetBoolean() && refusal.GetProperty("actualType").GetString() == typeof(InvalidOperationException).FullName
                && refusal.GetProperty("message").GetString() == message && refusal.GetProperty("code").GetString() == code
                && refusal.GetProperty("stage").GetString() == stage, "actual-security-refusal-type-code-stage-" + field);
            var prior = refusal.GetProperty("before").Deserialize<Dictionary<string, string?>>()!;
            var after = refusal.GetProperty("after").Deserialize<Dictionary<string, string?>>()!;
            assertions.Check(prior.Keys.Order().SequenceEqual(before.Keys.Order()) && prior[actor + ":metadata"] is not null
                && prior[actor + ":events:1"] is not null, "complete-committed-inventory-before-" + field);
            assertions.Check(Equal(prior, after), "denied-operation-preserves-committed-bytes-" + field);
        }
        assertions.Check(result.GetProperty("accepted").GetBoolean() && result.GetProperty("replayed").GetBoolean(), "actual-fenced-command-and-idempotent-effect-accepted");
        assertions.Check(result.GetProperty("baseline").GetInt64() == 1 && result.GetProperty("sequence").GetInt64() == 3, "baseline-plus-two-actual-persisted-effects");
        var committed = await InventoryAsync(actor, 4);
        await StartWriterAsync(host, domain);
        JsonElement replay = await ExecuteAsync(actor, false);
        assertions.Check(IsOk(replay) && replay.GetProperty("count").GetInt32() == 3, "protected-effects-survive-fresh-processes");
        assertions.Check(Equal(committed, await InventoryAsync(actor, 4)), "protected-effects-restart-preserves-bytes");
        return assertions.Failures.Count == 0 ? "compatible" : "unverified";
    });
    }

    /// <summary>Executes the actual required manifest hop independently for both selected consumer origins.</summary>
    internal async Task ExecuteSelectedManifestAsync(List<ScenarioObservation> observations)
    {
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host" && build.Version == ConsumerMaterializer.SelectedVersion))
            await ExecuteManifestBoundaryAsync(observations, host,
                builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source));
    }

    private async Task ExecuteManifestBoundaryAsync(List<ScenarioObservation> observations, ConsumerBuild host, ConsumerBuild domain)
    {
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
                { messageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV", aggregateId = aggregate, aggregateType = "r", tenantId = "tenant-a", domain = "d",
                    sequenceNumber = 1, globalPosition = 987, timestamp = DateTimeOffset.UnixEpoch, correlationId = "corr", causationId = "cause",
                    userId = "fixture-user", domainServiceVersion = "v1", eventTypeName = "Legacy.Event", metadataVersion = 1,
                    serializationFormat = "json", payload = Convert.ToBase64String("{}"u8.ToArray()), extensions = (object?)null } },
        });
        var before = await InventoryAsync(actor, 2);
        await StartWriterAsync(host, domain);
        JsonElement result = await PostAsync("http://127.0.0.1:" + _hostPort + "/execute/" + Uri.EscapeDataString(actor), new { append = true });
        assertions.Record("production-manifest-command-result", result.GetRawText());
        assertions.Check(!IsOk(result), "production-required-logical-hop-refused-without-bound-callable");
        assertions.Check(result.TryGetProperty("error", out JsonElement error) && !string.IsNullOrWhiteSpace(error.GetString()), "typed-production-evolution-refusal");
        JsonElement observation = await GetAsync("http://127.0.0.1:" + _hostPort + "/manifest-refusal/" + Uri.EscapeDataString(actor));
        bool observed = observation.GetProperty("observed").GetBoolean();
        assertions.Check(observed, "production-missing-callable-cause-was-observed");
        JsonElement cause = observation.GetProperty("cause");
        bool intendedHop = observed && cause.GetProperty("code").GetString() == "missing-allow-listed-callable"
            && cause.GetProperty("stage").GetString() == "production-logical-event-upcast-binding"
            && cause.GetProperty("actor").GetString() == actor
            && cause.GetProperty("declaringType").GetString()!.StartsWith("Hexalith.EventStore.Client.Events.EventUpcastChainExecutor", StringComparison.Ordinal);
        assertions.Check(intendedHop, "actual-production-hop-missing-callable-cause-and-stage");
        assertions.Record("production-manifest-refusal", observation.GetRawText());
        assertions.Check(Equal(before, await InventoryAsync(actor, 2)), "production-manifest-refusal-preserves-original-event-bytes");
        JsonElement stats = await GetAsync("http://127.0.0.1:" + _domainPort + "/stats");
        assertions.Check(stats.GetProperty("applyCalls").GetInt32() == 0 && stats.GetProperty("handleCalls").GetInt32() == 0, "manifest-refusal-before-domain-apply-handle");
        return intendedHop && assertions.Failures.Count == 0 ? "incompatible" : "unverified";
    });
    }

    private async Task ExecuteQueryBoundaryAsync(List<ScenarioObservation> observations, ConsumerBuild host, ConsumerBuild domain)
    {
        await ExecuteDeclaredHandlerBoundaryAsync(observations, host, domain);
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

    /// <summary>Separately measures production declared-handler transport and natural Reminder delivery for both immutable origins.</summary>
    internal async Task ExecuteReviewedQueryAndReminderBoundariesAsync(List<ScenarioObservation> observations)
    {
        foreach (ConsumerBuild host in builds.Where(build => build.Role == "Host" && build.Version == ConsumerMaterializer.SelectedVersion))
        {
            ConsumerBuild domain = builds.Single(build => build.Role == "Domain" && build.Version == host.Version && build.Source == host.Source);
            await ExecuteQueryBoundaryAsync(observations, host, domain);
            await ExecuteNaturalReminderAsync(observations, host, domain);
        }
    }

    private async Task ExecuteDeclaredHandlerBoundaryAsync(List<ScenarioObservation> observations, ConsumerBuild host, ConsumerBuild domain)
    {
        await PublishedScenarioTests.ObserveAsync(observations, "query-wire", host.Configuration + "-production-declared-handler-discovery-transport", async assertions =>
        {
            await StartWriterAsync(host, domain);
            string aggregate = Guid.NewGuid().ToString("N");
            JsonElement declaration = await PostAsync("http://127.0.0.1:" + _hostPort + "/declare-query-handlers", new { });
            assertions.Record("production-handler-discovery", declaration.GetRawText());
            assertions.Check(declaration.GetProperty("declared").EnumerateArray().Select(item => item.GetString()).SequenceEqual(new[] { "get-counter" })
                && declaration.GetProperty("persisted").GetRawText() == declaration.GetProperty("declared").GetRawText(), "actual-domain-handler-declaration-persisted-through-dapr");
            assertions.Check(declaration.GetProperty("supported").GetBoolean() && !declaration.GetProperty("unknown").GetBoolean()
                && declaration.GetProperty("registry").GetString() == "Hexalith.EventStore.Queries.DaprDomainQueryHandlerRegistry", "production-registry-discovers-only-declared-query");
            await PostAsync("http://127.0.0.1:" + _hostPort + "/project-model/" + aggregate, new { position = 987, sequence = 12 });
            foreach (string mode in new[] { "valid", "missing", "forged", "foreign-tenant", "foreign-actor" })
            {
                JsonElement before = await GetAsync("http://127.0.0.1:" + _domainPort + "/stats");
                JsonElement response = await PostAsync("http://127.0.0.1:" + _hostPort + "/query-route/" + aggregate,
                    new { serializer = "json", route = "declared-handler", mode });
                assertions.Record("production-handler-" + mode, response.GetRawText());
                assertions.Check(response.GetProperty("router").GetString() == "Hexalith.EventStore.Queries.HandlerAwareQueryRouter"
                    && response.GetProperty("invoker").GetString() == "Hexalith.EventStore.Queries.DaprDomainQueryInvoker", "actual-production-handler-router-and-invoker-" + mode);
                JsonElement after = await GetAsync("http://127.0.0.1:" + _domainPort + "/stats");
                assertions.Check(after.GetProperty("queryRequests").GetInt32() == before.GetProperty("queryRequests").GetInt32() + 1,
                    "live-domain-http-transport-received-exact-query-" + mode);
                if (mode == "valid")
                {
                    assertions.Check(response.GetProperty("success").GetBoolean(), "declared-handler-production-route-succeeds");
                    if (response.GetProperty("success").GetBoolean())
                    {
                        JsonElement payload = response.GetProperty("payload");
                        foreach ((string field, string expected) in new[] { ("originalActorId", "actor"), ("authenticatedWorkloadId", "workload"),
                            ("delegationId", "delegation"), ("tenantId", "tenant-a"), ("correlationId", "corr") })
                            assertions.Check(payload.GetProperty(field).GetString() == expected, "declared-handler-preserves-" + field);
                        assertions.Check(payload.GetProperty("isDelegated").GetBoolean() && payload.GetProperty("scopes")[0].GetString() == "counter.read"
                            && payload.GetProperty("audience")[0].GetString() == "eventstore", "declared-handler-preserves-dual-principal-authority");
                        assertions.Check(!string.IsNullOrWhiteSpace(payload.GetProperty("identityAdmissionProof").GetString()), "nonnull-proof-crosses-production-router-invoker-and-transport");
                        assertions.Check(payload.GetProperty("value").GetProperty("watermark").GetInt64() == 987, "declared-handler-reads-exact-durable-watermark");
                    }
                    assertions.Check(after.GetProperty("queryReads").GetInt32() == before.GetProperty("queryReads").GetInt32() + 1, "valid-declared-handler-reads-once-after-proof");
                }
                else
                {
                    assertions.Check(!response.GetProperty("success").GetBoolean() && response.GetProperty("errorMessage").GetString() == "protected-admission-refused",
                        "exact-declared-handler-admission-refusal-" + mode);
                    assertions.Check(after.GetProperty("queryReads").GetInt32() == before.GetProperty("queryReads").GetInt32(), "invalid-proof-before-declared-handler-read-" + mode);
                }
            }
            return assertions.Failures.Count == 0 ? "compatible" : "unverified";
        });
    }

    private string? _lastPostResponseHash;

    private async Task<JsonElement> PostAsync(string address, object request)
    {
        DateTimeOffset began = DateTimeOffset.UtcNow;
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var message = new HttpRequestMessage(HttpMethod.Post, address) { Content = new ByteArrayContent(input) };
        message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var response = await _http.SendAsync(message, OperationCancellation);
        byte[] output = await response.Content.ReadAsByteArrayAsync(OperationCancellation);
        string body = Encoding.UTF8.GetString(output);
        _lastPostResponseHash = QualificationRuntime.Hash(output);
        PublishedScenarioTests.ActiveAssertions?.Record("http-post", JsonSerializer.Serialize(new { address, began, ended = DateTimeOffset.UtcNow,
            status = (int)response.StatusCode, requestHash = QualificationRuntime.Hash(input), responseHash = QualificationRuntime.Hash(output),
            request = HistoricalHttpOperationCapture.Request(request), response = HistoricalHttpOperationCapture.Response(body) }));
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }

    private async Task<JsonElement> GetAsync(string address)
    {
        DateTimeOffset began = DateTimeOffset.UtcNow;
        using var response = await _http.GetAsync(address, OperationCancellation);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string value = json.RootElement.GetRawText();
        PublishedScenarioTests.ActiveAssertions?.Record("http-get:" + address, value);
        // Retain bounded transport coordinates and a semantic-body hash without copying any
        // additional authorization material out of the existing response observation.
        PublishedScenarioTests.ActiveAssertions?.Record("http-get-observation", JsonSerializer.Serialize(new { address, began,
            ended = DateTimeOffset.UtcNow, status = (int)response.StatusCode, bodyHash = QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value)) }));
        return json.RootElement.Clone();
    }

    private async Task RestoreCasesAsync(List<ScenarioObservation> observations, ConsumerBuild selected)
    {
        foreach (string target in new[] { "3.70.1", "3.110.0", ConsumerMaterializer.SelectedVersion, "tagged-source" })
        foreach (string scenario in new[] { "post-upgrade-restore", "pre-upgrade-restore" })
        {
            if (scenario == "pre-upgrade-restore" && target is not ("3.70.1" or "3.110.0")) continue;
            await PublishedScenarioTests.ObserveAsync(observations, scenario, target, async assertions =>
            {
                bool containment = scenario == "pre-upgrade-restore";
                ConsumerBuild targetHost = Build("Host", target);
                ConsumerBuild targetDomain = Build("Domain", target);
                ConsumerBuild writer = containment ? targetHost : selected;
                await StartWriterAsync(writer, Build("Domain", writer.Version, source: writer.Source is not null));
                string actor = Address("tenant-a");
                string other = "tenant-b:" + actor.Split(':', 2)[1];
                await AppendAsync(actor, 12, assertions);
                await AppendAsync(other, 3, assertions);
                if (!containment) await ConfigureHistoryAsync(actor, "metadata-write");
                if (!containment)
                {
                    using JsonDocument prepared = JsonDocument.Parse((await ReadAsync(actor, actor + ":metadata"))!);
                    assertions.Check(prepared.RootElement.GetProperty("retainedFloor").GetInt64() == 5
                        && prepared.RootElement.GetProperty("sourceProof").GetProperty("retainedFloor").GetInt64() == 5
                        && prepared.RootElement.GetProperty("sourceProof").GetProperty("headSequence").GetInt64() == 12,
                        "provider-backup-has-coherent-floor-five-head-twelve-proof");
                }
                var before = await InventoryAsync(actor, 12);
                var isolated = await InventoryAsync(other, 3);
                await StopWritersAsync();
                string dump = Path.Combine(runtime.Root, "backup-" + Guid.NewGuid().ToString("N") + ".rdb");
                runtime.RegisterSensitivePath(dump);
                await runtime.RequireRedisSaveAsync(_redis);
                await runtime.RequireAsync("docker", ["cp", _redis + ":/data/dump.rdb", dump]);
                string hash = QualificationRuntime.Hash(await File.ReadAllBytesAsync(dump));
                await runtime.ObserveProviderDumpAsync(dump, "provider-backup-copied", OperationCancellation);
                assertions.Record("provider-backup", JsonSerializer.Serialize(new { sourceContainer = _redis, hash, bytes = new FileInfo(dump).Length, path = dump }));
                assertions.Check(new FileInfo(dump).Length > 0 && hash.Length == 64, "actual-provider-backup-hash");
                if (containment)
                {
                    await StartWriterAsync(selected, Build("Domain", selected.Version));
                    JsonElement later = await ExecuteAsync(actor, true);
                    assertions.Check(IsOk(later) && later.GetProperty("sequence").GetInt64() == 13, "selected-writer-committed-after-pre-upgrade-backup");
                    assertions.Check(await ReadAsync(actor, actor + ":events:13") is not null, "later-selected-event-existed-before-containment-restore");
                    await StopWritersAsync();
                }
                await FreshDatabaseAsync(dump);
                File.Delete(dump);
                await runtime.ObserveProviderDumpAsync(dump, "provider-backup-destroyed", OperationCancellation);
                assertions.Check(!File.Exists(dump), "dump-destroyed-after-restore");
                await StartWriterAsync(targetHost, targetDomain);
                assertions.Check(Equal(before, await InventoryAsync(actor, 12)), "fresh-database-restored-inventory");
                assertions.Check(Equal(isolated, await InventoryAsync(other, 3)), "fresh-database-restored-second-tenant");
                if (containment) assertions.Check(await ReadAsync(actor, actor + ":events:13") is null, "containment-restores-earlier-point-and-cannot-claim-rpo-zero");
                JsonElement result = await ExecuteAsync(actor, !containment);
                assertions.Check(IsOk(result) && result.GetProperty("count").GetInt32() == 12, "restored-target-host-rehydration");
                bool floor = containment || IsOk(result) && result.GetProperty("metadata").TryGetProperty("retainedFloor", out JsonElement retained) && retained.GetInt64() == 5;
                if (!containment && IsOk(result))
                {
                    assertions.Check(result.GetProperty("sequence").GetInt64() == 13, "restored-database-actual-append");
                    assertions.Check(floor, "restored-writer-preserves-floor-five");
                    var appended = await InventoryAsync(actor, 13);
                    foreach ((string key, string? value) in before.Where(pair => !pair.Key.EndsWith(":metadata", StringComparison.Ordinal)))
                        assertions.Check(appended.GetValueOrDefault(key) == value, "restore-append-preserves-prior-event-or-snapshot-" + key);
                    assertions.Check(Equal(isolated, await InventoryAsync(other, 3)), "restore-append-preserves-second-tenant");
                    assertions.Check(Enumerable.Range(1, 4).All(sequence => appended[actor + ":events:" + sequence] is null)
                        && Enumerable.Range(5, 9).All(sequence => appended[actor + ":events:" + sequence] is not null)
                        && await ReadAsync(actor, actor + ":events:14") is null, "restore-append-exact-retained-event-set-five-through-thirteen");
                    string eventThirteenHash = appended[actor + ":events:13"]!;
                    assertions.Check(eventThirteenHash.Length == 64, "restore-new-event-thirteen-hash-observed");
                    using JsonDocument eventThirteen = JsonDocument.Parse((await ReadAsync(actor, actor + ":events:13"))!);
                    assertions.Check(result.GetProperty("metadata").TryGetProperty("sourceProof", out JsonElement proof)
                        && proof.GetProperty("headSequence").GetInt64() == 13 && proof.GetProperty("retainedFloor").GetInt64() == 5
                        && proof.GetProperty("headMessageId").GetString() == eventThirteen.RootElement.GetProperty("messageId").GetString(),
                        "restore-append-preserves-coherent-source-proof-coordinates");
                    await StartWriterAsync(targetHost, targetDomain);
                    JsonElement restart = await ExecuteAsync(actor, false);
                    assertions.Check(IsOk(restart) && restart.GetProperty("count").GetInt32() == 13
                        && restart.GetProperty("sequence").GetInt64() == 13, "restored-append-restarts-with-folded-count-thirteen");
                    assertions.Check(IsOk(restart) && restart.GetProperty("metadata").TryGetProperty("retainedFloor", out JsonElement restartedFloor)
                        && restartedFloor.GetInt64() == 5, "restored-floor-five-survives-fresh-restart");
                    var restarted = await InventoryAsync(actor, 13);
                    assertions.Check(Equal(appended, restarted), "restore-restart-preserves-entire-committed-inventory-and-snapshot");
                    assertions.Check(restarted[actor + ":events:13"] == eventThirteenHash, "restore-restart-preserves-new-event-thirteen-hash");
                    assertions.Check(Equal(isolated, await InventoryAsync(other, 3)), "restore-restart-preserves-second-tenant");
                }
                return !containment && floor && assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
        }
    }

    /// <summary>Runs the actual provider restores independently for focused recovery verification.</summary>
    internal Task ExecuteProviderRestoresAsync(List<ScenarioObservation> observations)
        => RestoreCasesAsync(observations, Build("Host", ConsumerMaterializer.SelectedVersion));

    private async Task StartWriterAsync(ConsumerBuild host, ConsumerBuild domain)
    {
        OperationCancellation.ThrowIfCancellationRequested();
        await StopWritersAsync();
        OperationCancellation.ThrowIfCancellationRequested();
        _hostPort = Port();
        _sidecarPort = Port();
        int domainPort = Port();
        _domainPort = domainPort;
        int grpc = Port();
        string appApiToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _securityInventoryPath = Path.Combine(runtime.Root, "security-inventory-" + Guid.NewGuid().ToString("N") + ".jsonl");
        var captureOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) captureOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var capture = new FileStream(_securityInventoryPath, captureOptions)) { }
        string components = Path.Combine(runtime.Root, "components");
        Directory.CreateDirectory(components);
        await File.WriteAllTextAsync(Path.Combine(components, "state.yaml"), $"apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore\nspec:\n  type: state.redis\n  version: v1\n  metadata:\n  - name: redisHost\n    value: 127.0.0.1:{_redisPort}\n  - name: actorStateStore\n    value: \"true\"\n  - name: keyPrefix\n    value: appid\n");
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:" + _hostPort,
            ["P1R_DOMAIN_URL"] = "http://127.0.0.1:" + domainPort,
            ["P1R_DOMAIN_APP_ID"] = _appId + "-domain",
            ["DAPR_HTTP_PORT"] = _sidecarPort.ToString(), ["DAPR_GRPC_PORT"] = grpc.ToString(),
            ["ASPNETCORE_ENVIRONMENT"] = "Testing", ["APP_API_TOKEN"] = appApiToken,
            ["P1R_QUERY_SIGNING_KEY_PATH"] = _querySigningKeyPath,
            ["P1R_SECURITY_INVENTORY_PATH"] = _securityInventoryPath,
        };
        _profileId = Guid.NewGuid().ToString("N");
        var daprdInput = await runtime.CaptureScenarioInputAsync(_profileId, "daprd", Path.Combine(_binaries, "daprd"));
        var componentInput = await runtime.CaptureScenarioInputAsync(_profileId, "component", Path.Combine(components, "state.yaml"));
        var domainProcess = runtime.Start(new("dotnet", [domain.Assembly, "--urls", "http://127.0.0.1:" + domainPort], Path.GetDirectoryName(domain.Project)!, TimeSpan.FromMinutes(1),
            new Dictionary<string, string> { ["P1R_QUERY_VERIFICATION_KEY"] = _queryVerificationKey,
                ["DAPR_HTTP_PORT"] = _sidecarPort.ToString(), ["DAPR_GRPC_PORT"] = grpc.ToString() }), domain, _profileId);
        _writers.Add(domainProcess);
        var actualHost = runtime.Start(new("dotnet", [host.Assembly], Path.GetDirectoryName(host.Project)!, TimeSpan.FromMinutes(1), environment), host, _profileId);
        _securityHostPid = actualHost.Process.Id; _writers.Add(actualHost);
        await WaitHttpAsync("http://127.0.0.1:" + domainPort + "/identity");
        await WaitHttpAsync("http://127.0.0.1:" + _hostPort + "/identity");
        foreach ((ConsumerBuild build, int port) in new[] { (host, _hostPort), (domain, domainPort) })
        {
            JsonElement identity = await GetAsync("http://127.0.0.1:" + port + "/identity");
            PublishedScenarioTests.ActiveAssertions?.Record("loaded-" + build.Role, identity.GetRawText());
            var admission = PublishedScenarioTests.ActiveAssertions ?? new MeasuredAssertions();
            int failures = admission.Failures.Count;
            PublishedScenarioTests.ValidateIdentity(build, identity, admission);
            if (admission.Failures.Count != failures)
                throw new InvalidOperationException("Live consumer identity does not match its independently verified build/archive inputs.");
        }
        var sidecarEnvironment = new Dictionary<string, string>
        {
            ["APP_API_TOKEN"] = appApiToken,
        };
        var actualSidecar = runtime.Start(new(Path.Combine(_binaries, "daprd"), ["--app-id", _appId, "--app-port", _hostPort.ToString(),
            "--app-channel-address", "127.0.0.1", "--dapr-http-port", _sidecarPort.ToString(), "--dapr-grpc-port", grpc.ToString(),
            "--dapr-internal-grpc-port", Port().ToString(), "--metrics-port", Port().ToString(), "--profile-port", Port().ToString(),
            "--placement-host-address", "127.0.0.1:" + _placementPort, "--scheduler-host-address", "127.0.0.1:" + _schedulerPort, "--resources-path", components,
            "--log-level", "warn"], runtime.Root, TimeSpan.FromMinutes(1), sidecarEnvironment));
        _writers.Add(actualSidecar);
        runtime.RecordDaprLaunch(new(_profileId, runtime.Id, daprdInput.Path, components, _appId, _hostPort, _sidecarPort, actualSidecar.Process.Id, DateTimeOffset.UtcNow, "Host", actualSidecar.Started, null));
        await WaitHttpAsync("http://127.0.0.1:" + _sidecarPort + "/v1.0/healthz");
        OwnedProcess? domainSidecar = null;
        int? domainSidecarPort = null;
        if (host.Version == ConsumerMaterializer.SelectedVersion)
        {
            domainSidecarPort = Port();
            domainSidecar = runtime.Start(new(Path.Combine(_binaries, "daprd"), ["--app-id", _appId + "-domain", "--app-port", domainPort.ToString(),
                "--app-channel-address", "127.0.0.1", "--dapr-http-port", domainSidecarPort.Value.ToString(), "--dapr-grpc-port", Port().ToString(),
                "--dapr-internal-grpc-port", Port().ToString(), "--metrics-port", Port().ToString(), "--profile-port", Port().ToString(),
                "--placement-host-address", "127.0.0.1:" + _placementPort, "--scheduler-host-address", "127.0.0.1:" + _schedulerPort,
                "--resources-path", components,
                "--log-level", "warn"], runtime.Root, TimeSpan.FromMinutes(1)));
            _writers.Add(domainSidecar);
            runtime.RecordDaprLaunch(new(_profileId, runtime.Id, daprdInput.Path, components, _appId + "-domain", domainPort, domainSidecarPort.Value, domainSidecar.Process.Id, DateTimeOffset.UtcNow, "Domain", domainSidecar.Started, null));
            await WaitHttpAsync("http://127.0.0.1:" + domainSidecarPort + "/v1.0/healthz");
        }
        PublishedScenarioTests.ActiveAssertions?.Record("runtime-profile", JsonSerializer.Serialize(new
        {
            profileId = _profileId, owner = runtime.Id, root = runtime.Root, observedAt = DateTimeOffset.UtcNow,
            appId = _appId, runtime = "1.18.4", redisImage = RedisImage, redisContainer = _redis, redisVolume = _redisVolume,
            sidecarAddress = "http://127.0.0.1:" + _sidecarPort, redisAddress = "127.0.0.1:" + _redisPort,
            daprdPath = daprdInput.Path, componentPath = componentInput.Path,
            hostAddress = "http://127.0.0.1:" + _hostPort, domainAddress = "http://127.0.0.1:" + domainPort,
            sidecarProcessId = actualSidecar.Process.Id, hostProcessId = actualHost.Process.Id, domainProcessId = domainProcess.Process.Id,
            domainSidecarProcessId = domainSidecar?.Process.Id, domainSidecarAddress = domainSidecarPort is null ? null : "http://127.0.0.1:" + domainSidecarPort,
            componentHash = componentInput.Sha256, daprdHash = daprdInput.Sha256, host, domain,
        }));
        await Task.Delay(TimeSpan.FromSeconds(1), OperationCancellation);
    }

    private async Task FreshDatabaseAsync(string? dump = null)
    {
        _redis = runtime.ReserveContainer("redis-" + ++_databaseNumber);
        _redisPort = Port();
        string volume = runtime.ReserveVolume("redis-data-" + _databaseNumber);
        _redisVolume = volume;
        await runtime.RequireAsync("docker", ["volume", "create", "--label", "p1r.owner=" + runtime.Id, volume]);
        await runtime.RequireAsync("docker", ["create", "--name", _redis, "--label", "p1r.owner=" + runtime.Id,
            "--mount", "type=volume,source=" + volume + ",destination=/data",
            "-p", "127.0.0.1:" + _redisPort + ":6379", RedisImage, "redis-server", "--appendonly", "no"]);
        if (dump is not null)
        {
            await runtime.ObserveProviderDumpAsync(dump, "provider-backup-before-restore", OperationCancellation);
            await runtime.RequireAsync("docker", ["cp", dump, _redis + ":/data/dump.rdb"]);
        }
        await runtime.RequireAsync("docker", ["start", _redis]);
        DateTimeOffset until = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            CommandObservation ping = await runtime.RunAsync(new("docker", ["exec", _redis, "redis-cli", "PING"], runtime.Root, TimeSpan.FromSeconds(5)));
            if (ping.ExitCode == 0 && ping.StandardOutput.Trim() == "PONG") break;
            if (DateTimeOffset.UtcNow >= until) throw new TimeoutException("Owned Redis did not become ready.");
            await Task.Delay(200, OperationCancellation);
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
        string address = "http://127.0.0.1:" + _hostPort + "/execute/" + Uri.EscapeDataString(actor);
        JsonElement result = await PostAsync(address, new { append });
        PublishedScenarioTests.ActiveAssertions?.Record("actual-execute-result", JsonSerializer.Serialize(new
        {
            profileId = _profileId, actor, address, append, hostPid = _securityHostPid,
            result = HistoricalSafeJson.Capture(result), responseHash = _lastPostResponseHash,
        }));
        return result;
    }

    private async Task FixtureAsync(string actor, object operations)
    {
        string address = "http://127.0.0.1:" + _hostPort + "/fixture/" + Uri.EscapeDataString(actor);
        DateTimeOffset began = DateTimeOffset.UtcNow;
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(operations, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var message = new HttpRequestMessage(HttpMethod.Post, address) { Content = new ByteArrayContent(input) };
        message.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using HttpResponseMessage response = await _http.SendAsync(message, OperationCancellation);
        byte[] output = await response.Content.ReadAsByteArrayAsync(OperationCancellation);
        PublishedScenarioTests.ActiveAssertions?.Record("fixture-http-post", JsonSerializer.Serialize(new { address, began, ended = DateTimeOffset.UtcNow,
            status = (int)response.StatusCode, requestHash = QualificationRuntime.Hash(input), responseHash = QualificationRuntime.Hash(output),
            request = HistoricalHttpOperationCapture.Request(operations), response = HistoricalHttpOperationCapture.Response(Encoding.UTF8.GetString(output)) }));
        response.EnsureSuccessStatusCode();
    }

    private async Task<string?> ReadAsync(string actor, string key, List<string>? inventoryReads = null)
    {
        string address = "http://127.0.0.1:" + _hostPort + "/read/" + Uri.EscapeDataString(actor) + "/" + Uri.EscapeDataString(key);
        DateTimeOffset began = DateTimeOffset.UtcNow;
        using HttpResponseMessage response = await _http.GetAsync(address, OperationCancellation);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(OperationCancellation);
        string? value = response.StatusCode == HttpStatusCode.NotFound ? null : Encoding.UTF8.GetString(bytes);
        var read = new QualificationStateRead(Guid.NewGuid().ToString("N"), _profileId, runtime.Id, _appId, _redis,
            actor, key, address, _securityHostPid, began, DateTimeOffset.UtcNow, (int)response.StatusCode, bytes.LongLength,
            QualificationRuntime.Hash(bytes), value is null ? null : QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value)));
        runtime.RecordStateRead(read); inventoryReads?.Add(read.Id);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return value;
    }

    private async Task<Dictionary<string, string?>> InventoryAsync(string actor, int head)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var readIds = new List<string>();
        foreach (string key in new[] { actor + ":metadata", actor + ":snapshot" }.Concat(Enumerable.Range(1, head).Select(sequence => actor + ":events:" + sequence)))
        {
            string? raw = await ReadAsync(actor, key, readIds);
            result.Add(key, raw is null ? null : QualificationRuntime.Hash(Encoding.UTF8.GetBytes(raw)));
        }
        PublishedScenarioTests.ActiveAssertions?.Record("inventory:" + actor, JsonSerializer.Serialize(new { fixture = actor, appId = _appId, database = _redis, profileId = _profileId, readIds, hashes = result }));
        return result;
    }

    private async Task WaitHttpAsync(string address)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(OperationCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try
            {
                using HttpResponseMessage response = await _http.GetAsync(address, deadline.Token);
                if (response.IsSuccessStatusCode) return;
                if ((int)response.StatusCode >= 500)
                {
                    string body = await response.Content.ReadAsStringAsync(deadline.Token);
                    bool transientDaprReadiness = address.EndsWith("/v1.0/healthz", StringComparison.Ordinal)
                        && body.Contains("ERR_HEALTH_NOT_READY", StringComparison.Ordinal);
                    if (!transientDaprReadiness) throw new InvalidOperationException("Host readiness returned " + response.StatusCode + ": " + body);
                }
            }
            catch (HttpRequestException) { }
            await Task.Delay(200, deadline.Token);
        }
    }

    private async Task StopWritersAsync()
    {
        foreach (OwnedProcess process in _writers.AsEnumerable().Reverse()) await QualificationRuntime.StopAsync(process, OperationCancellation);
        _writers.Clear();
    }

    private ConsumerBuild Build(string role, string version, bool source = false)
        => builds.Single(build => build.Role == role && build.Version == (version == "tagged-source" ? ConsumerMaterializer.SelectedVersion : version)
            && (build.Source is not null) == (source || version == "tagged-source"));

    private static bool IsOk(JsonElement result) => result.TryGetProperty("ok", out JsonElement ok) && ok.GetBoolean();

    private static bool Equal(Dictionary<string, string?> left, Dictionary<string, string?> right)
        => left.Count == right.Count && left.All(pair => right.GetValueOrDefault(pair.Key) == pair.Value);

    private static string Address(string tenant) => tenant + ":counter:" + Guid.NewGuid().ToString("N");

    /// <summary>Selects an invocation-distinct loopback port with a finite collision retry budget.</summary>
    /// <param name="selectPort">An optional deterministic candidate selector for collision regressions.</param>
    internal int Port(Func<int>? selectPort = null)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int candidate;
            if (selectPort is not null) candidate = selectPort();
            else
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                candidate = ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            lock (_allocatedPorts)
                if (_allocatedPorts.Add(candidate)) return candidate;
        }
        throw new InvalidOperationException("The bounded loopback port selection budget could not find an invocation-distinct port.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopWritersAsync();
        _http.Dispose();
    }
}
