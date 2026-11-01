using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds scenario claims to independently prepared files, actual launches and actual state reads.</summary>
internal static class HistoricalMatrixRuntimeInputContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyList<HistoricalExecutionCommand> commands,
        IReadOnlyList<QualificationScenarioInput> inputs, IReadOnlyList<QualificationStateRead> reads, IReadOnlyList<QualificationDaprLaunch> launches, HistoricalTransportIndex exchanges)
    {
        var records = observation.Measurements ?? new Dictionary<string, string>();
        var profiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile")
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        foreach (var profile in profiles)
        {
            _ = HistoricalOwnedBackendContract.Validate(profile, commands);
            string id = Text(profile, "profileId"), owner = Text(profile, "owner"), root = Text(profile, "root");
            DateTimeOffset at = profile.GetProperty("observedAt").GetDateTimeOffset();
            if (owner != Text(profile, "appId") || Path.GetFileName(root) != owner || !Path.IsPathFullyQualified(root)
                || !Guid.TryParseExact(id, "N", out _) || at < observation.Started || at > observation.Ended)
                throw new InvalidOperationException("The actual runtime input lost its exact owner/profile/time.");
            foreach (string kind in new[] { "daprd", "component" })
            {
                string path = Path.Combine(root, kind == "daprd" ? "dapr-1.18.4/daprd" : "components/state.yaml");
                var captured = inputs.Where(input => input.Profile == id && input.Kind == kind).ToArray();
                if (captured.Length != 1 || captured[0].Owner != owner || captured[0].Root != root || captured[0].Path != path
                    || captured[0].Path != Text(profile, kind == "daprd" ? "daprdPath" : "componentPath")
                    || captured[0].Observed > at || captured[0].Bytes <= 0 || captured[0].Sha256 != Text(profile, kind + "Hash"))
                    throw new InvalidOperationException("The runtime profile differs from its independently prepared " + kind + " bytes.");
                if (kind == "component")
                {
                    var binding = captured[0].Component;
                    if (binding is null || binding.RedisAddress != Text(profile, "redisAddress") || binding.Name != "statestore"
                        || binding.Type != "state.redis" || binding.Version != "v1" || !binding.ActorStateStore || binding.KeyPrefix != "appid")
                        throw new InvalidOperationException("The prepared component differs from its actual owned backend semantics.");
                    var creates = commands.Select(command => command.Record).Where(command => Text(command.GetProperty("Command"), "FileName") == "docker"
                        && Text(command.GetProperty("Command"), "WorkingDirectory") == root && command.GetProperty("ExitCode").GetInt32() == 0
                        && command.GetProperty("Ended").GetDateTimeOffset() <= at).Where(command =>
                        command.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(item => item.GetString()!).ToArray() is var args
                        && args.SequenceEqual(new[] { "create", "--name", Text(profile, "redisContainer"), "--label", "p1r.owner=" + owner,
                            "--mount", "type=volume,source=" + Text(profile, "redisVolume") + ",destination=/data", "-p", binding.RedisAddress + ":6379",
                            PublishedCluster.RedisImage, "redis-server", "--appendonly", "no" })).ToArray();
                    if (creates.Length != 1) throw new InvalidOperationException("The prepared component has no unique actual owned backend launch binding.");
                }
                if (kind == "daprd" && !commands.Any(command => Text(command.Record.GetProperty("Command"), "FileName") == path
                    && command.Record.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()).SequenceEqual(new[] { "--version" })
                    && command.Record.GetProperty("ExitCode").GetInt32() == 0 && !command.Record.GetProperty("OutputTruncated").GetBoolean()
                    && Text(command.Record, "StandardOutput").Trim() == "1.18.4" && command.Record.GetProperty("Ended").GetDateTimeOffset() <= at))
                    throw new InvalidOperationException("The prepared daprd input has no actual pinned version execution.");
            }
            var actual = launches.Where(launch => launch.Profile == id).ToArray();
            bool selected = Text(profile.GetProperty("host"), "Version") == ConsumerMaterializer.SelectedVersion;
            string[] roles = selected ? ["Host", "Domain"] : ["Host"];
            if (actual.Length != roles.Length || actual.Select(launch => launch.Pid).Distinct().Count() != actual.Length)
                throw new InvalidOperationException("The runtime requires every actual primary and selected Domain sidecar.");
            foreach (string role in roles)
            {
                var matches = actual.Where(launch => launch.Role == role).ToArray();
                string prefix = role == "Host" ? "sidecar" : "domainSidecar";
                string address = role == "Host" ? "hostAddress" : "domainAddress";
                if (matches.Length != 1) throw new InvalidOperationException("An actual sidecar role is missing or duplicated.");
                var launch = matches[0];
                if (launch.Owner != owner || launch.Pid <= 0 || launch.Pid != profile.GetProperty(prefix + "ProcessId").GetInt32()
                    || launch.Executable != Text(profile, "daprdPath") || launch.Components != Path.Combine(root, "components")
                    || launch.AppId != Text(profile, "appId") + (role == "Domain" ? "-domain" : "")
                    || launch.HostPort != new Uri(Text(profile, address)).Port
                    || Text(profile, prefix + "Address") != "http://127.0.0.1:" + launch.SidecarPort
                    || launch.Started > launch.Observed || launch.Observed > at || launch.Ended is null || launch.Ended < at)
                    throw new InvalidOperationException("The prepared runtime input differs from its actual " + (role == "Host" ? "primary" : "Domain") + " sidecar launch/lifetime.");
                var addresses = new[] { Text(profile, "hostAddress"), Text(profile, "domainAddress"), Text(profile, "sidecarAddress") };
                foreach (var record in records.Where(pair => pair.Key.Split('#')[0] is "http-post" or "fixture-http-post" or "http-get-observation"))
                {
                    var operation = JsonSerializer.Deserialize<JsonElement>(record.Value);
                    DateTimeOffset began = operation.GetProperty("began").GetDateTimeOffset(), ended = operation.GetProperty("ended").GetDateTimeOffset();
                    if (began >= at && addresses.Any(endpoint => Text(operation, "address").StartsWith(endpoint + "/", StringComparison.Ordinal))
                        && (began < launch.Started || ended > launch.Ended))
                        throw new InvalidOperationException("An actual runtime operation exceeds its owning sidecar lifetime.");
                }
            }
        }
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records.Where(pair => pair.Key.StartsWith("inventory:", StringComparison.Ordinal)))
        {
            var inventory = JsonSerializer.Deserialize<JsonElement>(record.Value);
            string actor = Text(inventory, "fixture"), id = Text(inventory, "profileId");
            var profile = profiles.Single(profile => Text(profile, "profileId") == id);
            var hashes = inventory.GetProperty("hashes").EnumerateObject().ToArray();
            var ids = inventory.GetProperty("readIds").EnumerateArray().Select(value => value.GetString()).ToArray();
            if (ids.Length != hashes.Length) throw new InvalidOperationException("Every inventory value requires its actual state read.");
            for (int i = 0; i < ids.Length; i++)
            {
                var matches = reads.Where(read => read.Id == ids[i]).ToArray();
                if (matches.Length != 1 || !used.Add(matches[0].Id)) throw new InvalidOperationException("An inventory read is absent, copied or duplicated.");
                var read = matches[0];
                string address = Text(profile, "hostAddress") + "/read/" + Uri.EscapeDataString(actor) + "/" + Uri.EscapeDataString(hashes[i].Name);
                string? value = hashes[i].Value.ValueKind == JsonValueKind.Null ? null : hashes[i].Value.GetString();
                var transport = exchanges.Within(read.Address, read.Started, read.Ended).Where(exchange => exchange.Method == "GET" && exchange.Status == read.Status && exchange.ResponseSha256 == read.BodySha256).ToArray();
                if (transport.Length != 1) throw new InvalidOperationException("The actual state read lacks its independent GET transport receipt.");
                if (read.Profile != id || read.Owner != Text(profile, "owner") || read.AppId != Text(inventory, "appId") || read.AppId != Text(profile, "appId")
                    || read.Backend != Text(inventory, "database") || read.Backend != Text(profile, "redisContainer") || read.Actor != actor || read.Key != hashes[i].Name
                    || read.Address != address || read.HostPid != profile.GetProperty("hostProcessId").GetInt32() || read.Started < profile.GetProperty("observedAt").GetDateTimeOffset()
                    || read.Started < observation.Started || read.Ended > observation.Ended || read.Ended < read.Started
                    || read.ValueSha256 != value || (value is null ? read.Status != 404 || read.Bytes != 0 || read.BodySha256 != QualificationRuntime.Hash([])
                        : read.Status != 200 || read.Bytes <= 0 || read.BodySha256 != value))
                    throw new InvalidOperationException("The inventory value differs from its actual owned state-read target/status/bytes.");
            }
        }
    }

    private static string Text(JsonElement value, string field) => value.GetProperty(field).GetString()
        ?? throw new InvalidOperationException("An actual runtime input coordinate is absent.");
}
