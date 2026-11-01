using System.Text.Json;
using System.Globalization;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds interrupted creation and repeated absence to the exact independently captured provider owner and actual commands.</summary>
internal static class HistoricalMatrixCleanupContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records,
        IReadOnlyList<HistoricalExecutionCommand> commands)
    {
        if (!records.TryGetValue("provider-created-before-interruption", out string? raw)) return;
        using var document = JsonDocument.Parse(raw);
        var containers = document.RootElement;
        if (containers.ValueKind != JsonValueKind.Array || containers.GetArrayLength() != 1)
            throw new InvalidOperationException("The interruption proof requires its one actual reserved provider.");
        var container = containers[0];
        if (!records.TryGetValue("provider-local-image", out string? imageRaw))
            throw new InvalidOperationException("The owned provider requires an actual local image observation; older captures do not supply it.");
        var image = JsonSerializer.Deserialize<JsonElement>(imageRaw);
        string imageId = QualificationImageIdentity.RequireApprovedEngineId(image);
        string owner = Text(container.GetProperty("Config").GetProperty("Labels"), "p1r.owner"), name = owner + "-creation-lifecycle";
        string id = Text(container, "Id");
        if (!owner.StartsWith("p1r-", StringComparison.Ordinal) || !Guid.TryParseExact(owner[4..], "N", out _)
            || Text(container, "Name") != "/" + name || Text(container, "Image") != imageId
            || Text(container.GetProperty("Config"), "Image") != PublishedCluster.RedisImage || container.GetProperty("State").GetProperty("Running").GetBoolean())
            throw new InvalidOperationException("The observed created provider lost exact owner/name/image/state coordinates.");
        var scoped = commands.Select(command => command.Record).Where(command =>
            command.GetProperty("Started").GetDateTimeOffset() >= observation.Started && command.GetProperty("Ended").GetDateTimeOffset() <= observation.Ended).ToArray();
        var launches = scoped.Where(command => FileName(command) == "python3" && Arguments(command) is { Length: 6 } args
            && args[0] == "-c" && args[1] == ContainerCreationDrill.LaunchScript && args[2] == name && args[3] == owner
            && args[4] == PublishedCluster.RedisImage && args[5] == Path.Combine(WorkingDirectory(command), "provider-created.json")
            && Path.GetFileName(WorkingDirectory(command)) == owner).ToArray();
        if (launches.Length != 1) throw new InvalidOperationException("The provider has no unique exact owned creation program.");
        var launch = launches[0]; string root = WorkingDirectory(launch);
        bool cancelled = observation.Case.StartsWith("cancellation", StringComparison.Ordinal);
        if (!launch.TryGetProperty("DeadlineStarted", out var deadline) || deadline.ValueKind != JsonValueKind.String
            || launch.GetProperty("Started").GetDateTimeOffset() > deadline.GetDateTimeOffset()
            || deadline.GetDateTimeOffset() > launch.GetProperty("Ended").GetDateTimeOffset()
            || (!cancelled && launch.GetProperty("Ended").GetDateTimeOffset() < deadline.GetDateTimeOffset().AddSeconds(8))
            || !launch.TryGetProperty("Interruption", out var interruption) || interruption.GetString() != (cancelled ? "cancelled" : "timeout"))
            throw new InvalidOperationException("The actual creation interruption lost its separate observation/deadline phase and cause.");
        if (launch.GetProperty("ExitCode").GetInt32() != -1 || launch.GetProperty("OutputTruncated").GetBoolean()
            || TimeSpan.Parse(Text(launch.GetProperty("Command"), "Timeout"), CultureInfo.InvariantCulture) != TimeSpan.FromSeconds(observation.Case.StartsWith("cancellation", StringComparison.Ordinal) ? 30 : 8)
            || !Text(launch, "StandardError").Contains("Command input/execution was interrupted or exceeded its deadline.", StringComparison.Ordinal))
            throw new InvalidOperationException("The exact creation launcher did not preserve its real bounded interruption.");
        bool Inspect(JsonElement command) => FileName(command) == "docker" && Arguments(command).SequenceEqual(new[] { "inspect", "--format", QualificationRuntime.ContainerIdentityFormat, name })
            && WorkingDirectory(command) == root && !command.GetProperty("OutputTruncated").GetBoolean();
        var before = scoped.Where(command => Inspect(command) && command.GetProperty("ExitCode").GetInt32() == 0
            && command.GetProperty("Started").GetDateTimeOffset() >= launch.GetProperty("Started").GetDateTimeOffset()
            && command.GetProperty("Ended").GetDateTimeOffset() <= launch.GetProperty("Ended").GetDateTimeOffset()
            && JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(Text(command, "StandardOutput")), containers)).ToArray();
        if (before.Length != 1 || before[0].GetProperty("Ended").GetDateTimeOffset() > deadline.GetDateTimeOffset()) throw new InvalidOperationException("The provider observation has no unique actual pre-interruption inspection.");
        var removals = scoped.Where(command => FileName(command) == "docker" && WorkingDirectory(command) == root
            && Arguments(command).SequenceEqual(new[] { "rm", "-fv", id }) && command.GetProperty("ExitCode").GetInt32() == 0
            && command.GetProperty("Started").GetDateTimeOffset() >= launch.GetProperty("Ended").GetDateTimeOffset()).ToArray();
        if (removals.Length != 1) throw new InvalidOperationException("Only the exact inspected immutable provider ID may be removed after interruption.");
        var images = scoped.Where(command => FileName(command) == "docker" && WorkingDirectory(command) == root
            && Arguments(command).SequenceEqual(new[] { "image", "inspect", "--format", QualificationImageIdentity.Format, PublishedCluster.RedisImage })
            && command.GetProperty("ExitCode").GetInt32() == 0 && !command.GetProperty("OutputTruncated").GetBoolean()
            && command.GetProperty("Started").GetDateTimeOffset() >= launch.GetProperty("Started").GetDateTimeOffset()
            && command.GetProperty("Ended").GetDateTimeOffset() <= removals[0].GetProperty("Started").GetDateTimeOffset()
            && JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(Text(command, "StandardOutput")), image)).ToArray();
        if (images.Length != 1 || images[0].GetProperty("Ended").GetDateTimeOffset() > deadline.GetDateTimeOffset()) throw new InvalidOperationException("The provider image binding has no unique actual immutable-reference inspection.");
        var absent = scoped.Where(command => Inspect(command) && command.GetProperty("ExitCode").GetInt32() != 0
            && command.GetProperty("Started").GetDateTimeOffset() >= removals[0].GetProperty("Ended").GetDateTimeOffset()
            && Text(command, "StandardError").Contains("no such object:", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (absent.Length < 3 || absent.Select(command => command.GetProperty("Started").GetDateTimeOffset()).Distinct().Count() != absent.Length)
            throw new InvalidOperationException("The owned provider lacks repeated independent post-removal absence evidence.");
    }

    private static string FileName(JsonElement command) => Text(command.GetProperty("Command"), "FileName");
    private static string WorkingDirectory(JsonElement command) => Text(command.GetProperty("Command"), "WorkingDirectory");
    private static string[] Arguments(JsonElement command) => command.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()!).ToArray();
    private static string Text(JsonElement data, string name) => data.GetProperty(name).GetString() ?? throw new InvalidOperationException("An actual provider lifecycle coordinate is absent.");
}
