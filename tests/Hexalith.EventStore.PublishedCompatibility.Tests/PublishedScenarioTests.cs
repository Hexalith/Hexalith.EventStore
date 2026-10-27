using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Runs the approved immutable-package matrix; failures remain ordinary failing test results.</summary>
public sealed class PublishedScenarioTests
{
    private static string? _observationLog;
    /// <summary>Measures every required scenario and both additions without advancing owner or readiness decisions.</summary>
    [Fact]
    public async Task Published31282QualificationMatrixAsync()
    {
        await using var runtime = new QualificationRuntime();
        _observationLog = Path.Combine(runtime.Root, "scenarios.ndjson");
        string repository = FindRepository();
        var materializer = new ConsumerMaterializer(runtime, repository);
        var observations = new List<ScenarioObservation>();
        string? materializationFailure = null;
        try { await materializer.MaterializeAsync(); }
        catch (Exception error) { materializationFailure = error.ToString(); }
        if (materializer.Failures.Count > 0) materializationFailure = string.Join("\n", materializer.Failures);
        TestContext.Current.TestOutputHelper?.WriteLine("Invocation scratch: " + runtime.Root);
        foreach (ConsumerBuild build in materializer.Builds)
        {
            await ObserveAsync(observations, "provenance", Key(build), async assertions =>
            {
                JsonElement identity = await ProbeAsync(runtime, build, new { operation = "identity" });
                ValidateIdentity(build, identity, assertions);
                return "compatible";
            });
        }
        foreach (string version in ConsumerMaterializer.Versions)
        {
            ConsumerBuild? probe = materializer.Builds.SingleOrDefault(build => build.Version == version && build.Role == "Probe" && build.Source is null);
            if (probe is null)
            {
                observations.Add(new("provenance", version, "unavailable", "unverified", 0, [], materializationFailure ?? "Missing independent package consumers."));
                continue;
            }
            foreach (bool pascal in new[] { false, true })
            {
                await ObserveAsync(observations, "legacy-metadata", version + "-" + pascal, async assertions =>
                {
                    string value = pascal ? "{\"CurrentSequence\":12,\"LastModified\":\"1970-01-01T00:00:00+00:00\",\"ETag\":\"old\"}"
                        : "{\"currentSequence\":12,\"lastModified\":\"1970-01-01T00:00:00+00:00\",\"eTag\":\"old\"}";
                    JsonElement result = (await ProbeAsync(runtime, probe, new { operation = "metadata", value })).GetProperty("value");
                    assertions.Check(result.GetProperty("currentSequence").GetInt64() == 12, "sequence-12");
                    assertions.Check(result.GetProperty("eTag").GetString() == "old", "etag-preserved");
                    assertions.Check(result.GetProperty("lastModified").GetDateTimeOffset() == DateTimeOffset.UnixEpoch, "timestamp-preserved");
                    bool floor = result.TryGetProperty("retainedFloor", out JsonElement floorValue);
                    assertions.Check(!floor || floorValue.GetInt64() == 1, "legacy-floor-default");
                    return floor ? "compatible" : "incompatible";
                });
            }
            foreach (int floor in new[] { 1, 5, 0, 14 })
            {
                await ObserveAsync(observations, "metadata-read", version + "-floor-" + floor, async assertions =>
                {
                    JsonElement result = (await ProbeAsync(runtime, probe, new { operation = "metadata",
                        value = JsonSerializer.Serialize(new { currentSequence = 12, lastModified = DateTimeOffset.UnixEpoch, eTag = "old", retainedFloor = floor }) })).GetProperty("value");
                    assertions.Check(result.GetProperty("currentSequence").GetInt64() == 12, "head-read");
                    bool retained = result.TryGetProperty("retainedFloor", out JsonElement actual) && actual.GetInt64() == floor;
                    assertions.Check(!result.TryGetProperty("retainedFloor", out actual) || actual.GetInt64() == floor, "floor-read-value");
                    return retained ? "compatible" : "incompatible";
                });
            }
        }
        ConsumerBuild[] probes = materializer.Builds.Where(build => build.Role == "Probe").ToArray();
        foreach (ConsumerBuild probe in probes)
        {
            await ObserveAsync(observations, "mixed-api", Key(probe) + "-cursor", async assertions =>
            {
                JsonElement cursor = await ProbeAsync(runtime, probe, new { operation = "cursor" });
                if (!cursor.GetProperty("supported").GetBoolean())
                {
                    assertions.Check(cursor.GetProperty("denial").GetString() == "incapable-published-endpoint", "typed-cursor-refusal");
                    return "incompatible";
                }
                foreach (string field in new[] { "decoded", "changed", "foreignCaller", "foreignTenant", "foreignDomain", "changedFilter", "changedQuery", "tamperDenied" })
                    assertions.Check(cursor.GetProperty(field).GetBoolean(), "cursor-" + field);
                assertions.Check(cursor.GetProperty("position").GetString() == "987", "exact-position-distinct-from-sequence12");
                assertions.Check(cursor.GetProperty("unknownDenied").GetInt32() == 3, "unknown-watermarks-refuse");
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
        }
        string? signedQuery = null;
        ConsumerBuild? selectedProbe = probes.SingleOrDefault(build => build.Version == ConsumerMaterializer.SelectedVersion && build.Source is null);
        if (selectedProbe is not null)
        {
            await ObserveAsync(observations, "query-wire", "real-signed-admission-and-refusals", async assertions =>
            {
                JsonElement fixture = await ProbeAsync(runtime, selectedProbe, new { operation = "signed-query" });
                foreach (string check in new[] { "verified", "missingRefused", "forgedRefused", "foreignTenantRefused" })
                    assertions.Check(fixture.GetProperty(check).GetBoolean(), "signed-query-" + check);
                signedQuery = fixture.GetProperty("value").GetRawText();
                assertions.Check(!string.IsNullOrWhiteSpace(fixture.GetProperty("value").GetProperty("identityAdmissionProof").GetString()), "nonnull-real-signed-admission");
                return assertions.Failures.Count == 0 ? "compatible" : "incompatible";
            });
        }
        foreach (ConsumerBuild writer in probes)
        foreach (ConsumerBuild reader in probes.Where(reader => reader != writer && (reader.Source is null || writer.Source is null)))
        foreach (string serializer in new[] { "json", "xml" })
        foreach (string kind in new[] { "query", "projection" })
        {
            await ObserveAsync(observations, kind + "-wire", Key(writer) + "-to-" + Key(reader) + "-" + serializer, async assertions =>
            {
                string value = kind == "query" ? signedQuery ?? throw new InvalidOperationException("Real signed admission fixture is unavailable.") : ProjectionFixture();
                string wire = serializer == "xml"
                    ? (await ProbeAsync(runtime, writer, new { operation = "make-wire", kind, value })).GetProperty("wire").GetString()!
                    : (await ProbeAsync(runtime, writer, new { operation = "wire", kind, serializer, value })).GetProperty("wire").GetString()!;
                JsonElement middle = await ProbeAsync(runtime, reader, new { operation = "wire", kind, serializer, value = wire });
                JsonElement result = await ProbeAsync(runtime, writer, new { operation = "wire", kind, serializer, value = middle.GetProperty("wire").GetString() });
                JsonElement actual = result.GetProperty("value");
                bool compatible = true;
                using JsonDocument expected = JsonDocument.Parse(value);
                foreach (JsonProperty field in expected.RootElement.EnumerateObject())
                {
                    bool equal = actual.TryGetProperty(field.Name, out JsonElement found) && JsonElement.DeepEquals(field.Value, found);
                    assertions.Check(equal, "wire-preserves-" + field.Name);
                    compatible &= equal;
                }
                return compatible ? "compatible" : "incompatible";
            });
        }
        foreach (string version in ConsumerMaterializer.Versions)
        foreach (string role in new[] { "Host", "Domain", "Probe" })
            if (!materializer.Builds.Any(build => build.Role == role && build.Version == version && build.Source is null))
                observations.Add(new("provenance", version + "/" + role, "unavailable", "unverified", 0, [], materializationFailure));
        foreach (string role in new[] { "Host", "Domain", "Probe" })
            if (!materializer.Builds.Any(build => build.Role == role && build.Source is not null))
                observations.Add(new("provenance", "tagged-source/" + role, "unavailable", "unverified", 0, [], materializationFailure));
        if (new[] { "Host", "Domain" }.All(role => materializer.Builds.Any(build => build.Role == role && build.Version == ConsumerMaterializer.SelectedVersion && build.Source is null)))
        {
            await using var cluster = new PublishedCluster(runtime, materializer.Builds);
            try
            {
                await cluster.InitializeAsync();
                await cluster.ExecuteMatrixAsync(observations);
            }
            catch (Exception error)
            {
                observations.Add(new("runtime-initialization", "Redis-Dapr-1.18.4", "unavailable", "unverified", 0, [], error.ToString()));
            }
        }
        foreach (bool cancel in new[] { false, true })
        {
            await ObserveAsync(observations, "failure-cleanup", cancel ? "cancellation-and-startup-failure" : "timeout-and-detached-descendant", async assertions =>
            {
                await using var drill = new QualificationRuntime();
                await drill.CaptureSharedContainersAsync();
                string name = drill.ReserveContainer("interrupted-create");
                await drill.RequireAsync("docker", ["create", "--name", name, "--label", "p1r.owner=" + drill.Id, PublishedCluster.RedisImage]);
                bool startupRefused = false;
                try { drill.Start(new("/missing-p1r-executable", [], drill.Root, TimeSpan.FromSeconds(1))); }
                catch (System.ComponentModel.Win32Exception) { startupRefused = true; }
                assertions.Check(startupRefused, "real-startup-failure-observed");
                using var cancellation = new CancellationTokenSource();
                if (cancel) cancellation.CancelAfter(200);
                bool interrupted = false;
                try
                {
                    await drill.RunAsync(new("python3", ["-c", "import subprocess,time; subprocess.Popen(['sleep','120'],start_new_session=True); time.sleep(120)"],
                        drill.Root, cancel ? TimeSpan.FromSeconds(20) : TimeSpan.FromMilliseconds(200)), cancellation.Token);
                }
                catch (TimeoutException) { interrupted = true; }
                assertions.Check(interrupted, "real-interruption-observed");
                await drill.CleanupAsync();
                await drill.CleanupAsync();
                assertions.Check(drill.CleanupFailures.Count == 0, "repeated-cleanup-owned-absence-and-shared-preservation");
                CommandObservation absent = await drill.RunAsync(new("docker", ["inspect", name], drill.Root, TimeSpan.FromSeconds(10)));
                assertions.Check(absent.ExitCode != 0 && absent.StandardError.Contains("no such object:", StringComparison.OrdinalIgnoreCase), "interrupted-container-absent");
                return assertions.Failures.Count == 0 ? "compatible" : "unverified";
            });
        }
        foreach (string scenario in RequiredScenarios)
            if (!observations.Any(observation => observation.Scenario == scenario))
                observations.Add(new(scenario, "required-lane", "unavailable", "unverified", 0, [], materializationFailure ?? "Required execution did not complete."));
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        var cleanup = new MeasuredAssertions();
        cleanup.Check(runtime.CleanupFailures.Count == 0, "all-owned-resources-removed-shared-preserved");
        observations.Add(new("failure-cleanup", "matrix-cleanup-twice", cleanup.Failures.Count == 0 ? "passed" : "failed", "compatible", cleanup.Count, cleanup.Failures));
        using JsonDocument executorInputs = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "executor-inputs.json")));
        string executor = executorInputs.RootElement.GetProperty("executor").GetString()!;
        var summary = new { selected = ConsumerMaterializer.SelectedVersion, source = ConsumerMaterializer.SelectedSource,
            builds = ConsumerMaterializer.SelectedBuilds, executor, executorDiffHash = executorInputs.RootElement.GetProperty("diffHash").GetString(),
            executorInputs = executorInputs.RootElement.Clone(),
            assertions = observations.Sum(observation => observation.Assertions),
            passed = observations.Count(observation => observation.Execution == "passed"),
            failed = observations.Count(observation => observation.Execution == "failed"),
            unavailable = observations.Count(observation => observation.Execution == "unavailable"),
            incompatible = observations.Count(observation => observation.Compatibility == "incompatible"),
            qualified = observations.All(observation => observation.Passing), p1rUsable = false, rollback = (string?)null,
            owners = "pending", conformance = "pending", observations };
        string serialized = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "summary.json"), serialized);
        TestContext.Current.TestOutputHelper?.WriteLine(serialized);
        observations.All(observation => observation.Passing).ShouldBeTrue("Every required published lane must pass compatibility; see " + runtime.Root);
    }

    internal static readonly string[] RequiredScenarios = ["provenance", "legacy-metadata", "metadata-read", "metadata-write", "full-replay",
        "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "invalid-evidence", "query-wire", "projection-wire",
        "mixed-api", "checkout", "post-upgrade-restore", "pre-upgrade-restore", "failure-cleanup", "natural-reminder", "registered-evolution"];

    internal static async Task ObserveAsync(List<ScenarioObservation> observations, string scenario, string name,
        Func<MeasuredAssertions, Task<string>> operation)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var assertions = new MeasuredAssertions();
        try
        {
            string compatibility = await operation(assertions);
            observations.Add(new(scenario, name, assertions.Failures.Count == 0 && assertions.Count > 0 ? "passed" : "failed",
                compatibility, assertions.Count, assertions.Failures));
        }
        catch (Exception error)
        {
            observations.Add(new(scenario, name, "failed", "unverified", assertions.Count, assertions.Failures, error.ToString()));
        }
        if (_observationLog is not null)
            await File.AppendAllTextAsync(_observationLog, JsonSerializer.Serialize(observations[^1]) + "\n");
    }

    internal static async Task<JsonElement> ProbeAsync(QualificationRuntime runtime, ConsumerBuild build, object request)
    {
        string[] arguments = build.Role == "Probe" ? [build.Assembly] : [build.Assembly, "--identity"];
        string raw = await runtime.RequireAsync("dotnet", arguments, Path.GetDirectoryName(build.Project),
            TimeSpan.FromSeconds(30), input: JsonSerializer.Serialize(request));
        return ParseProbeOutput(raw);
    }

    internal static JsonElement ParseProbeOutput(string raw)
    {
        string[] lines = raw.Split('\n').Where(line => line.StartsWith("P1R_RESULT ", StringComparison.Ordinal)).ToArray();
        if (lines.Length != 1) throw new InvalidOperationException("Probe must produce exactly one structured result.");
        using JsonDocument json = JsonDocument.Parse(lines[0][11..]);
        return json.RootElement.Clone();
    }

    internal static void ValidateIdentity(ConsumerBuild build, JsonElement result, MeasuredAssertions assertions)
    {
        JsonElement[] identities = result.GetProperty("identities").EnumerateArray().ToArray();
        assertions.Check(identities.Length == 5, "five-physically-loaded-packages");
        foreach (JsonElement identity in identities)
        {
            string path = identity.GetProperty("path").GetString()!;
            assertions.Check(Path.GetFullPath(path).StartsWith(Path.GetDirectoryName(build.Assembly)! + Path.DirectorySeparatorChar, StringComparison.Ordinal), "loaded-from-consumer-output");
            assertions.Check(identity.GetProperty("configuration").GetString() == build.Configuration, "loaded-configuration");
            string expectedVersion = build.Source is null ? build.Version
                : build.ResolvedVersions?.GetValueOrDefault(identity.GetProperty("name").GetString()!)
                    ?? throw new InvalidOperationException("Tagged source assembly version was not independently resolved.");
            assertions.Check(identity.GetProperty("informationalVersion").GetString()!.Split('+')[0] == expectedVersion, "loaded-version");
            assertions.Check(identity.GetProperty("sha256").GetString() == QualificationRuntime.Hash(File.ReadAllBytes(path)), "loaded-sha256");
            if (build.Source is null)
            {
                string package = identity.GetProperty("name").GetString()!.ToLowerInvariant();
                assertions.Check(build.ArchiveSources is not null
                    && build.ArchiveSources.TryGetValue(identity.GetProperty("name").GetString()!, out string? expectedSource)
                    && identity.GetProperty("informationalVersion").GetString()!.Split('+').ElementAtOrDefault(1) == expectedSource,
                    "loaded-source-matches-archive-source");
                string archiveDll = Path.Combine(build.Cache, package, build.Version, "lib", "net10.0", identity.GetProperty("name").GetString() + ".dll");
                assertions.Check(File.Exists(archiveDll) && QualificationRuntime.Hash(File.ReadAllBytes(archiveDll)) == identity.GetProperty("sha256").GetString(), "loaded-bytes-from-restored-archive");
            }
        }
    }

    private static string Key(ConsumerBuild build) => build.Version + "-" + (build.Source is null ? "package" : "source") + "-" + build.Role;

    private static string ProjectionFixture() => JsonSerializer.Serialize(new { eventTypeName = "event", payload = "e30=", serializationFormat = "json",
        sequenceNumber = 12, timestamp = DateTimeOffset.UnixEpoch, correlationId = "corr", globalPosition = 987,
        metadataVersion = 2, storedEventContractType = "counter-incremented", storedPayloadVersion = 1, storedSerializationFormat = "json",
        effectiveEventContractType = "counter-incremented", effectivePayloadVersion = 1, effectiveSerializationFormat = "json",
        effectivePayload = "e30=", storedEventTypeName = "event", storedDigest = "AQI=", registryFingerprint = "registry", isAdapted = false });

    internal static string FindRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
