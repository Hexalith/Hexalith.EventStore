using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Preserves instrumented assertion floors and the original producer's required measurement geometry.</summary>
internal static class HistoricalMatrixEvidenceContract
{
    // Frozen job124678675845 / artifact13062045943; instrumented MeasuredAssertions
    // and PublishedCluster/ProbeAsync record sites independently ground these minima.
    // This is a fixed contract, never inferred from a new candidate during validation.
    private static readonly Dictionary<(string Scenario, string Case), (int Assertions, Dictionary<string, int> Records)> _cases = CreateCases();

    /// <summary>Rejects erased successful checks, measurement records or bound safety coordinates.</summary>
    internal static void Validate(ScenarioObservation observation, IReadOnlyList<ConsumerBuild> builds,
        IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        if (!_cases.TryGetValue((observation.Scenario, observation.Case), out var expected)
            || observation.Assertions < expected.Assertions)
            throw new InvalidOperationException("Historical assertion floor was erased: " + observation.Case);
        var records = observation.Measurements ?? new Dictionary<string, string>();
        HistoricalMatrixScenarioContract.ValidateProfiles(observation, records);
        HistoricalMatrixSafetyContract.Validate(observation, records);
        HistoricalMatrixFixtureInputContract.Validate(observation, records);
        HistoricalMatrixInventoryContract.Validate(observation, records);
        HistoricalMatrixHttpContract.Validate(observation, records);
        HistoricalMatrixCrossClientContract.Validate(observation, records, builds, commands);
        HistoricalMatrixCleanupContract.Validate(observation, records, commands);
        HistoricalMatrixSecurityContract.Validate(observation, records, commands);
        var distinctExecutions = new HashSet<(DateTimeOffset, DateTimeOffset, string)>() ;
        foreach ((string kind, int minimum) in expected.Records)
            if (records.Keys.Count(key => Kind(key) == kind) < minimum)
                throw new InvalidOperationException("Required measured record was erased: " + kind + "/" + observation.Case);
        foreach ((string key, string value) in records)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("An empty record cannot measure a boundary.");
            using JsonDocument document = JsonDocument.Parse(value);
            JsonElement data = document.RootElement;
            if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                throw new InvalidOperationException("An erased record cannot measure a boundary.");
            string kind = Kind(key);
            string[] fields = kind switch
            {
                "constructed-metadata" => ["currentSequence", "eTag", "lastModified"],
                "http-post" => ["address", "began", "ended", "requestHash", "responseHash", "status"],
                "production-handler-discovery" => ["declared", "persisted", "registry", "supported", "unknown"],
                "production-handler-valid" or "production-handler-missing" or "production-handler-forged" or "production-handler-foreign-tenant" or "production-handler-foreign-actor"
                    => ["errorMessage", "invoker", "payload", "router", "success"],
                "production-manifest-command-result" => ["count", "deadLetterBookkeeping", "error", "message", "metadata", "ok", "sequence"],
                "actual-host-protected-contract-refusal" => ["actualType", "code", "message", "requestBody", "stage"],
                "cross-client-common" or "cross-client-trusted-replay" => ["action", "identities", "ok", "outcome", "supported"],
                "cross-client-fenced-valid" or "cross-client-trusted-valid" => ["action", "identities", "ok", "supported"],
                "cross-client-fenced-forged" or "cross-client-trusted-forged" => ["action", "actualType", "cause", "error", "identities", "message", "ok", "supported"],
                _ => [],
            };
            foreach (string field in fields)
                if (!data.TryGetProperty(field, out _)) throw new InvalidOperationException("A required measured field was erased: " + kind + "/" + field);
            if (kind == "runtime-profile")
            {
                if (Text(data, "runtime") != "1.18.4" || Text(data, "redisImage") != PublishedCluster.RedisImage
                    || !Text(data, "redisContainer").StartsWith(Text(data, "appId") + "-redis-", StringComparison.Ordinal))
                    throw new InvalidOperationException("The measured runtime ownership changed.");
                Hash(data, "componentHash"); Hash(data, "daprdHash");
                MatchBuild(data.GetProperty("host"), builds); MatchBuild(data.GetProperty("domain"), builds);
            }
            else if (kind == "consumer-build") MatchBuild(data, builds);
            else if (kind is "loaded-Host" or "loaded-Domain")
            {
                string suffix = key[kind.Length..];
                using var profile = JsonDocument.Parse(records["runtime-profile" + suffix]);
                ConsumerBuild build = MatchBuild(profile.RootElement.GetProperty(kind == "loaded-Host" ? "host" : "domain"), builds);
                ValidateIdentities(data, build);
            }
            else if (kind == "probe-command")
            {
                string suffix = key[kind.Length..];
                using var consumer = JsonDocument.Parse(records["consumer-build" + suffix]);
                ConsumerBuild build = MatchBuild(consumer.RootElement, builds);
                JsonElement command = data.GetProperty("Command");
                Hash(data, "OutputHash"); Hash(command, "InputHash");
                var expectedProbe = HistoricalMatrixScenarioContract.Probe(observation, HistoricalMatrixScenarioContract.Ordinal(key));
                DateTimeOffset started = data.GetProperty("Started").GetDateTimeOffset(), ended = data.GetProperty("Ended").GetDateTimeOffset();
                if (HistoricalMatrixScenarioContract.Key(build) != expectedProbe.Build || started < observation.Started || ended > observation.Ended
                    || ended < started || !distinctExecutions.Add((started, ended, build.Assembly)))
                    throw new InvalidOperationException("The probe lost its distinct case-contained consumer execution.");
                string[] arguments = command.GetProperty("Arguments").EnumerateArray().Select(arg => arg.GetString()!).ToArray();
                if (Text(command, "FileName") != "dotnet" || arguments.FirstOrDefault() != build.Assembly
                    || Text(command, "WorkingDirectory") != Path.GetDirectoryName(build.Project)
                    || data.GetProperty("ExitCode").GetInt32() != 0
                    || commands.Count(capture =>
                    {
                        JsonElement actual = capture.Record;
                        JsonElement invoked = actual.GetProperty("Command");
                        return capture.Operation == expectedProbe.Operation && capture.Kind == expectedProbe.Kind && capture.Serializer == expectedProbe.Serializer
                            && invoked.GetProperty("FileName").GetString() == "dotnet"
                            && invoked.GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()).SequenceEqual(arguments)
                            && actual.GetProperty("Started").GetDateTimeOffset() == data.GetProperty("Started").GetDateTimeOffset()
                            && actual.GetProperty("Ended").GetDateTimeOffset() == data.GetProperty("Ended").GetDateTimeOffset()
                            && actual.GetProperty("ExitCode").GetInt32() == 0 && !actual.GetProperty("OutputTruncated").GetBoolean()
                            && Text(actual, "OutputHash") == Text(data, "OutputHash") && Text(invoked, "InputHash") == Text(command, "InputHash");
                    }) != 1)
                    throw new InvalidOperationException("The probe observation has no unique actual matching completed operation.");
                var captured = commands.Single(capture => capture.Record.GetProperty("Started").GetDateTimeOffset() == started
                    && capture.Record.GetProperty("Ended").GetDateTimeOffset() == ended
                    && capture.Record.GetProperty("Command").GetProperty("Arguments")[0].GetString() == build.Assembly);
                HistoricalMatrixProbeContract.Validate(observation, records, build, captured);
            }
            else if (kind == "inventory")
            {
                string fixture = Text(data, "fixture");
                string plainKey = key.Split('#')[0];
                JsonElement hashes = data.GetProperty("hashes");
                if (plainKey != "inventory:" + fixture || !fixture.StartsWith("tenant-", StringComparison.Ordinal)
                    || !Profiles(records).Any(profile => Text(profile, "appId") == Text(data, "appId") && Text(profile, "redisContainer") == Text(data, "database"))
                    || !hashes.TryGetProperty(fixture + ":metadata", out _) || !hashes.TryGetProperty(fixture + ":snapshot", out _)
                    || !hashes.TryGetProperty(fixture + ":events:1", out _))
                    throw new InvalidOperationException("Persisted inventory lost its actor, tenant or owned database coordinates.");
                foreach (JsonProperty hash in hashes.EnumerateObject())
                    if (!hash.Name.StartsWith(fixture + ":", StringComparison.Ordinal)
                        || hash.Value.ValueKind != JsonValueKind.Null && !IsHash(hash.Value.GetString(), 64))
                        throw new InvalidOperationException("Persisted inventory hash coordinates changed.");
            }
            else if (kind == "http-get")
            {
                string address = key["http-get:".Length..].Split('#')[0];
                if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) || !uri.IsLoopback || uri.Scheme != "http")
                    throw new InvalidOperationException("The measured HTTP observation left the owned loopback topology.");
            }
            else if (kind == "http-post")
            {
                Hash(data, "requestHash"); Hash(data, "responseHash");
                if (!Uri.TryCreate(Text(data, "address"), UriKind.Absolute, out Uri? uri) || !uri.IsLoopback || uri.Scheme != "http"
                    || data.GetProperty("ended").GetDateTimeOffset() < data.GetProperty("began").GetDateTimeOffset())
                    throw new InvalidOperationException("The measured mutation lost its request, response or owned endpoint coordinates.");
            }
            else if (kind == "constructed-metadata")
            {
                if (data.GetProperty("currentSequence").GetInt64() != 12 || Text(data, "eTag") != "old"
                    || data.GetProperty("lastModified").GetDateTimeOffset() != DateTimeOffset.UnixEpoch)
                    throw new InvalidOperationException("The actual legacy constructor observation was erased.");
            }
            else if (kind == "provider-created-before-interruption")
            {
                if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
                    throw new InvalidOperationException("Interrupted cleanup lost its actual created container observation.");
                foreach (JsonElement container in data.EnumerateArray())
                {
                    Hash(container, "Id"); Text(container, "Name"); Text(container, "Image");
                    if (!Text(container.GetProperty("Config").GetProperty("Labels"), "p1r.owner").StartsWith("p1r-", StringComparison.Ordinal))
                        throw new InvalidOperationException("Interrupted cleanup lost its exact container owner label.");
                }
            }
            else if (kind == "provider-backup")
            {
                Hash(data, "hash");
                if (data.GetProperty("bytes").GetInt64() <= 0
                    || !Profiles(records).Any(profile => Text(profile, "redisContainer") == Text(data, "sourceContainer")))
                    throw new InvalidOperationException("Provider backup lost its owned source and actual bytes.");
            }
            else if (kind == "production-manifest-refusal")
            {
                JsonElement cause = data.GetProperty("cause");
                string actor = Text(cause, "actor");
                if (!data.GetProperty("observed").GetBoolean() || Text(cause, "code") != "missing-allow-listed-callable"
                    || Text(cause, "stage") != "production-logical-event-upcast-binding"
                    || Text(cause, "declaringType") != "Hexalith.EventStore.Client.Events.EventUpcastChainExecutor"
                    || Text(cause, "method") != "GetBinding"
                    || Text(cause, "message") != "CapabilityMismatch: an admitted edge lacks an allow-listed callable."
                    || !records.Keys.Any(name => name.Split('#')[0] == "inventory:" + actor))
                    throw new InvalidOperationException("The manifest refusal lost its actual cause or persisted actor.");
            }
            else if (kind == "actual-writer-admission-result")
            {
                _ = data.GetProperty("refused").GetBoolean(); Text(data, "code"); Text(data, "stage");
                foreach (string field in new[] { "protectionCalls", "allocations", "staging" }) _ = data.GetProperty(field).GetInt32();
            }
            else if (kind == "actual-versioned-reader-result") _ = data.GetProperty("ok").GetBoolean();
            else if (kind == "bounded-reminder-registration-restart-window")
            {
                foreach (string field in new[] { "preparationStarted", "preparationFinished", "due" }) _ = data.GetProperty(field).GetDateTimeOffset();
                if (data.GetProperty("preparationBudgetSeconds").GetDouble() <= 0 || data.GetProperty("naturalDeliveryMarginSeconds").GetDouble() <= 0)
                    throw new InvalidOperationException("The natural reminder lost its bounded observation window.");
            }
            else if (kind == "capability-security-observations")
            {
                if (!data.GetProperty("supported").GetBoolean()) throw new InvalidOperationException("A supported capability lost its evidence.");
                foreach (string refusal in new[] { "stale", "forged", "completedStale", "unauthorized", "forgedEffect" })
                {
                    JsonElement proof = data.GetProperty(refusal);
                    if (!proof.GetProperty("expected").GetBoolean()) throw new InvalidOperationException("The actual protected refusal was erased.");
                    Text(proof, "code"); Text(proof, "stage");
                    _ = proof.GetProperty("before"); _ = proof.GetProperty("after");
                }
            }
        }
    }

    private static ConsumerBuild MatchBuild(JsonElement element, IReadOnlyList<ConsumerBuild> builds)
    {
        string assembly = Text(element, "Assembly");
        ConsumerBuild? build = builds.SingleOrDefault(candidate => candidate.Assembly == assembly);
        if (build is null || !JsonElement.DeepEquals(element, JsonSerializer.SerializeToElement(build)))
            throw new InvalidOperationException("The measured consumer differs from the actual independently prepared build.");
        return build;
    }

    internal static void ValidateIdentities(JsonElement data, ConsumerBuild build)
    {
        string[] names = new[] { "Client", "Contracts", "DomainService", "Server", "ServiceDefaults" }
            .Concat(build.Role == "Host" && build.Version == "3.128.2" ? ["Gateway"] : []).Select(name => "Hexalith.EventStore." + name).Order(StringComparer.Ordinal).ToArray();
        JsonElement[] identities = data.GetProperty("identities").EnumerateArray().ToArray();
        if (!identities.Select(identity => Text(identity, "name")).Order(StringComparer.Ordinal).SequenceEqual(names))
            throw new InvalidOperationException("The loaded identity inventory was erased.");
        foreach (JsonElement identity in identities)
        {
            string name = Text(identity, "name"); Hash(identity, "sha256");
            string version = build.Source is null ? build.Version : build.ResolvedVersions![name];
            if (Text(identity, "configuration") != build.Configuration || Text(identity, "informationalVersion").Split('+')[0] != version
                || Text(identity, "path") != Path.Combine(Path.GetDirectoryName(build.Assembly)!, name + ".dll")
                || build.Source is null && (Text(identity, "sha256") != build.VerifiedAssemblyHashes![name]
                    || Text(identity, "informationalVersion").Split('+').ElementAtOrDefault(1) != build.ArchiveSources![name])
                || build.Source is not null && (build.PreparedAssemblyHashes is null
                    || !build.PreparedAssemblyHashes.TryGetValue(name, out string? prepared) || Text(identity, "sha256") != prepared))
                throw new InvalidOperationException("Loaded assembly proof lost its actual source, bytes or consumer coordinates.");
        }
    }

    private static IEnumerable<JsonElement> Profiles(IReadOnlyDictionary<string, string> records)
        => records.Where(pair => Kind(pair.Key) == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value));

    private static string Kind(string key) => key.StartsWith("inventory:", StringComparison.Ordinal) ? "inventory"
        : key.StartsWith("http-get:", StringComparison.Ordinal) ? "http-get" : key.Split('#')[0];

    private static string Text(JsonElement element, string field)
        => element.GetProperty(field).GetString() is { Length: > 0 } value ? value : throw new InvalidOperationException("Measured coordinate missing: " + field);

    private static bool IsHash(string? value, int length) => value?.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Hash(JsonElement element, string field)
    {
        if (!IsHash(Text(element, field), 64)) throw new InvalidOperationException("Measured hash missing: " + field);
    }

    private static Dictionary<(string, string), (int, Dictionary<string, int>)> CreateCases()
        => Frozen.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split('|')).ToDictionary(parts => (parts[0], parts[1]),
            parts => (int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), parts[3].Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(record => record.Split('=')).ToDictionary(record => record[0], record => int.Parse(record[1], System.Globalization.CultureInfo.InvariantCulture))));

    private const string Frozen = """
provenance|3.70.1-package-Host|36|consumer-build=1;probe-command=1
provenance|3.70.1-package-Domain|36|consumer-build=1;probe-command=1
provenance|3.70.1-package-Probe|36|consumer-build=1;probe-command=1
provenance|3.110.0-package-Host|36|consumer-build=1;probe-command=1
provenance|3.110.0-package-Domain|36|consumer-build=1;probe-command=1
provenance|3.110.0-package-Probe|36|consumer-build=1;probe-command=1
provenance|3.128.2-package-Host|43|consumer-build=1;probe-command=1
provenance|3.128.2-package-Domain|36|consumer-build=1;probe-command=1
provenance|3.128.2-package-Probe|36|consumer-build=1;probe-command=1
provenance|3.128.2-source-Host|25|consumer-build=1;probe-command=1
provenance|3.128.2-source-Domain|21|consumer-build=1;probe-command=1
provenance|3.128.2-source-Probe|21|consumer-build=1;probe-command=1
legacy-metadata|3.70.1-package-Probe-three-argument-constructor|4|constructed-metadata=1;consumer-build=1;probe-command=1
legacy-metadata|3.110.0-package-Probe-three-argument-constructor|4|constructed-metadata=1;consumer-build=1;probe-command=1
legacy-metadata|3.128.2-package-Probe-three-argument-constructor|4|constructed-metadata=1;consumer-build=1;probe-command=1
legacy-metadata|3.128.2-source-Probe-three-argument-constructor|4|constructed-metadata=1;consumer-build=1;probe-command=1
legacy-metadata|3.70.1-False|4|consumer-build=1;probe-command=1
legacy-metadata|3.70.1-True|4|consumer-build=1;probe-command=1
metadata-read|3.70.1-floor-1|2|consumer-build=1;probe-command=1
metadata-read|3.70.1-floor-5|2|consumer-build=1;probe-command=1
metadata-read|3.70.1-floor-0|2|consumer-build=1;probe-command=1
metadata-read|3.70.1-floor-14|2|consumer-build=1;probe-command=1
legacy-metadata|3.110.0-False|4|consumer-build=1;probe-command=1
legacy-metadata|3.110.0-True|4|consumer-build=1;probe-command=1
metadata-read|3.110.0-floor-1|2|consumer-build=1;probe-command=1
metadata-read|3.110.0-floor-5|2|consumer-build=1;probe-command=1
metadata-read|3.110.0-floor-0|2|consumer-build=1;probe-command=1
metadata-read|3.110.0-floor-14|2|consumer-build=1;probe-command=1
legacy-metadata|3.128.2-False|4|consumer-build=1;probe-command=1
legacy-metadata|3.128.2-True|4|consumer-build=1;probe-command=1
metadata-read|3.128.2-floor-1|2|consumer-build=1;probe-command=1
metadata-read|3.128.2-floor-5|2|consumer-build=1;probe-command=1
metadata-read|3.128.2-floor-0|2|consumer-build=1;probe-command=1
metadata-read|3.128.2-floor-14|2|consumer-build=1;probe-command=1
mixed-api|3.70.1-package-Probe-cursor|1|consumer-build=1;probe-command=1
mixed-api|3.110.0-package-Probe-cursor|10|consumer-build=1;probe-command=1
mixed-api|3.128.2-package-Probe-cursor|10|consumer-build=1;probe-command=1
mixed-api|3.128.2-source-Probe-cursor|10|consumer-build=1;probe-command=1
query-wire|real-signed-admission-and-refusals|6|consumer-build=1;probe-command=1
query-wire|3.70.1-package-Probe-to-3.110.0-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.70.1-package-Probe-to-3.110.0-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.70.1-package-Probe-to-3.110.0-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.70.1-package-Probe-to-3.110.0-package-Probe-xml|0|
query-wire|3.70.1-package-Probe-to-3.128.2-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.70.1-package-Probe-to-3.128.2-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.70.1-package-Probe-to-3.128.2-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.70.1-package-Probe-to-3.128.2-package-Probe-xml|0|
query-wire|3.70.1-package-Probe-to-3.128.2-source-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.70.1-package-Probe-to-3.128.2-source-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.70.1-package-Probe-to-3.128.2-source-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.70.1-package-Probe-to-3.128.2-source-Probe-xml|0|
query-wire|3.110.0-package-Probe-to-3.70.1-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.110.0-package-Probe-to-3.70.1-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.110.0-package-Probe-to-3.70.1-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.110.0-package-Probe-to-3.70.1-package-Probe-xml|0|
query-wire|3.110.0-package-Probe-to-3.128.2-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.110.0-package-Probe-to-3.128.2-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.110.0-package-Probe-to-3.128.2-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.110.0-package-Probe-to-3.128.2-package-Probe-xml|0|
query-wire|3.110.0-package-Probe-to-3.128.2-source-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.110.0-package-Probe-to-3.128.2-source-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.110.0-package-Probe-to-3.128.2-source-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.110.0-package-Probe-to-3.128.2-source-Probe-xml|0|
query-wire|3.128.2-package-Probe-to-3.70.1-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-package-Probe-to-3.70.1-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.128.2-package-Probe-to-3.70.1-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-package-Probe-to-3.70.1-package-Probe-xml|0|consumer-build=1;probe-command=1
query-wire|3.128.2-package-Probe-to-3.110.0-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-package-Probe-to-3.110.0-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.128.2-package-Probe-to-3.110.0-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-package-Probe-to-3.110.0-package-Probe-xml|0|consumer-build=1;probe-command=1
query-wire|3.128.2-package-Probe-to-3.128.2-source-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-package-Probe-to-3.128.2-source-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.128.2-package-Probe-to-3.128.2-source-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-package-Probe-to-3.128.2-source-Probe-xml|19|consumer-build=3;probe-command=3
query-wire|3.128.2-source-Probe-to-3.70.1-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-source-Probe-to-3.70.1-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.128.2-source-Probe-to-3.70.1-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-source-Probe-to-3.70.1-package-Probe-xml|0|consumer-build=1;probe-command=1
query-wire|3.128.2-source-Probe-to-3.110.0-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-source-Probe-to-3.110.0-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.128.2-source-Probe-to-3.110.0-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-source-Probe-to-3.110.0-package-Probe-xml|0|consumer-build=1;probe-command=1
query-wire|3.128.2-source-Probe-to-3.128.2-package-Probe-json|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-source-Probe-to-3.128.2-package-Probe-json|19|consumer-build=3;probe-command=3
query-wire|3.128.2-source-Probe-to-3.128.2-package-Probe-xml|19|consumer-build=3;probe-command=3
projection-wire|3.128.2-source-Probe-to-3.128.2-package-Probe-xml|19|consumer-build=3;probe-command=3
full-replay|3.70.1-to-3.128.2|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
snapshot-tail|3.70.1-to-3.128.2-snapshot-tail|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-covered|3.70.1-to-3.128.2-retained-covered|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.70.1-to-3.128.2-retained-uncovered|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.70.1-to-3.128.2-retained-uncovered-absent|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.70.1-to-3.128.2-missing-event|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.70.1-to-3.128.2-missing-event-tail|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.70.1-to-3.128.2-metadata-write|264|http-get=6;inventory=5;loaded-Domain=3;loaded-Host=3;runtime-profile=3
full-replay|3.128.2-to-3.70.1|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
snapshot-tail|3.128.2-to-3.70.1-snapshot-tail|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-covered|3.128.2-to-3.70.1-retained-covered|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.128.2-to-3.70.1-retained-uncovered|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.128.2-to-3.70.1-retained-uncovered-absent|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.128.2-to-3.70.1-missing-event|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.128.2-to-3.70.1-missing-event-tail|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.128.2-to-3.70.1-metadata-write|257|http-get=6;inventory=5;loaded-Domain=3;loaded-Host=3;runtime-profile=3
full-replay|3.110.0-to-3.128.2|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
snapshot-tail|3.110.0-to-3.128.2-snapshot-tail|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-covered|3.110.0-to-3.128.2-retained-covered|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.110.0-to-3.128.2-retained-uncovered|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.110.0-to-3.128.2-retained-uncovered-absent|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.110.0-to-3.128.2-missing-event|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.110.0-to-3.128.2-missing-event-tail|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.110.0-to-3.128.2-metadata-write|264|http-get=6;inventory=5;loaded-Domain=3;loaded-Host=3;runtime-profile=3
full-replay|3.128.2-to-3.110.0|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
snapshot-tail|3.128.2-to-3.110.0-snapshot-tail|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-covered|3.128.2-to-3.110.0-retained-covered|170|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.128.2-to-3.110.0-retained-uncovered|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.128.2-to-3.110.0-retained-uncovered-absent|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.128.2-to-3.110.0-missing-event|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.128.2-to-3.110.0-missing-event-tail|169|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.128.2-to-3.110.0-metadata-write|257|http-get=6;inventory=5;loaded-Domain=3;loaded-Host=3;runtime-profile=3
full-replay|tagged-source-to-3.128.2|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
snapshot-tail|tagged-source-to-3.128.2-snapshot-tail|144|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-covered|tagged-source-to-3.128.2-retained-covered|144|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|tagged-source-to-3.128.2-retained-uncovered|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|tagged-source-to-3.128.2-retained-uncovered-absent|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|tagged-source-to-3.128.2-missing-event|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|tagged-source-to-3.128.2-missing-event-tail|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|tagged-source-to-3.128.2-metadata-write|238|http-get=6;inventory=5;loaded-Domain=3;loaded-Host=3;runtime-profile=3
full-replay|3.128.2-to-tagged-source|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
snapshot-tail|3.128.2-to-tagged-source-snapshot-tail|144|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-covered|3.128.2-to-tagged-source-retained-covered|144|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.128.2-to-tagged-source-retained-uncovered|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
retained-uncovered|3.128.2-to-tagged-source-retained-uncovered-absent|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.128.2-to-tagged-source-missing-event|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
missing-event|3.128.2-to-tagged-source-missing-event-tail|143|http-get=4;inventory=4;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.128.2-to-tagged-source-metadata-write|205|http-get=6;inventory=5;loaded-Domain=3;loaded-Host=3;runtime-profile=3
invalid-evidence|3.70.1-Release-floor-0-tenant-a|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-floor-0-tenant-b|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-floor-14-tenant-a|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-floor-14-tenant-b|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-unreadable-tenant-a|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-unreadable-tenant-b|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-protected-mismatch-tenant-a|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-protected-mismatch-tenant-b|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-unknown-type-tenant-a|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-unknown-type-tenant-b|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-version-987-tenant-a|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-version-987-tenant-b|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-floor-0-tenant-a|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-floor-0-tenant-b|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-floor-14-tenant-a|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-floor-14-tenant-b|150|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-unreadable-tenant-a|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-unreadable-tenant-b|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-protected-mismatch-tenant-a|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-protected-mismatch-tenant-b|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-unknown-type-tenant-a|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-unknown-type-tenant-b|153|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-version-987-tenant-a|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-version-987-tenant-b|151|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-floor-0-tenant-a|164|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-floor-0-tenant-b|164|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-floor-14-tenant-a|164|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-floor-14-tenant-b|164|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-unreadable-tenant-a|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-unreadable-tenant-b|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-protected-mismatch-tenant-a|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-protected-mismatch-tenant-b|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-unknown-type-tenant-a|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-unknown-type-tenant-b|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-version-987-tenant-a|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-version-987-tenant-b|167|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-floor-0-tenant-a|98|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-floor-0-tenant-b|98|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-floor-14-tenant-a|98|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-floor-14-tenant-b|98|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-unreadable-tenant-a|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-unreadable-tenant-b|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-protected-mismatch-tenant-a|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-protected-mismatch-tenant-b|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-unknown-type-tenant-a|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-unknown-type-tenant-b|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-version-987-tenant-a|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-version-987-tenant-b|101|actual-versioned-reader-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.70.1-Release-head12-floor0|161|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.70.1-Release-head12-floor1|244|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.70.1-Release-head12-floor5|244|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.70.1-Release-head12-floor13|244|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
invalid-evidence|3.70.1-Release-head12-floor14|161|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.110.0-Release-head12-floor0|161|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.110.0-Release-head12-floor1|244|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.110.0-Release-head12-floor5|244|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.110.0-Release-head12-floor13|244|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
invalid-evidence|3.110.0-Release-head12-floor14|161|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Release-head12-floor0|175|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.128.2-Release-head12-floor1|265|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.128.2-Release-head12-floor5|265|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.128.2-Release-head12-floor13|265|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
invalid-evidence|3.128.2-Release-head12-floor14|175|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
invalid-evidence|3.128.2-Debug-head12-floor0|109|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
metadata-write|3.128.2-Debug-head12-floor1|166|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.128.2-Debug-head12-floor5|166|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
metadata-write|3.128.2-Debug-head12-floor13|166|http-get=6;inventory=2;loaded-Domain=3;loaded-Host=3;runtime-profile=3
invalid-evidence|3.128.2-Debug-head12-floor14|109|actual-writer-admission-result=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
post-upgrade-restore|3.70.1|268|http-get=6;inventory=8;loaded-Domain=3;loaded-Host=3;provider-backup=1;runtime-profile=3
pre-upgrade-restore|3.70.1|246|http-get=6;inventory=4;loaded-Domain=3;loaded-Host=3;provider-backup=1;runtime-profile=3
post-upgrade-restore|3.110.0|268|http-get=6;inventory=8;loaded-Domain=3;loaded-Host=3;provider-backup=1;runtime-profile=3
pre-upgrade-restore|3.110.0|246|http-get=6;inventory=4;loaded-Domain=3;loaded-Host=3;provider-backup=1;runtime-profile=3
post-upgrade-restore|3.128.2|282|http-get=6;inventory=8;loaded-Domain=3;loaded-Host=3;provider-backup=1;runtime-profile=3
post-upgrade-restore|tagged-source|216|http-get=6;inventory=8;loaded-Domain=3;loaded-Host=3;provider-backup=1;runtime-profile=3
mixed-api|3.70.1-Release-persisted-status-tristate|150|http-get=4;http-post=3;loaded-Domain=2;loaded-Host=2;runtime-profile=2
mixed-api|3.70.1-Release|75|http-get=2;http-post=1;inventory=2;loaded-Domain=1;loaded-Host=1;runtime-profile=1
mixed-api|3.110.0-Release-persisted-status-tristate|150|http-get=4;http-post=3;loaded-Domain=2;loaded-Host=2;runtime-profile=2
mixed-api|3.110.0-Release|164|capability-security-observations=1;http-get=4;http-post=1;inventory=3;loaded-Domain=2;loaded-Host=2;runtime-profile=2
mixed-api|3.128.2-Release-persisted-status-tristate|164|http-get=4;http-post=3;loaded-Domain=2;loaded-Host=2;runtime-profile=2
mixed-api|3.128.2-Release|178|capability-security-observations=1;http-get=4;http-post=1;inventory=3;loaded-Domain=2;loaded-Host=2;runtime-profile=2
query-wire|Release-production-declared-handler-discovery-transport|109|http-get=2;http-post=7;loaded-Domain=1;loaded-Host=1;production-handler-discovery=1;production-handler-foreign-actor=1;production-handler-foreign-tenant=1;production-handler-forged=1;production-handler-missing=1;production-handler-valid=1;runtime-profile=1
query-wire|Release-supported-handler-and-actor-routes|111|http-get=2;http-post=12;loaded-Domain=1;loaded-Host=1;runtime-profile=1
projection-wire|Release-durably-applied-watermark-cursor|167|http-get=4;http-post=11;loaded-Domain=2;loaded-Host=2;runtime-profile=2
registered-evolution|Release-registered-manifest-production-hop|165|http-get=4;inventory=2;loaded-Domain=2;loaded-Host=2;production-manifest-command-result=1;production-manifest-refusal=1;runtime-profile=2
registered-evolution|Release|84|http-get=2;http-post=1;inventory=2;loaded-Domain=1;loaded-Host=1;runtime-profile=1
natural-reminder|Release|165|bounded-reminder-registration-restart-window=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
mixed-api|3.128.2-Debug-persisted-status-tristate|98|http-get=4;http-post=3;loaded-Domain=2;loaded-Host=2;runtime-profile=2
mixed-api|3.128.2-Debug|112|capability-security-observations=1;http-get=4;http-post=1;inventory=3;loaded-Domain=2;loaded-Host=2;runtime-profile=2
query-wire|Debug-production-declared-handler-discovery-transport|76|http-get=2;http-post=7;loaded-Domain=1;loaded-Host=1;production-handler-discovery=1;production-handler-foreign-actor=1;production-handler-foreign-tenant=1;production-handler-forged=1;production-handler-missing=1;production-handler-valid=1;runtime-profile=1
query-wire|Debug-supported-handler-and-actor-routes|78|http-get=2;http-post=12;loaded-Domain=1;loaded-Host=1;runtime-profile=1
projection-wire|Debug-durably-applied-watermark-cursor|101|http-get=4;http-post=11;loaded-Domain=2;loaded-Host=2;runtime-profile=2
registered-evolution|Debug-registered-manifest-production-hop|99|http-get=4;inventory=2;loaded-Domain=2;loaded-Host=2;production-manifest-command-result=1;production-manifest-refusal=1;runtime-profile=2
registered-evolution|Debug|51|http-get=2;http-post=1;inventory=2;loaded-Domain=1;loaded-Host=1;runtime-profile=1
natural-reminder|Debug|99|bounded-reminder-registration-restart-window=1;http-get=4;http-post=1;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
mixed-api|client-3.70.1-Release-to-host-3.128.2-Release|273|consumer-build=3;cross-client-common=1;cross-client-fenced-valid=1;cross-client-trusted-valid=1;http-get=4;http-post=1;inventory=3;loaded-Domain=2;loaded-Host=2;probe-command=3;runtime-profile=2
mixed-api|client-3.128.2-Release-to-host-3.70.1-Release|259|actual-host-protected-contract-refusal=2;consumer-build=3;cross-client-common=1;cross-client-fenced-valid=1;cross-client-trusted-valid=1;http-get=4;http-post=1;inventory=3;loaded-Domain=2;loaded-Host=2;probe-command=3;runtime-profile=2
mixed-api|client-3.110.0-Release-to-host-3.128.2-Release|389|consumer-build=6;cross-client-common=1;cross-client-fenced-forged=1;cross-client-fenced-valid=1;cross-client-trusted-forged=1;cross-client-trusted-replay=1;cross-client-trusted-valid=1;http-get=4;http-post=1;inventory=13;loaded-Domain=2;loaded-Host=2;probe-command=6;runtime-profile=2
mixed-api|client-3.128.2-Release-to-host-3.110.0-Release|375|consumer-build=6;cross-client-common=1;cross-client-fenced-forged=1;cross-client-fenced-valid=1;cross-client-trusted-forged=1;cross-client-trusted-replay=1;cross-client-trusted-valid=1;http-get=4;http-post=1;inventory=13;loaded-Domain=2;loaded-Host=2;probe-command=6;runtime-profile=2
mixed-api|client-3.128.2-Release-to-host-3.128.2-Debug|323|consumer-build=6;cross-client-common=1;cross-client-fenced-forged=1;cross-client-fenced-valid=1;cross-client-trusted-forged=1;cross-client-trusted-replay=1;cross-client-trusted-valid=1;http-get=4;http-post=1;inventory=13;loaded-Domain=2;loaded-Host=2;probe-command=6;runtime-profile=2
mixed-api|client-3.128.2-Debug-to-host-3.128.2-Release|299|consumer-build=6;cross-client-common=1;cross-client-fenced-forged=1;cross-client-fenced-valid=1;cross-client-trusted-forged=1;cross-client-trusted-replay=1;cross-client-trusted-valid=1;http-get=4;http-post=1;inventory=13;loaded-Domain=2;loaded-Host=2;probe-command=6;runtime-profile=2
checkout|package-to-exact-tag-source|139|http-get=4;inventory=2;loaded-Domain=2;loaded-Host=2;runtime-profile=2
failure-cleanup|timeout-and-detached-descendant|7|provider-created-before-interruption=1
failure-cleanup|cancellation-and-startup-failure|7|provider-created-before-interruption=1
failure-cleanup|matrix-cleanup-twice|2|
""";
}
