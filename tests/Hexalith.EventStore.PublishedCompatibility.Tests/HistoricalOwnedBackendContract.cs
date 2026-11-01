using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds an actual owned Redis endpoint to its unique volume/create/start/readiness commands.</summary>
internal static class HistoricalOwnedBackendContract
{
    internal static (JsonElement Volume, JsonElement Create, JsonElement Start, JsonElement Ready) Validate(JsonElement profile,
        IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        string owner = Text(profile, "owner"), root = Text(profile, "root"), target = Text(profile, "redisContainer"), volume = Text(profile, "redisVolume");
        if (!target.StartsWith(owner + "-redis-", StringComparison.Ordinal) || !volume.StartsWith(owner + "-redis-data-", StringComparison.Ordinal))
            throw new InvalidOperationException("The actual backend lost its finite owned container/volume names.");
        var actual = commands.Select(command => command.Record).Where(command => Text(command.GetProperty("Command"), "FileName") == "docker"
            && Text(command.GetProperty("Command"), "WorkingDirectory") == root && command.GetProperty("ExitCode").GetInt32() == 0
            && !command.GetProperty("OutputTruncated").GetBoolean()).ToArray();
        JsonElement One(string[] args)
        {
            var rows = actual.Where(command => command.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()).SequenceEqual(args)).ToArray();
            return rows.Length == 1 ? rows[0] : throw new InvalidOperationException("The actual backend lost its unique fresh target command sequence.");
        }
        var v = One(["volume", "create", "--label", "p1r.owner=" + owner, volume]);
        var c = One(["create", "--name", target, "--label", "p1r.owner=" + owner, "--mount", "type=volume,source=" + volume + ",destination=/data",
            "-p", Text(profile, "redisAddress") + ":6379", PublishedCluster.RedisImage, "redis-server", "--appendonly", "no"]);
        var s = One(["start", target]);
        var p = actual.Where(command => command.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(value => value.GetString())
            .SequenceEqual(new[] { "exec", target, "redis-cli", "PING" }) && Text(command, "StandardOutput").Trim() == "PONG").ToArray();
        string id = Text(c, "StandardOutput").Trim();
        if (id.Length != 64 || !id.All(char.IsAsciiHexDigitLower) || Text(v, "StandardOutput").Trim() != volume || Text(s, "StandardOutput").Trim() != target
            || p.Length != 1 || End(v) > Begin(c) || End(c) > Begin(s) || End(s) > Begin(p[0]) || End(p[0]) > profile.GetProperty("observedAt").GetDateTimeOffset())
            throw new InvalidOperationException("The actual fresh backend create/start/readiness ordering differs from its profile.");
        return (v, c, s, p[0]);
    }

    private static DateTimeOffset Begin(JsonElement value) => value.GetProperty("Started").GetDateTimeOffset();
    private static DateTimeOffset End(JsonElement value) => value.GetProperty("Ended").GetDateTimeOffset();
    private static string Text(JsonElement value, string field) => value.GetProperty(field).GetString() ?? throw new InvalidOperationException("An actual backend coordinate is absent.");
}
