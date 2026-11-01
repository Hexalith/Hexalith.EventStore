using System.Text.Json;

using ExpectedOutcome = (string Execution, string Compatibility, string[] Failures, string? Error);

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Checks the finite comparison-only 3.128.2 experiment without granting technical qualification.</summary>
internal static class HistoricalMatrixMeasurementContract
{
    private const string Selected = "3.128.2";
    private const string SelectedSource = "935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8";
    private const string ProjectionXmlError = "System.Runtime.Serialization.InvalidDataContractException: Type 'Hexalith.EventStore.Contracts.Projections.ProjectionEventDto' cannot be serialized.";
    private static readonly Dictionary<(string Scenario, string Case), ExpectedOutcome> _cases = CreateCases();

    /// <summary>Requires every original case, unchanged failure accounting and a nonqualifying receipt.</summary>
    internal static void Validate(IReadOnlyList<ScenarioObservation> observations, JsonElement summary,
        JsonElement actualCapture, IReadOnlyList<ConsumerBuild> builds, IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        if (observations.Count != _cases.Count || observations.Select(item => (item.Scenario, item.Case)).Distinct().Count() != _cases.Count)
            throw new InvalidOperationException("Historical characterization requires every distinct original case.");
        foreach (ScenarioObservation observation in observations)
        {
            ValidateObservation(observation);
            HistoricalMatrixEvidenceContract.Validate(observation, builds, commands);
            if (observation.Error is not null) ValidateXmlFailure(observation, builds, commands);
        }
        ValidateReceiptClaims(summary);
        ValidateCapture(summary, actualCapture);
        if (!summary.TryGetProperty("observations", out JsonElement saved)
            || !JsonElement.DeepEquals(saved, JsonSerializer.SerializeToElement(observations)))
            throw new InvalidOperationException("The saved observations differ from the complete validated measurements.");
        foreach ((string name, int expected) in new[]
        {
            ("assertions", observations.Sum(item => item.Assertions)),
            ("passed", observations.Count(item => item.Execution == "passed")),
            ("failed", observations.Count(item => item.Execution == "failed")),
            ("unavailable", observations.Count(item => item.Execution == "unavailable")),
            ("incompatible", observations.Count(item => item.Compatibility == "incompatible")),
        })
            if (summary.GetProperty(name).GetInt32() != expected)
                throw new InvalidOperationException("Historical characterization cannot erase measured " + name + ".");
    }

    private static void ValidateCapture(JsonElement summary, JsonElement capture)
    {
        static string RequireText(JsonElement element, string name, int length)
        {
            if (!element.TryGetProperty(name, out JsonElement field) || field.ValueKind != JsonValueKind.String
                || field.GetString() is not { } value || value.Length != length
                || !value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new InvalidOperationException("The actual executor capture lacks " + name + ".");
            return value;
        }
        string executor = RequireText(capture, "executor", 40);
        string diffHash = RequireText(capture, "diffHash", 64);
        _ = RequireText(capture, "coordinatorHash", 64);
        if (RequireText(capture, "builds", 40) != "a22b55f0a4f6d2aaa3a5ccc505896064ac595364"
            || !capture.TryGetProperty("consumerInputOrigin", out JsonElement origin)
            || origin.GetString() is not { Length: > 0 } path || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("The actual executor capture lost its selected Builds or input origin.");
        foreach (string hashes in new[] { "templateHashes", "untrackedHashes" })
        {
            if (!capture.TryGetProperty(hashes, out JsonElement entries) || entries.ValueKind != JsonValueKind.Object
                || hashes == "templateHashes" && !entries.EnumerateObject().Any())
                throw new InvalidOperationException("The actual executor input inventory is missing.");
            foreach (JsonProperty entry in entries.EnumerateObject()) _ = RequireText(entries, entry.Name, 64);
        }
        if (!summary.TryGetProperty("builds", out JsonElement builds) || builds.GetString() != "db897eac9f2819220b32d237109d7255218b46a9"
            || !summary.TryGetProperty("executor", out JsonElement savedExecutor) || savedExecutor.GetString() != executor
            || !summary.TryGetProperty("executorDiffHash", out JsonElement savedDiff) || savedDiff.GetString() != diffHash
            || !summary.TryGetProperty("executorInputs", out JsonElement savedInputs) || !JsonElement.DeepEquals(savedInputs, capture))
            throw new InvalidOperationException("The saved provenance differs from the actual captured executor inputs.");
    }

    private static void ValidateXmlFailure(ScenarioObservation observation, IReadOnlyList<ConsumerBuild> builds,
        IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        string[] pair = observation.Case[..^"-xml".Length].Split("-to-", StringSplitOptions.None);
        string failingConsumer = pair[0].StartsWith(Selected, StringComparison.Ordinal) ? pair[1] : pair[0];
        ConsumerBuild? build = builds.SingleOrDefault(candidate => candidate.Role == "Probe"
            && candidate.Version + (candidate.Source is null ? "-package-" : "-source-") + candidate.Role == failingConsumer);
        if (build is null) throw new InvalidOperationException("The old XML failure has no actual prepared consumer.");
        string operation = failingConsumer == pair[0] ? "make-wire" : "wire";
        HistoricalExecutionCommand[] matching = commands.Where(command =>
        {
            JsonElement actual = command.Record;
            JsonElement invocation = actual.GetProperty("Command");
            return invocation.GetProperty("FileName").GetString() == "dotnet"
                && invocation.GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()).SequenceEqual(new[] { build.Assembly })
                && invocation.GetProperty("WorkingDirectory").GetString() == Path.GetDirectoryName(build.Project)
                && actual.GetProperty("Started").GetDateTimeOffset() >= observation.Started && actual.GetProperty("Ended").GetDateTimeOffset() <= observation.Ended
                && actual.GetProperty("ExitCode").GetInt32() == 134 && !actual.GetProperty("OutputTruncated").GetBoolean();
        }).ToArray();
        if (matching.Length != 1) throw new InvalidOperationException("The XML exception has no unique actual failing command.");
        HistoricalExecutionCommand actualCommand = matching[0];
        JsonElement record = actualCommand.Record;
        string output = record.GetProperty("StandardOutput").GetString()!;
        string error = record.GetProperty("StandardError").GetString()!;
        if (actualCommand.Operation != operation || actualCommand.Kind != "projection"
            || operation == "wire" && actualCommand.Serializer != "xml"
            || !error.Contains("Unhandled exception. " + ProjectionXmlError, StringComparison.Ordinal)
            || !observation.Error!.StartsWith($"System.InvalidOperationException: Exit 134: dotnet {build.Assembly}\n{output}\n{error}", StringComparison.Ordinal))
            throw new InvalidOperationException("The XML failure is not the implicated consumer's actual serialization operation.");
    }

    /// <summary>Rejects any new supported failure, unknown historical deficit or lost measurement.</summary>
    internal static void ValidateObservation(ScenarioObservation actual)
    {
        if (!_cases.TryGetValue((actual.Scenario, actual.Case), out ExpectedOutcome expected))
            throw new InvalidOperationException("Unexpected historical case: " + actual.Scenario + "/" + actual.Case);
        if (expected.Execution == "passed" && expected.Compatibility == "compatible")
        {
            if (!actual.Passing) throw new InvalidOperationException("A supported historical operation regressed: " + actual.Case);
            return;
        }
        // Comparison-only does not mean arbitrary failures are acceptable. These immutable
        // contracts have specific absent fields/guards; their original unsafe effects remain failed.
        if (actual.Execution != expected.Execution || actual.Compatibility != expected.Compatibility
            || actual.Failures.Count != expected.Failures.Length
            || !actual.Failures.Order(StringComparer.Ordinal).SequenceEqual(expected.Failures.Order(StringComparer.Ordinal))
            || actual.Started is null || actual.Ended is null || actual.Ended < actual.Started
            || (expected.Error is null ? actual.Error is not null
                : actual.Error is null || !actual.Error.StartsWith("System.InvalidOperationException: Exit 134: dotnet ", StringComparison.Ordinal)
                    || !actual.Error.Contains("Unhandled exception. " + expected.Error, StringComparison.Ordinal))
            || (expected.Error is null && actual.Assertions <= 0)
            || (expected.Error is not null && actual.Assertions != 0)
            || actual.Passing)
            throw new InvalidOperationException("Historical deficit or refusal changed: " + actual.Scenario + "/" + actual.Case);
    }

    /// <summary>Preserves the separate historical ownership, recovery and qualification decisions.</summary>
    internal static void ValidateReceiptClaims(JsonElement summary)
    {
        if (summary.GetProperty("selected").GetString() != Selected
            || summary.GetProperty("source").GetString() != SelectedSource
            || summary.GetProperty("qualified").GetBoolean() || summary.GetProperty("p1rUsable").GetBoolean()
            || summary.GetProperty("rollback").ValueKind != JsonValueKind.Null
            || summary.GetProperty("owners").GetString() != "pending" || summary.GetProperty("conformance").GetString() != "pending")
            throw new InvalidOperationException("Historical characterization cannot grant compatibility, rollback or owner acceptance.");
    }

    private static Dictionary<(string Scenario, string Case), ExpectedOutcome> CreateCases()
    {
        var cases = new Dictionary<(string, string), ExpectedOutcome>();
        string[] versions = ["3.70.1", "3.110.0", Selected];
        string[] probes = ["3.70.1-package-Probe", "3.110.0-package-Probe", Selected + "-package-Probe", Selected + "-source-Probe"];
        (string Version, string Configuration)[] profiles = [("3.70.1", "Release"), ("3.110.0", "Release"), (Selected, "Release"), (Selected, "Debug")];
        string[] hops = ["3.70.1-to-" + Selected, Selected + "-to-3.70.1", "3.110.0-to-" + Selected,
            Selected + "-to-3.110.0", "tagged-source-to-" + Selected, Selected + "-to-tagged-source"];

        // Exact case geometry mirrors the fixed experiment, not a permissive version predicate.
        foreach (string origin in new[] { "3.70.1-package", "3.110.0-package", Selected + "-package", Selected + "-source" })
        foreach (string role in new[] { "Host", "Domain", "Probe" }) Add("provenance", origin + "-" + role);
        foreach (string probe in probes)
        {
            Add("legacy-metadata", probe + "-three-argument-constructor");
            Add("mixed-api", probe + "-cursor");
        }
        foreach (string version in versions)
        {
            foreach (bool pascal in new[] { false, true }) Add("legacy-metadata", version + "-" + pascal);
            foreach (int floor in new[] { 1, 5, 0, 14 }) Add("metadata-read", version + "-floor-" + floor);
        }
        Add("query-wire", "real-signed-admission-and-refusals");
        foreach (string writer in probes)
        foreach (string reader in probes.Where(reader => reader != writer))
        foreach (string serializer in new[] { "json", "xml" })
        foreach (string kind in new[] { "query", "projection" }) Add(kind + "-wire", writer + "-to-" + reader + "-" + serializer);
        foreach (string hop in hops)
        {
            Add("full-replay", hop);
            foreach (string scenario in new[] { "snapshot-tail", "retained-covered", "retained-uncovered", "missing-event", "metadata-write" })
                Add(scenario, hop + "-" + scenario);
            Add("retained-uncovered", hop + "-retained-uncovered-absent");
            Add("missing-event", hop + "-missing-event-tail");
        }
        foreach ((string version, string configuration) in profiles)
        {
            string profile = version + "-" + configuration;
            foreach (string invalid in new[] { "floor-0", "floor-14", "unreadable", "protected-mismatch", "unknown-type", "version-987" })
            foreach (string tenant in new[] { "tenant-a", "tenant-b" }) Add("invalid-evidence", profile + "-" + invalid + "-" + tenant);
            foreach (int floor in new[] { 0, 14 }) Add("invalid-evidence", profile + "-head12-floor" + floor);
            foreach (int floor in new[] { 1, 5, 13 }) Add("metadata-write", profile + "-head12-floor" + floor);
            Add("mixed-api", profile + "-persisted-status-tristate");
            Add("mixed-api", profile);
        }
        foreach (string version in new[] { "3.70.1", "3.110.0", Selected, "tagged-source" }) Add("post-upgrade-restore", version);
        foreach (string version in new[] { "3.70.1", "3.110.0" }) Add("pre-upgrade-restore", version);
        foreach (string configuration in new[] { "Release", "Debug" })
        {
            Add("query-wire", configuration + "-production-declared-handler-discovery-transport");
            Add("query-wire", configuration + "-supported-handler-and-actor-routes");
            Add("projection-wire", configuration + "-durably-applied-watermark-cursor");
            Add("registered-evolution", configuration + "-registered-manifest-production-hop");
            Add("registered-evolution", configuration);
            Add("natural-reminder", configuration);
        }
        foreach (string hop in new[] { "client-3.70.1-Release-to-host-3.128.2-Release", "client-3.128.2-Release-to-host-3.70.1-Release",
            "client-3.110.0-Release-to-host-3.128.2-Release", "client-3.128.2-Release-to-host-3.110.0-Release",
            "client-3.128.2-Release-to-host-3.128.2-Debug", "client-3.128.2-Debug-to-host-3.128.2-Release" }) Add("mixed-api", hop);
        Add("checkout", "package-to-exact-tag-source");
        foreach (string drill in new[] { "timeout-and-detached-descendant", "cancellation-and-startup-failure", "matrix-cleanup-twice" }) Add("failure-cleanup", drill);

        // 3.70.1 lacks retained-floor metadata and cursor/status/method contracts. These
        // successful measurements remain incompatible, not successful protected operations.
        foreach (string name in new[] { "3.70.1-package-Probe-three-argument-constructor", "3.70.1-False", "3.70.1-True" }) IncompatibleMeasurement("legacy-metadata", name);
        foreach (int floor in new[] { 1, 5, 0, 14 }) IncompatibleMeasurement("metadata-read", "3.70.1-floor-" + floor);
        IncompatibleMeasurement("metadata-write", Selected + "-to-3.70.1-metadata-write");
        foreach (string name in new[] { "3.70.1-package-Probe-cursor", "3.70.1-Release", "client-3.70.1-Release-to-host-3.128.2-Release", "client-3.128.2-Release-to-host-3.70.1-Release" }) IncompatibleMeasurement("mixed-api", name);
        foreach (string version in new[] { "3.70.1", "3.110.0" }) IncompatibleMeasurement("pre-upgrade-restore", version);

        string[] authorityFields = ["originalActorId", "authenticatedWorkloadId", "isDelegated", "identityAdmissionProof", "callerAuthority", "scopes", "audience", "delegationId"];
        string[] projectionFields = ["globalPosition", "metadataVersion", "storedEventContractType", "storedPayloadVersion", "storedSerializationFormat", "effectiveEventContractType", "effectivePayloadVersion", "effectiveSerializationFormat", "effectivePayload", "storedEventTypeName", "storedDigest", "registryFingerprint", "isAdapted"];
        foreach (string writer in probes)
        foreach (string reader in probes.Where(reader => reader != writer))
        foreach (string serializer in new[] { "json", "xml" })
        {
            string name = writer + "-to-" + reader + "-" + serializer;
            bool oldest = writer.StartsWith("3.70.1-", StringComparison.Ordinal) || reader.StartsWith("3.70.1-", StringComparison.Ordinal);
            bool middle = writer.StartsWith("3.110.0-", StringComparison.Ordinal) || reader.StartsWith("3.110.0-", StringComparison.Ordinal);
            if (!oldest && !middle) continue;
            // Current query constructors reconstruct CallerAuthority on selected->old reads;
            // they cannot restore lost signed proof or other absent dual-principal fields.
            string[] query = oldest ? authorityFields.Where(field => field != "callerAuthority" || !writer.StartsWith(Selected, StringComparison.Ordinal)).ToArray()
                : writer.StartsWith("3.110.0-", StringComparison.Ordinal) ? ["identityAdmissionProof", "callerAuthority"] : ["identityAdmissionProof"];
            Deficit("query-wire", name, "incompatible", query.Select(field => "wire-preserves-" + field).ToArray());
            if (serializer == "xml")
                Set("projection-wire", name, ("failed", "unverified", [], ProjectionXmlError));
            else
                Deficit("projection-wire", name, "incompatible", projectionFields.Where(field => oldest || field != "globalPosition").Select(field => "wire-preserves-" + field).ToArray());
        }
        // Old replay/writer binaries lack the newer complete-batch, protection/version and
        // retained-floor guards. Keep their unsafe effects and precise failed checks visible.
        foreach (string version in new[] { "3.70.1", "3.110.0" })
        {
            foreach (string tenant in new[] { "tenant-a", "tenant-b" })
            foreach (string invalid in new[] { "floor-0", "floor-14", "unreadable", "protected-mismatch", "unknown-type", "version-987" })
            {
                string[] failures = invalid is "unreadable" or "unknown-type" ? ["whole-batch-admitted-before-apply"]
                    : invalid is "floor-0" or "floor-14" ? ["invalid-history-typed-refusal", "typed-error-observed", "refusal-preserves-domain-inventory"]
                    : ["actual-domain-invalid-replay-refused", "invalid-history-typed-refusal", "typed-error-observed", "refusal-preserves-domain-inventory"];
                Deficit("invalid-evidence", version + "-Release-" + invalid + "-" + tenant, "unverified", failures);
            }
            foreach (int floor in new[] { 0, 14 }) Deficit("invalid-evidence", version + "-Release-head12-floor" + floor, "unverified",
                ["actual-writer-malformed-floor-production-type-code-stage", "malformed-floor-before-protectionCalls", "malformed-floor-before-allocations", "malformed-floor-before-staging"]);
        }
        foreach (int floor in new[] { 1, 5, 13 }) Deficit("metadata-write", "3.70.1-Release-head12-floor" + floor, "unverified", ["boundary-floor-preserved"]);
        Deficit("post-upgrade-restore", "3.70.1", "incompatible", ["restored-writer-preserves-floor-five", "restore-append-preserves-coherent-source-proof-coordinates", "restored-floor-five-survives-fresh-restart"]);
        Deficit("post-upgrade-restore", "3.110.0", "incompatible", ["restore-append-preserves-coherent-source-proof-coordinates"]);
        Deficit("mixed-api", "3.70.1-Release-persisted-status-tristate", "incompatible", ["persisted-status-tristate-", "persisted-status-tristate-False", "persisted-status-tristate-True"]);
        // The real selected manifest hop deliberately proves a missing-callable refusal;
        // its focused test requires incompatible and no mutation in BOTH consumer origins.
        foreach (string configuration in new[] { "Release", "Debug" }) IncompatibleMeasurement("registered-evolution", configuration + "-registered-manifest-production-hop");
        return cases;

        void Add(string scenario, string name) => cases.Add((scenario, name), ("passed", "compatible", [], null));
        void IncompatibleMeasurement(string scenario, string name) => Set(scenario, name, ("passed", "incompatible", [], null));
        void Deficit(string scenario, string name, string disposition, string[] failures) => Set(scenario, name, ("failed", disposition, failures, null));
        void Set(string scenario, string name, ExpectedOutcome outcome)
        {
            if (!cases.ContainsKey((scenario, name))) throw new InvalidOperationException("A deficit cannot create or substitute an experiment case.");
            cases[(scenario, name)] = outcome;
        }
    }
}
