using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds actual provider-dump byte observations to the owned SAVE/copy/restore/destruction command sequence.</summary>
internal static class HistoricalMatrixBackupContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records,
        IReadOnlyList<HistoricalExecutionCommand> commands, IReadOnlyList<QualificationFileFingerprint> files)
    {
        if (!records.TryGetValue("provider-backup", out string? raw)) return;
        var backup = JsonSerializer.Deserialize<JsonElement>(raw);
        string path = backup.GetProperty("path").GetString()!, source = backup.GetProperty("sourceContainer").GetString()!;
        string root = Path.GetDirectoryName(path)!;
        var profiles = records.Where(pair => pair.Key.Split('#')[0] == "runtime-profile")
            .Select(pair => JsonSerializer.Deserialize<JsonElement>(pair.Value)).ToArray();
        string owner = profiles[0].GetProperty("appId").GetString()!;
        string name = Path.GetFileName(path);
        if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path) != path || Path.GetFileName(root) != owner
            || name.Length != 43 || !name.StartsWith("backup-", StringComparison.Ordinal) || !name.EndsWith(".rdb", StringComparison.Ordinal)
            || !Guid.TryParseExact(name[7..^4], "N", out _) || !profiles.Any(profile => profile.GetProperty("redisContainer").GetString() == source))
            throw new InvalidOperationException("The actual backup lost its exact owner-private path or source backend.");
        var actual = files.Where(file => file.Path == path).ToArray();
        string[] purposes = ["provider-backup-copied", "provider-backup-before-restore", "provider-backup-destroyed"];
        if (actual.Length != 3 || !actual.Select(file => file.Purpose).SequenceEqual(purposes)
            || actual.Any(file => file.Owner != owner || file.Started < observation.Started || file.Ended > observation.Ended || file.Ended < file.Started)
            || !actual[0].Exists || !actual[1].Exists || actual[2].Exists || actual[2].Bytes != 0 || actual[2].Sha256 is not null
            || actual[0].Bytes <= 0 || actual[0].Bytes != actual[1].Bytes || actual[0].Sha256 != actual[1].Sha256
            || actual[0].Sha256 is not { Length: 64 } digest || !digest.All(char.IsAsciiHexDigitLower)
            || backup.GetProperty("hash").GetString() != digest || backup.GetProperty("bytes").GetInt64() != actual[0].Bytes)
            throw new InvalidOperationException("The backup measurement differs from actual independently read dump bytes or destruction.");
        var scoped = commands.Select(command => command.Record).Where(command => command.GetProperty("Started").GetDateTimeOffset() >= observation.Started
            && command.GetProperty("Ended").GetDateTimeOffset() <= observation.Ended
            && command.GetProperty("ExitCode").GetInt32() == 0 && !command.GetProperty("OutputTruncated").GetBoolean()
            && command.GetProperty("Command").GetProperty("FileName").GetString() == "docker"
            && command.GetProperty("Command").GetProperty("WorkingDirectory").GetString() == root).ToArray();
        string[] Arguments(JsonElement command) => command.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()!).ToArray();
        JsonElement One(Func<string[], bool> predicate)
        {
            var matches = scoped.Where(command => predicate(Arguments(command))).ToArray();
            return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("The actual backup lost its unique owned command chain.");
        }
        var save = One(args => args.SequenceEqual(new[] { "exec", source, "redis-cli", "SAVE" }));
        var copy = One(args => args.SequenceEqual(new[] { "cp", source + ":/data/dump.rdb", path }));
        var restore = One(args => args.Length == 3 && args[0] == "cp" && args[1] == path && args[2] != source + ":/data/dump.rdb"
            && profiles.Any(profile => args[2] == profile.GetProperty("redisContainer").GetString() + ":/data/dump.rdb"));
        string target = Arguments(restore)[2][..^":/data/dump.rdb".Length];
        var targetProfile = profiles.First(profile => profile.GetProperty("redisContainer").GetString() == target);
        var fresh = HistoricalOwnedBackendContract.Validate(targetProfile, commands);
        if (target == source || fresh.Volume.GetProperty("Started").GetDateTimeOffset() < observation.Started
            || fresh.Create.GetProperty("Ended").GetDateTimeOffset() > actual[1].Started
            || restore.GetProperty("Ended").GetDateTimeOffset() > fresh.Start.GetProperty("Started").GetDateTimeOffset())
            throw new InvalidOperationException("The restore target is not genuinely fresh owned volume/create/copy-before-start state.");
        if (save.GetProperty("StandardOutput").GetString()!.Trim() != "OK"
            || save.GetProperty("Ended").GetDateTimeOffset() > copy.GetProperty("Started").GetDateTimeOffset()
            || copy.GetProperty("Ended").GetDateTimeOffset() > actual[0].Started
            || actual[0].Ended > actual[1].Started || actual[1].Ended > restore.GetProperty("Started").GetDateTimeOffset()
            || restore.GetProperty("Ended").GetDateTimeOffset() > actual[2].Started)
            throw new InvalidOperationException("The real provider backup/copy/restore/destruction observations are out of order.");
    }
}
