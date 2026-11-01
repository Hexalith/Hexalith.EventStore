using System.Globalization;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds each protected refusal to its actual contemporaneous state capture and accepted-write boundaries.</summary>
internal static class HistoricalMatrixSecurityContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records,
        IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        if (!records.TryGetValue("capability-security-observations", out string? raw)) return;
        if (!records.TryGetValue("capability-security-capture", out string? captured))
            throw new InvalidOperationException("The protected operations require contemporaneous actual consumer state; old captures do not supply it.");
        var data = JsonSerializer.Deserialize<JsonElement>(raw); var capture = JsonSerializer.Deserialize<JsonElement>(captured);
        string owner = Text(capture, "owner"), path = Text(capture, "path"); int pid = capture.GetProperty("pid").GetInt32();
        string root = Path.GetDirectoryName(path)!;
        if (pid <= 0 || !owner.StartsWith("p1r-", StringComparison.Ordinal) || !Guid.TryParseExact(owner[4..], "N", out _)
            || !Path.IsPathFullyQualified(path) || Path.GetFullPath(path) != path || Path.GetFileName(root) != owner
            || !Path.GetFileName(path).StartsWith("security-inventory-", StringComparison.Ordinal)
            || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(path)["security-inventory-".Length..], "N", out _))
            throw new InvalidOperationException("The protected state capture lost its exact actual process/owner path.");
        var matchingProfiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile")
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value))
            .Where(profile => Text(profile, "profileId") == Text(capture, "profileId") && profile.GetProperty("hostProcessId").GetInt32() == pid).ToArray();
        if (matchingProfiles.Length != 1) throw new InvalidOperationException("The protected state PID/owner/root differs from the actual runtime Host PID/owner/root; no unique contemporaneous Host profile exists.");
        var profile = matchingProfiles[0];
        if (pid != profile.GetProperty("hostProcessId").GetInt32() || owner != Text(profile, "owner") || root != Text(profile, "root"))
            throw new InvalidOperationException("The protected state PID/owner/root differs from the actual runtime Host PID/owner/root.");
        var snapshots = capture.GetProperty("snapshots");
        string[] args = ["-c", HistoricalSecurityCapture.ReadScript, path, owner, pid.ToString(CultureInfo.InvariantCulture)];
        var reads = commands.Select(command => command.Record).Where(command =>
            Text(command.GetProperty("Command"), "FileName") == "python3" && Text(command.GetProperty("Command"), "WorkingDirectory") == root
            && command.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()).SequenceEqual(args)
            && command.GetProperty("ExitCode").GetInt32() == 0 && !command.GetProperty("OutputTruncated").GetBoolean()
            && command.GetProperty("Started").GetDateTimeOffset() >= observation.Started
            && command.GetProperty("Ended").GetDateTimeOffset() <= observation.Ended
            && JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(Text(command, "StandardOutput")), snapshots)).ToArray();
        if (reads.Length != 1 || snapshots.ValueKind != JsonValueKind.Array || snapshots.GetArrayLength() != 10)
            throw new InvalidOperationException("The protected snapshots have no unique actual bounded private-file read.");
        string[] fields = ["stale", "forged", "completedStale", "unauthorized", "forgedEffect"];
        string[] codes = ["stale-execution-authority", "invalid-execution-fence", "completed-execution-authority", "unauthorized-effect-delegation", "invalid-effect-gateway-proof"];
        DateTimeOffset previous = observation.Started!.Value;
        string actor = data.GetProperty("stale").GetProperty("before").EnumerateObject().Single(property => property.Name.EndsWith(":metadata", StringComparison.Ordinal)).Name[..^":metadata".Length];
        var inventories = records.Where(pair => pair.Key.StartsWith("inventory:" + actor, StringComparison.Ordinal))
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value).GetProperty("hashes")).ToArray();
        if (inventories.Length < 2) throw new InvalidOperationException("Protected state requires independent initial and committed inventories.");
        for (int i = 0; i < snapshots.GetArrayLength(); i++)
        {
            var snapshot = snapshots[i]; string phase = i % 2 == 0 ? "before" : "after";
            DateTimeOffset began = snapshot.GetProperty("began").GetDateTimeOffset(), ended = snapshot.GetProperty("ended").GetDateTimeOffset();
            if (Text(snapshot, "owner") != owner || snapshot.GetProperty("pid").GetInt32() != pid
                || Text(snapshot, "code") != codes[i / 2] || Text(snapshot, "phase") != phase || began < previous || ended < began
                || ended > reads[0].GetProperty("Started").GetDateTimeOffset()
                || !JsonElement.DeepEquals(snapshot.GetProperty("hashes"), data.GetProperty(fields[i / 2]).GetProperty(phase)))
                throw new InvalidOperationException("A protected before/after value differs from its actual ordered consumer read.");
            previous = ended;
        }
        var initial = data.GetProperty("stale").GetProperty("before");
        var accepted = data.GetProperty("completedStale").GetProperty("before");
        if (!JsonElement.DeepEquals(initial, inventories[0]) || !JsonElement.DeepEquals(initial, data.GetProperty("forged").GetProperty("before"))
            || !JsonElement.DeepEquals(accepted, data.GetProperty("unauthorized").GetProperty("before"))
            || !JsonElement.DeepEquals(accepted, data.GetProperty("forgedEffect").GetProperty("before"))
            || JsonElement.DeepEquals(initial, accepted) || accepted.GetProperty(actor + ":events:2").ValueKind != JsonValueKind.String
            || accepted.GetProperty(actor + ":events:3").ValueKind != JsonValueKind.Null
            || !JsonElement.DeepEquals(initial.GetProperty(actor + ":events:1"), accepted.GetProperty(actor + ":events:1"))
            || !JsonElement.DeepEquals(accepted.GetProperty(actor + ":events:2"), inventories[1].GetProperty(actor + ":events:2"))
            || inventories[1].GetProperty(actor + ":events:3").ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("The protected snapshots lost real accepted-write progression or unchanged original event bytes.");
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new InvalidOperationException("An actual protected state coordinate is absent.");
}
