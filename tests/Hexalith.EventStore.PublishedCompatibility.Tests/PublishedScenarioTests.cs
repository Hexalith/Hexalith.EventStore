using System.Text.Json;
using System.Runtime.CompilerServices;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Characterizes the immutable historical comparison matrix without granting compatibility.</summary>
public sealed class PublishedScenarioTests
{
    private static readonly ConditionalWeakTable<List<ScenarioObservation>, string> _observationLogs = new();
    private static readonly AsyncLocal<MeasuredAssertions?> _activeAssertions = new();

    /// <summary>Gets the currently executing case's measurement scope.</summary>
    internal static MeasuredAssertions? ActiveAssertions => _activeAssertions.Value;
    /// <summary>Retains every measured deficit and requires the exact supported and comparison-only boundaries.</summary>
    [Fact]
    public async Task Published31282QualificationMatrixAsync()
    {
        await using var runtime = new QualificationRuntime();
        string repository = FindRepository();
        var materializer = new ConsumerMaterializer(runtime, repository);
        var observations = new List<ScenarioObservation>();
        RegisterObservationLog(observations, Path.Combine(runtime.Root, "scenarios.ndjson"));
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
        await ExecuteLegacyConstructorsAsync(runtime, materializer.Builds, observations);
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
                foreach (string check in new[] { "verified", "principalsBound", "missingRefused", "forgedRefused", "foreignTenantRefused" })
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
                await ContainerCreationDrill.ExecuteAsync(drill, cancel, assertions);
                return assertions.Failures.Count == 0 ? "compatible" : "unverified";
            });
        }
        foreach (string scenario in RequiredScenarios)
            if (!observations.Any(observation => observation.Scenario == scenario))
                observations.Add(new(scenario, "required-lane", "unavailable", "unverified", 0, [], materializationFailure ?? "Required execution did not complete."));
        DateTimeOffset cleanupStarted = DateTimeOffset.UtcNow;
        await runtime.CleanupAsync();
        await runtime.CleanupAsync();
        var cleanup = new MeasuredAssertions();
        cleanup.Check(runtime.CleanupFailures.Count == 0, "all-owned-resources-removed-shared-preserved");
        cleanup.Check(runtime.Commands.All(command => !command.OutputTruncated), "all-command-output-within-retained-bounds");
        observations.Add(new("failure-cleanup", "matrix-cleanup-twice", cleanup.Failures.Count == 0 ? "passed" : "failed", "compatible", cleanup.Count, cleanup.Failures,
            Started: cleanupStarted, Ended: DateTimeOffset.UtcNow));
        string serialized = await WriteSummaryAsync(runtime, observations, materializationFailure);
        TestContext.Current.TestOutputHelper?.WriteLine(serialized);
        using JsonDocument summary = JsonDocument.Parse(serialized);
        using JsonDocument actualCapture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "executor-inputs.json")));
        HistoricalMatrixMeasurementContract.Validate(observations, summary.RootElement, actualCapture.RootElement, materializer.Builds, runtime.Commands.Select(HistoricalExecutionCommand.Capture).ToArray());
    }

    /// <summary>Writes an honest nonqualifying summary when initial executor capture is unavailable.</summary>
    internal static async Task<string> WriteSummaryAsync(QualificationRuntime runtime, List<ScenarioObservation> observations, string? materializationFailure)
    {
        JsonElement? executorInputs = null;
        string? executor = null;
        string? executorDiffHash = null;
        try
        {
            using JsonDocument captured = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(runtime.Root, "executor-inputs.json")));
            executor = captured.RootElement.GetProperty("executor").GetString() ?? throw new InvalidOperationException("Captured executor is unknown.");
            executorDiffHash = captured.RootElement.GetProperty("diffHash").GetString() ?? throw new InvalidOperationException("Captured executor diff is unknown.");
            executorInputs = captured.RootElement.Clone();
        }
        catch (Exception error)
        {
            executor = null;
            executorDiffHash = null;
            observations.Add(new("provenance", "executor-input-capture", "unavailable", "unverified", 0, [],
                materializationFailure ?? error.ToString()));
        }
        var summary = new { selected = ConsumerMaterializer.SelectedVersion, source = ConsumerMaterializer.SelectedSource,
            builds = ConsumerMaterializer.SelectedBuilds, executor, executorDiffHash, executorInputs,
            assertions = observations.Sum(observation => observation.Assertions),
            passed = observations.Count(observation => observation.Execution == "passed"),
            failed = observations.Count(observation => observation.Execution == "failed"),
            unavailable = observations.Count(observation => observation.Execution == "unavailable"),
            incompatible = observations.Count(observation => observation.Compatibility == "incompatible"),
            qualified = executorInputs is not null && observations.All(observation => observation.Passing), p1rUsable = false, rollback = (string?)null,
            owners = "pending", conformance = "pending", observations };
        string serialized = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "summary.json"), serialized);
        return serialized;
    }

    /// <summary>Names every required scenario and independently selected addition.</summary>
    internal static readonly string[] RequiredScenarios = ["provenance", "legacy-metadata", "metadata-read", "metadata-write", "full-replay",
        "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "invalid-evidence", "query-wire", "projection-wire",
        "mixed-api", "checkout", "post-upgrade-restore", "pre-upgrade-restore", "failure-cleanup", "natural-reminder", "registered-evolution"];

    /// <summary>Executes one case and preserves its assertions, timing, measurements, and disposition.</summary>
    internal static async Task ObserveAsync(List<ScenarioObservation> observations, string scenario, string name,
        Func<MeasuredAssertions, Task<string>> operation)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var assertions = new MeasuredAssertions();
        DateTimeOffset began = DateTimeOffset.UtcNow;
        MeasuredAssertions? previous = _activeAssertions.Value;
        _activeAssertions.Value = assertions;
        try
        {
            string compatibility = await operation(assertions);
            if (assertions.Failures.Count > 0 && compatibility == "compatible") compatibility = "unverified";
            observations.Add(new(scenario, name, assertions.Failures.Count == 0 && assertions.Count > 0 ? "passed" : "failed",
                compatibility, assertions.Count, assertions.Failures, Measurements: assertions.Measurements, Started: began, Ended: DateTimeOffset.UtcNow));
        }
        catch (Exception error)
        {
            observations.Add(new(scenario, name, "failed", "unverified", assertions.Count, assertions.Failures, error.ToString(), assertions.Measurements, began, DateTimeOffset.UtcNow));
        }
        finally { _activeAssertions.Value = previous; }
        if (_observationLogs.TryGetValue(observations, out string? observationLog))
            await File.AppendAllTextAsync(observationLog, JsonSerializer.Serialize(observations[^1]) + "\n");
    }

    /// <summary>Binds incremental output to one list, so separate test executions cannot append to another invocation.</summary>
    internal static void RegisterObservationLog(List<ScenarioObservation> observations, string path) => _observationLogs.Add(observations, path);

    /// <summary>Invokes a physically independent consumer and records its exact command observation.</summary>
    internal static async Task<JsonElement> ProbeAsync(QualificationRuntime runtime, ConsumerBuild build, object request)
    {
        string[] arguments = build.Role == "Probe" ? [build.Assembly] : [build.Assembly, "--identity"];
        string raw = await runtime.RequireAsync("dotnet", arguments, Path.GetDirectoryName(build.Project),
            TimeSpan.FromSeconds(30), input: JsonSerializer.Serialize(request));
        CommandObservation command = runtime.Commands[^1];
        ActiveAssertions?.Record("probe-command", JsonSerializer.Serialize(new { command.Command, command.Started, command.Ended, command.ExitCode, command.OutputHash }));
        ActiveAssertions?.Record("consumer-build", JsonSerializer.Serialize(build));
        return ParseProbeOutput(raw);
    }

    /// <summary>Requires exactly one well-formed structured consumer result.</summary>
    internal static JsonElement ParseProbeOutput(string raw)
    {
        string[] lines = raw.Split('\n').Where(line => line.StartsWith("P1R_RESULT ", StringComparison.Ordinal)).ToArray();
        if (lines.Length != 1) throw new InvalidOperationException("Probe must produce exactly one structured result.");
        using JsonDocument json = JsonDocument.Parse(lines[0][11..]);
        return json.RootElement.Clone();
    }

    /// <summary>Checks physically loaded versions, configuration, source, paths, and archive bytes.</summary>
    internal static void ValidateIdentity(ConsumerBuild build, JsonElement result, MeasuredAssertions assertions)
    {
        JsonElement[] identities = result.GetProperty("identities").EnumerateArray().ToArray();
        string[] required = (build.Role == "Host" && build.Version == ConsumerMaterializer.SelectedVersion
            ? ConsumerMaterializer.Packages.Append("Gateway") : ConsumerMaterializer.Packages)
            .Select(name => "Hexalith.EventStore." + name).Order(StringComparer.Ordinal).ToArray();
        string[] actual = identities.Select(identity => identity.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToArray();
        assertions.Check(actual.SequenceEqual(required, StringComparer.Ordinal), "distinct-exact-required-sdk-assembly-set");
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
                assertions.Check(build.VerifiedAssemblyHashes is not null
                    && build.VerifiedAssemblyHashes.TryGetValue(identity.GetProperty("name").GetString()!, out string? verifiedHash)
                    && verifiedHash == identity.GetProperty("sha256").GetString(), "loaded-bytes-from-independently-signature-verified-nupkg-entry");
            }
        }
    }

    /// <summary>Actually invokes the legacy three-argument constructor in every package/source probe.</summary>
    internal static async Task ExecuteLegacyConstructorsAsync(QualificationRuntime runtime, IReadOnlyList<ConsumerBuild> builds, List<ScenarioObservation> observations)
    {
        foreach (ConsumerBuild probe in builds.Where(build => build.Role == "Probe"))
            await ObserveAsync(observations, "legacy-metadata", Key(probe) + "-three-argument-constructor", async assertions =>
            {
                JsonElement result = (await ProbeAsync(runtime, probe, new { operation = "legacy-constructor" })).GetProperty("value");
                assertions.Record("constructed-metadata", result.GetRawText());
                assertions.Check(result.GetProperty("currentSequence").GetInt64() == 12, "constructor-preserves-sequence");
                assertions.Check(result.GetProperty("lastModified").GetDateTimeOffset() == DateTimeOffset.UnixEpoch, "constructor-preserves-timestamp");
                assertions.Check(result.GetProperty("eTag").GetString() == "old", "constructor-preserves-etag");
                bool floor = result.TryGetProperty("retainedFloor", out JsonElement retained);
                assertions.Check(probe.Version == "3.70.1" ? !floor : floor && retained.GetInt64() == 1, "constructor-default-floor-or-actual-old-api-absence");
                return floor ? "compatible" : "incompatible";
            });
    }

    private static string Key(ConsumerBuild build) => build.Version + "-" + (build.Source is null ? "package" : "source") + "-" + build.Role;

    private static string ProjectionFixture() => JsonSerializer.Serialize(new { eventTypeName = "event", payload = "e30=", serializationFormat = "json",
        sequenceNumber = 12, timestamp = DateTimeOffset.UnixEpoch, correlationId = "corr", globalPosition = 987,
        metadataVersion = 2, storedEventContractType = "counter-incremented", storedPayloadVersion = 1, storedSerializationFormat = "json",
        effectiveEventContractType = "counter-incremented", effectivePayloadVersion = 1, effectiveSerializationFormat = "json",
        effectivePayload = "e30=", storedEventTypeName = "event", storedDigest = "AQI=", registryFingerprint = "registry", isAdapted = false });

    /// <summary>Finds the owning repository without changing its Git state.</summary>
    internal static string FindRepository()
    {
        string? configured = Environment.GetEnvironmentVariable("P1R_QUALIFICATION_REPOSITORY");
        if (configured is not null)
            return File.Exists(Path.Combine(configured, "Hexalith.EventStore.slnx")) ? Path.GetFullPath(configured)
                : throw new DirectoryNotFoundException("The configured owning qualification repository does not contain its solution.");
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
