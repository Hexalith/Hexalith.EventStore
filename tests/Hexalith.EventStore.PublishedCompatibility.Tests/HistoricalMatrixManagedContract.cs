using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds actual Host/Domain/Probe output identities and lifetimes to the operations they produce.</summary>
internal static class HistoricalMatrixManagedContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyList<ConsumerBuild> builds,
        IReadOnlyList<HistoricalExecutionCommand> commands, IReadOnlyList<QualificationManagedLaunch> launches,
        IReadOnlyList<QualificationHttpExchange> exchanges, IReadOnlyList<QualificationStateRead> stateReads)
    {
        var records = observation.Measurements ?? new Dictionary<string, string>();
        var profiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile").Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        foreach (var profile in profiles)
        foreach (string role in new[] { "host", "domain" })
        {
            var build = builds.Single(build => build.Assembly == profile.GetProperty(role).GetProperty("Assembly").GetString());
            var actual = launches.Where(launch => launch.Profile == Text(profile, "profileId") && launch.Role.Equals(role, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (actual.Length != 1 || actual[0].Owner != Text(profile, "owner") || actual[0].Root != Text(profile, "root")
                || actual[0].Pid != profile.GetProperty(role + "ProcessId").GetInt32() || actual[0].Address != Text(profile, role + "Address")
                || actual[0].Assembly != build.Assembly || actual[0].Started > profile.GetProperty("observedAt").GetDateTimeOffset())
                throw new InvalidOperationException("The runtime profile differs from its actual managed consumer launch.");
            RequireOutputs(actual[0], build);
        }
        var operations = records.Where(record => record.Key.Split('#')[0] is "http-post" or "fixture-http-post" or "http-get-observation")
            .Select(record => JsonSerializer.Deserialize<JsonElement>(record.Value)).Select(value => (Address: Text(value, "address"),
                Began: value.GetProperty("began").GetDateTimeOffset(), Ended: value.GetProperty("ended").GetDateTimeOffset())).ToList();
        var ids = records.Where(record => record.Key.StartsWith("inventory:", StringComparison.Ordinal))
            .SelectMany(record => JsonSerializer.Deserialize<JsonElement>(record.Value).GetProperty("readIds").EnumerateArray().Select(id => id.GetString()!)).ToHashSet(StringComparer.Ordinal);
        operations.AddRange(stateReads.Where(read => ids.Contains(read.Id)).Select(read => (read.Address, read.Started, read.Ended)));
        foreach (var exchange in exchanges.Where(exchange => exchange.Started >= observation.Started && exchange.Ended <= observation.Ended
            && operations.Any(operation => operation.Address == exchange.Address && exchange.Started >= operation.Began && exchange.Ended <= operation.Ended)))
        {
            var uri = new Uri(exchange.Address);
            var candidates = launches.Where(launch => launch.Address == uri.GetLeftPart(UriPartial.Authority)
                && profiles.Any(profile => Text(profile, "profileId") == launch.Profile)).ToArray();
            if (candidates.Length != 1 || candidates[0].Ended is null || exchange.Started < candidates[0].Started || exchange.Ended > candidates[0].Ended)
                throw new InvalidOperationException("The actual HTTP operation exceeds its owning generated consumer lifetime.");
        }
        foreach (var command in commands.Where(command => command.Record.GetProperty("Started").GetDateTimeOffset() >= observation.Started
            && command.Record.GetProperty("Ended").GetDateTimeOffset() <= observation.Ended))
        {
            var args = command.Record.GetProperty("Command").GetProperty("Arguments");
            if (command.Record.GetProperty("Command").GetProperty("FileName").GetString() != "dotnet" || args.GetArrayLength() == 0) continue;
            var build = builds.SingleOrDefault(build => build.Assembly == args[0].GetString());
            if (build is null) continue;
            DateTimeOffset began = command.Record.GetProperty("Started").GetDateTimeOffset(), ended = command.Record.GetProperty("Ended").GetDateTimeOffset();
            var actual = launches.Where(launch => launch.Assembly == build.Assembly && launch.Profile is null && launch.Started >= began && launch.Ended <= ended).ToArray();
            if (actual.Length != 1 || actual[0].Pid <= 0 || actual[0].Ended is null || actual[0].Ended < actual[0].Started) throw new InvalidOperationException("The generated probe lacks its actual bounded process lifetime.");
            RequireOutputs(actual[0], build);
            if (command.CrossClient is not null)
            {
                var profile = profiles.Single(profile => Text(profile, "sidecarAddress") == command.CrossClient.Sidecar);
                var host = launches.Single(launch => launch.Profile == Text(profile, "profileId") && launch.Role == "Host");
                if (began < host.Started || ended > host.Ended) throw new InvalidOperationException("The cross-client operation exceeds its actual Host lifetime.");
            }
        }
    }

    private static void RequireOutputs(QualificationManagedLaunch launch, ConsumerBuild build)
    {
        string[] keys = [build.Role + ".dll", build.Role + ".deps.json", build.Role + ".runtimeconfig.json"];
        if (build.PreparedExecutableHashes is null || build.PreparedExecutableHashes.Values.Any(hash => hash.Length != 64 || !hash.All(char.IsAsciiHexDigitLower)) || !keys.Order(StringComparer.Ordinal).SequenceEqual(build.PreparedExecutableHashes.Keys.Order(StringComparer.Ordinal))
            || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(launch.ExecutableHashes), JsonSerializer.SerializeToElement(build.PreparedExecutableHashes)))
            throw new InvalidOperationException("The actual generated consumer differs from its independently prepared executable identity.");
    }
    private static string Text(JsonElement value, string field) => value.GetProperty(field).GetString() ?? throw new InvalidOperationException("An actual managed consumer coordinate is absent.");
}
