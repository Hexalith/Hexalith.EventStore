using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Reconstructs actual initial and repeated cleanup inventories and removal/absence chains from completed commands.</summary>
internal static class HistoricalMatrixCleanupSequenceContract
{
    internal static void Validate(ScenarioObservation observation, IReadOnlyDictionary<string, string> records,
        IReadOnlyList<HistoricalExecutionCommand> commands, IReadOnlyList<QualificationCleanupPass> passes, IReadOnlyList<QualificationProcessScan> processScans)
    {
        if (observation.Scenario != "failure-cleanup") return;
        int expectedPasses = observation.Case == "matrix-cleanup-twice" ? 2 : 3;
        if (!records.TryGetValue("cleanup-runtime", out string? raw))
            throw new InvalidOperationException("The repeated cleanup case requires its actual runtime identity.");
        var runtime = JsonSerializer.Deserialize<JsonElement>(raw); string owner = Text(runtime, "owner"), root = Text(runtime, "root");
        if (!owner.StartsWith("p1r-", StringComparison.Ordinal) || !Path.IsPathFullyQualified(root) || Path.GetFullPath(root) != root || Path.GetFileName(root) != owner)
            throw new InvalidOperationException("The repeated cleanup lost its exact owned command root.");
        var actual = passes.Where(pass => pass.Owner == owner && pass.Started >= observation.Started && pass.Ended <= observation.Ended).ToArray();
        if (actual.Length != expectedPasses || actual.Zip(actual.Skip(1)).Any(pair => pair.First.Ended > pair.Second.Started) || actual.Any(pass => pass.Root != root || pass.Failures != 0 || pass.RemainingProcesses != 0
            || pass.Ended < pass.Started || pass.BaselineStarted is null || pass.BaselineEnded is null || pass.Baseline is null || pass.After is null)
            || actual.Any(pass => pass.BaselineStarted != actual[0].BaselineStarted || pass.BaselineEnded != actual[0].BaselineEnded
                || pass.Baseline != actual[0].Baseline || !pass.ContainerNames.SequenceEqual(actual[0].ContainerNames)
                || !pass.VolumeNames.SequenceEqual(actual[0].VolumeNames))
            || actual[0].BaselineStarted != actual[1].BaselineStarted || actual[0].BaselineEnded != actual[1].BaselineEnded
            || actual[0].Baseline != actual[1].Baseline || !actual[0].ContainerNames.SequenceEqual(actual[1].ContainerNames)
            || !actual[0].VolumeNames.SequenceEqual(actual[1].VolumeNames))
            throw new InvalidOperationException("Both genuine independent cleanup passes and their exact owned roster are required.");
        if (records.TryGetValue("provider-created-before-interruption", out string? provider))
        {
            var created = JsonSerializer.Deserialize<JsonElement>(provider)[0];
            if (Text(created.GetProperty("Config").GetProperty("Labels"), "p1r.owner") != owner
                || !actual[0].ContainerNames.Contains(Text(created, "Name").TrimStart('/'), StringComparer.Ordinal))
                throw new InvalidOperationException("The interrupted provider differs from its actual disposal owner/roster.");
        }
        foreach (var pass in actual)
        {
            var scans = processScans.Where(scan => scan.Id == pass.ProcessScanId).ToArray();
            if (scans.Length != 1 || scans[0].Owner != owner || scans[0].Started < pass.Started || scans[0].Ended > pass.Ended
                || scans[0].Ended < scans[0].Started || scans[0].Pids.Count != 0 || pass.RemainingProcesses != scans[0].Pids.Count)
                throw new InvalidOperationException("An actual cleanup pass lost its independent empty owned-process observation.");
        }
        var captured = commands.Select(command => command.Record).Where(command => FileName(command) == "docker" && WorkingDirectory(command) == root).ToArray();
        foreach (var command in captured.Where(command => Started(command) < actual[0].Started))
        {
            string[] arguments = Arguments(command);
            if (!arguments.Contains("p1r.owner=" + owner, StringComparer.Ordinal)) continue;
            if (arguments is ["create", ..] || arguments is ["run", ..])
            {
                int nameIndex = Array.IndexOf(arguments, "--name");
                if (nameIndex < 0 || nameIndex + 1 >= arguments.Length || !actual[0].ContainerNames.Contains(arguments[nameIndex + 1], StringComparer.Ordinal))
                    throw new InvalidOperationException("An actually reserved provider was erased from both cleanup rosters.");
            }
            else if (arguments is ["volume", "create", ..] && !actual[0].VolumeNames.Contains(arguments[^1], StringComparer.Ordinal))
                throw new InvalidOperationException("An actually reserved provider volume was erased from both cleanup rosters.");
        }
        string baseline = Inventory(captured, owner, actual[0].BaselineStarted!.Value, actual[0].BaselineEnded!.Value);
        if (baseline != actual[0].Baseline || actual[0].BaselineEnded > actual[0].Started)
            throw new InvalidOperationException("The actual initial shared inventory differs from its independently completed scan.");
        foreach (var pass in actual)
        {
            if (Inventory(captured, owner, pass.Started, pass.Ended) != baseline || pass.After != baseline)
                throw new InvalidOperationException("An actual repeated cleanup changed or failed to observe shared container identity/state.");
            var scoped = captured.Where(command => Started(command) >= pass.Started && Ended(command) <= pass.Ended).ToArray();
            RequireNames(pass.ContainerNames, owner); RequireNames(pass.VolumeNames, owner);
            foreach (string name in pass.ContainerNames) ResourceAbsent(scoped, owner, name, volume: false);
            foreach (string name in pass.VolumeNames) ResourceAbsent(scoped, owner, name, volume: true);
        }
    }

    private static string Inventory(JsonElement[] commands, string owner, DateTimeOffset began, DateTimeOffset ended)
    {
        var scoped = commands.Where(command => Started(command) >= began && Ended(command) <= ended && Complete(command)).ToArray();
        var scans = scoped.Where(command => Arguments(command).SequenceEqual(new[] { "ps", "-aq", "--no-trunc" })).ToArray();
        if (scans.Length != 1) throw new InvalidOperationException("Every actual shared inventory requires its unique completed docker ps scan.");
        string[] ids = Text(scans[0], "StandardOutput").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Any(id => id.Length != 64 || !id.All(char.IsAsciiHexDigitLower)))
            throw new InvalidOperationException("The actual daemon inventory lost its exact distinct immutable IDs.");
        var inventory = new List<string>();
        foreach (string id in ids)
        {
            var inspections = scoped.Where(command => Arguments(command).SequenceEqual(new[] { "inspect", "--format", QualificationRuntime.ContainerIdentityFormat, id })
                && Started(command) >= Ended(scans[0])).ToArray();
            if (inspections.Length != 1) throw new InvalidOperationException("Every independently listed shared container requires its actual bounded inspection.");
            var observed = JsonSerializer.Deserialize<JsonElement>(Text(inspections[0], "StandardOutput"));
            if (observed.GetArrayLength() != 1 || Text(observed[0], "Id") != id) throw new InvalidOperationException("The shared inspection changed its actual immutable container.");
            var item = observed[0]; var labels = item.GetProperty("Config").GetProperty("Labels");
            if (labels.TryGetProperty("p1r.owner", out var label) && label.GetString() == owner) continue;
            inventory.Add($"{id}|{Text(item, "Image")}|{Text(item.GetProperty("State"), "StartedAt")}|{item.GetProperty("State").GetProperty("Running").GetBoolean()}");
        }
        return string.Join('\n', inventory.Order(StringComparer.Ordinal));
    }

    private static void ResourceAbsent(JsonElement[] commands, string owner, string name, bool volume)
    {
        string[] inspect = volume ? ["volume", "inspect", name] : ["inspect", "--format", QualificationRuntime.ContainerIdentityFormat, name];
        var inspections = commands.Where(command => Arguments(command).SequenceEqual(inspect) && !command.GetProperty("OutputTruncated").GetBoolean()).ToArray();
        if (inspections.Length == 0) throw new InvalidOperationException("Each exact owned resource requires its actual cleanup inspection.");
        var present = inspections.Where(command => Complete(command)).ToArray();
        if (present.Length > 1) throw new InvalidOperationException("A cleanup cannot replace one exact owned resource with another.");
        DateTimeOffset? removed = null;
        if (present.Length == 1)
        {
            var data = JsonSerializer.Deserialize<JsonElement>(Text(present[0], "StandardOutput"));
            if (data.GetArrayLength() != 1 || (volume ? Text(data[0], "Name") != name : Text(data[0], "Name") != "/" + name)
                || Text(volume ? data[0].GetProperty("Labels") : data[0].GetProperty("Config").GetProperty("Labels"), "p1r.owner") != owner)
                throw new InvalidOperationException("Actual cleanup resource ownership or identity changed.");
            string[] removal = volume ? ["volume", "rm", name] : ["rm", "-fv", Text(data[0], "Id")];
            var removals = commands.Where(command => Complete(command) && Arguments(command).SequenceEqual(removal) && Started(command) >= Ended(present[0])).ToArray();
            if (removals.Length != 1) throw new InvalidOperationException("Actual owned cleanup lost its exact verified resource removal.");
            removed = Ended(removals[0]);
        }
        string absence = volume ? "no such volume" : "no such object:";
        if (!inspections.Any(command => command.GetProperty("ExitCode").GetInt32() != 0
            && (removed is null || Started(command) >= removed) && Text(command, "StandardError").Contains(absence, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Actual owned cleanup did not independently prove resource absence.");
    }

    private static void RequireNames(IReadOnlyList<string> names, string owner)
    {
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count || names.Any(name => !name.StartsWith(owner + "-", StringComparison.Ordinal)))
            throw new InvalidOperationException("The exact cleanup resource roster changed its owner.");
    }

    private static bool Complete(JsonElement command) => command.GetProperty("ExitCode").GetInt32() == 0 && !command.GetProperty("OutputTruncated").GetBoolean();
    private static DateTimeOffset Started(JsonElement command) => command.GetProperty("Started").GetDateTimeOffset();
    private static DateTimeOffset Ended(JsonElement command) => command.GetProperty("Ended").GetDateTimeOffset();
    private static string FileName(JsonElement command) => Text(command.GetProperty("Command"), "FileName");
    private static string WorkingDirectory(JsonElement command) => Text(command.GetProperty("Command"), "WorkingDirectory");
    private static string[] Arguments(JsonElement command) => command.GetProperty("Command").GetProperty("Arguments").EnumerateArray().Select(value => value.GetString()!).ToArray();
    private static string Text(JsonElement data, string name) => data.GetProperty(name).GetString() ?? throw new InvalidOperationException("An actual cleanup coordinate is absent.");
}
