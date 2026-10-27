using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Owns finite commands, inherited process identities, and label-scoped containers for one invocation.</summary>
internal sealed class QualificationRuntime : IAsyncDisposable
{
    private readonly List<OwnedProcess> _processes = [];
    private readonly HashSet<string> _containerNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _volumeNames = new(StringComparer.Ordinal);
    private readonly List<CommandObservation> _commands = [];
    private readonly List<string> _cleanupFailures = [];
    private readonly HashSet<string> _sensitivePaths = new(StringComparer.Ordinal);
    private string? _sharedContainers;
    private bool _cleaning;
    private bool _stopping;

    /// <summary>Gets the invocation's unique ownership marker.</summary>
    internal string Id { get; } = "p1r-" + Guid.NewGuid().ToString("N");

    /// <summary>Gets invocation-owned temporary storage. Dumps and credentials never leave this directory.</summary>
    internal string Root { get; }

    /// <summary>Gets measured command observations.</summary>
    internal IReadOnlyList<CommandObservation> Commands => _commands;

    /// <summary>Gets every cleanup failure, including failures from earlier cleanup passes.</summary>
    internal IReadOnlyList<string> CleanupFailures => _cleanupFailures;

    /// <summary>Initializes invocation storage before any external resource is created.</summary>
    internal QualificationRuntime()
    {
        Root = Path.Combine(Path.GetTempPath(), Id);
        Directory.CreateDirectory(Root);
    }

    /// <summary>Computes a stable lowercase SHA-256 identity.</summary>
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Records shared container IDs, image IDs, start times and running state before owned launches.</summary>
    internal async Task CaptureSharedContainersAsync()
        => _sharedContainers = await ContainerInventoryAsync(excludeOwned: true);

    /// <summary>Starts an operation and immediately records ownership before any cancellable await.</summary>
    internal OwnedProcess Start(QualificationCommand command)
    {
        if (_stopping && !IsCleanupCommand(command)) throw new ObjectDisposedException(nameof(QualificationRuntime), "Owned launches are closed once cleanup starts.");
        if (!_cleaning) TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(command.FileName)
        {
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = command.Input is not null,
            UseShellExecute = false,
        };
        foreach (string argument in command.Arguments) start.ArgumentList.Add(argument);
        start.Environment["P1R_QUALIFICATION_OWNER"] = Id;
        string marker = Guid.NewGuid().ToString("N");
        start.Environment["P1R_COMMAND_OWNER"] = marker;
        start.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        if (command.Environment is not null)
            foreach ((string key, string value) in command.Environment) start.Environment[key] = value;
        var process = new Process { StartInfo = start };
        DateTimeOffset attempted = DateTimeOffset.UtcNow;
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Process creation returned false.");
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Root, "startup-failure-" + Guid.NewGuid().ToString("N") + ".json"), JsonSerializer.Serialize(new
                { command, started = attempted, ended = DateTimeOffset.UtcNow, exitCode = (int?)null, error = error.GetType().FullName, error.Message }));
            process.Dispose();
            throw;
        }
        var owned = new OwnedProcess(process, process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync(), marker);
        _processes.Add(owned);
        File.WriteAllText(Path.Combine(Root, $"process-{process.Id}-start.json"), JsonSerializer.Serialize(new { command, started = DateTimeOffset.UtcNow, process.Id, marker }));
        if (command.Input is not null)
        {
            process.StandardInput.Write(command.Input);
            process.StandardInput.Close();
        }
        return owned;
    }

    /// <summary>Runs an operation with an independent finite deadline and retains failed command observations.</summary>
    internal async Task<CommandObservation> RunAsync(QualificationCommand command, CancellationToken cancellationToken = default)
    {
        DateTimeOffset began = DateTimeOffset.UtcNow;
        OwnedProcess owned = Start(command);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            _cleaning ? CancellationToken.None : TestContext.Current.CancellationToken);
        deadline.CancelAfter(command.Timeout);
        try
        {
            await owned.Process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            await StopAsync(owned);
            await StopMarkedAsync(owned.Marker);
            await Task.WhenAll(owned.Output, owned.Error).WaitAsync(TimeSpan.FromSeconds(10));
            var interrupted = new CommandObservation(command, began, DateTimeOffset.UtcNow, -1,
                await owned.Output, await owned.Error, Hash(Encoding.UTF8.GetBytes(await owned.Output + await owned.Error)));
            _commands.Add(interrupted);
            await SaveObservationAsync(interrupted);
            throw new TimeoutException($"Command interrupted or exceeded {command.Timeout}: {command.FileName} {string.Join(' ', command.Arguments)}");
        }
        // An exited launcher may leave a detached child holding its redirected pipes.
        try { await Task.WhenAll(owned.Output, owned.Error).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException)
        {
            await StopMarkedAsync(owned.Marker);
            await Task.WhenAll(owned.Output, owned.Error).WaitAsync(TimeSpan.FromSeconds(10));
            throw new InvalidOperationException("Detached descendants retained command output handles after parent exit.");
        }
        string output = await owned.Output;
        string error = await owned.Error;
        var observation = new CommandObservation(command, began, DateTimeOffset.UtcNow, owned.Process.ExitCode,
            output, error, Hash(Encoding.UTF8.GetBytes(output + error)));
        _commands.Add(observation);
        await SaveObservationAsync(observation);
        return observation;
    }

    /// <summary>Runs a command that must succeed, preserving exact nonzero diagnostics on failure.</summary>
    internal async Task<string> RequireAsync(string executable, string[] arguments, string? workingDirectory = null,
        TimeSpan? timeout = null, IReadOnlyDictionary<string, string>? environment = null, string? input = null)
    {
        CommandObservation result = await RunAsync(new(executable, arguments, workingDirectory ?? Root,
            timeout ?? TimeSpan.FromMinutes(3), environment, input));
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Exit {result.ExitCode}: {executable} {string.Join(' ', arguments)}\n{result.StandardOutput}\n{result.StandardError}");
        return result.StandardOutput;
    }

    /// <summary>Registers a predictable name before Docker creation, so interrupted creation remains owned.</summary>
    internal string ReserveContainer(string suffix)
    {
        string name = Id + "-" + suffix;
        _containerNames.Add(name);
        return name;
    }

    /// <summary>Registers a dump or credential for destruction on every cleanup path.</summary>
    internal void RegisterSensitivePath(string path) => _sensitivePaths.Add(path);

    /// <summary>Records a volume identity before the potentially interrupted create request.</summary>
    internal string ReserveVolume(string suffix)
    {
        string name = Id + "-" + suffix;
        _volumeNames.Add(name);
        return name;
    }

    /// <summary>Stops one directly owned process while output drains remain active.</summary>
    internal static async Task StopAsync(OwnedProcess owned)
    {
        if (!owned.Process.HasExited) owned.Process.Kill(entireProcessTree: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await owned.Process.WaitForExitAsync(deadline.Token);
    }

    /// <summary>Continues all cleanup attempts, verifies absence, and preserves shared identities on every pass.</summary>
    internal async Task CleanupAsync()
    {
        _stopping = true;
        _cleaning = true;
        try
        {
        foreach (OwnedProcess owned in _processes.ToArray())
        {
            try { await StopAsync(owned); }
            catch (Exception error) { _cleanupFailures.Add($"process {owned.Process.Id}: {error.Message}"); }
        }
        // The marker is inherited across fork/exec and setsid. Scan independently of the parent lifetime.
            DateTimeOffset descendantDeadline = DateTimeOffset.UtcNow.AddSeconds(15);
            int emptyPasses = 0;
            while (DateTimeOffset.UtcNow < descendantDeadline && emptyPasses < 2)
        {
                List<int> descendants = MarkedProcesses();
                emptyPasses = descendants.Count == 0 ? emptyPasses + 1 : 0;
                foreach (int pid in descendants)
            {
                    try
                    {
                        string start = File.ReadAllText($"/proc/{pid}/stat").Split(')')[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19];
                        using Process process = Process.GetProcessById(pid);
                        string current = File.ReadAllText($"/proc/{pid}/stat").Split(')')[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19];
                        if (start == current && MarkedProcesses().Contains(pid)) process.Kill(entireProcessTree: true);
                        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await process.WaitForExitAsync(deadline.Token);
                    }
                    catch (ArgumentException) { }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (Exception error) { _cleanupFailures.Add($"descendant {pid}: {error.Message}"); }
            }
                await Task.Delay(100);
        }
        foreach (OwnedProcess process in _processes.ToArray())
        {
            try
            {
                await Task.WhenAll(process.Output, process.Error).WaitAsync(TimeSpan.FromSeconds(5));
                await File.WriteAllTextAsync(Path.Combine(Root, $"process-{process.Process.Id}.log"),
                    await process.Output + "\n" + await process.Error);
            }
            catch (Exception error) { _cleanupFailures.Add("Bounded output drain: " + error.Message); }
        }
        foreach (string name in _containerNames)
        {
            try
            {
                CommandObservation inspect = await RunAsync(new("docker", ["inspect", name], Root, TimeSpan.FromSeconds(15)));
                if (inspect.ExitCode != 0)
                {
                    if (inspect.StandardError.Contains("no such object:", StringComparison.OrdinalIgnoreCase)) continue;
                    throw new InvalidOperationException("Cannot determine container absence: " + inspect.StandardError);
                }
                using JsonDocument json = JsonDocument.Parse(inspect.StandardOutput);
                string? label = json.RootElement[0].GetProperty("Config").GetProperty("Labels")
                    .GetProperty("p1r.owner").GetString();
                if (label != Id) throw new InvalidOperationException("Container ownership label changed; removal refused.");
                await RequireAsync("docker", ["rm", "-fv", name], timeout: TimeSpan.FromSeconds(30));
                CommandObservation absent = await RunAsync(new("docker", ["inspect", name], Root, TimeSpan.FromSeconds(15)));
                if (absent.ExitCode == 0 || !absent.StandardError.Contains("no such object:", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Owned container absence was not established: " + absent.StandardError);
            }
            catch (Exception error) { _cleanupFailures.Add($"container {name}: {error.Message}"); }
        }
        foreach (string name in _volumeNames)
        {
            try
            {
                CommandObservation inspect = await RunAsync(new("docker", ["volume", "inspect", name], Root, TimeSpan.FromSeconds(15)));
                if (inspect.ExitCode != 0)
                {
                    if (inspect.StandardError.Contains("no such volume", StringComparison.OrdinalIgnoreCase)) continue;
                    throw new InvalidOperationException("Cannot determine volume absence: " + inspect.StandardError);
                }
                using JsonDocument json = JsonDocument.Parse(inspect.StandardOutput);
                if (json.RootElement[0].GetProperty("Labels").GetProperty("p1r.owner").GetString() != Id)
                    throw new InvalidOperationException("Volume label changed; removal refused.");
                await RequireAsync("docker", ["volume", "rm", name]);
                CommandObservation absent = await RunAsync(new("docker", ["volume", "inspect", name], Root, TimeSpan.FromSeconds(15)));
                if (absent.ExitCode == 0 || !absent.StandardError.Contains("no such volume", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Owned volume absence was not established.");
            }
            catch (Exception error) { _cleanupFailures.Add($"volume {name}: {error.Message}"); }
        }
        foreach (string path in _sensitivePaths)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                else if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Sensitive path remains.");
            }
            catch (Exception error) { _cleanupFailures.Add("Sensitive scratch destruction: " + error.Message); }
        }
        if (MarkedProcesses().Count != 0) _cleanupFailures.Add("Owned descendant processes remain.");
        if (_sharedContainers is not null)
        {
            try
            {
                if (await ContainerInventoryAsync(excludeOwned: true) != _sharedContainers)
                    _cleanupFailures.Add("Shared container identity/state changed.");
            }
            catch (Exception error) { _cleanupFailures.Add("Shared container preservation: " + error.Message); }
        }
        await File.WriteAllTextAsync(Path.Combine(Root, "cleanup.json"), JsonSerializer.Serialize(_cleanupFailures));
        }
        finally { _cleaning = false; }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await CleanupAsync();

    private async Task SaveObservationAsync(CommandObservation observation)
        => await File.WriteAllTextAsync(Path.Combine(Root, $"command-{_commands.Count:D4}.json"), JsonSerializer.Serialize(observation));

    private static bool IsCleanupCommand(QualificationCommand command)
        => command.FileName == "docker" && command.Arguments.Length > 0
            && (command.Arguments[0] is "inspect" or "ps" or "rm"
                || command.Arguments[0] == "volume" && command.Arguments.Length > 1 && command.Arguments[1] is "inspect" or "rm");

    private List<int> MarkedProcesses()
    {
        var result = new List<int>();
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Exact descendant ownership currently requires Linux /proc.");
        foreach (string path in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(path), out int pid)) continue;
            try
            {
                string environment = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(path, "environ")));
                if (environment.Split('\0').Contains("P1R_QUALIFICATION_OWNER=" + Id, StringComparer.Ordinal)) result.Add(pid);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return result;
    }

    private async Task StopMarkedAsync(string marker)
    {
        foreach (int pid in MarkedProcesses())
        {
            try
            {
                string environment = Encoding.UTF8.GetString(File.ReadAllBytes($"/proc/{pid}/environ"));
                if (!environment.Split('\0').Contains("P1R_COMMAND_OWNER=" + marker, StringComparer.Ordinal)) continue;
                using Process process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (ArgumentException) { }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private async Task<string> ContainerInventoryAsync(bool excludeOwned)
    {
        string ids = await RequireAsync("docker", ["ps", "-aq", "--no-trunc"]);
        var inventory = new List<string>();
        foreach (string id in ids.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string raw = await RequireAsync("docker", ["inspect", id]);
            using JsonDocument json = JsonDocument.Parse(raw);
            JsonElement item = json.RootElement[0];
            if (excludeOwned && item.GetProperty("Config").GetProperty("Labels").ValueKind == JsonValueKind.Object
                && item.GetProperty("Config").GetProperty("Labels").TryGetProperty("p1r.owner", out JsonElement owner)
                && owner.GetString() == Id) continue;
            inventory.Add($"{id}|{item.GetProperty("Image").GetString()}|{item.GetProperty("State").GetProperty("StartedAt").GetString()}|{item.GetProperty("State").GetProperty("Running").GetBoolean()}");
        }
        return string.Join('\n', inventory.Order(StringComparer.Ordinal));
    }
}
