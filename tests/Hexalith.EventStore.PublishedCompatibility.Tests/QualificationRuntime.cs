using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Owns finite commands, inherited process identities, and label-scoped containers for one invocation.</summary>
internal sealed class QualificationRuntime : IAsyncDisposable
{
    /// <summary>Limits container diagnostics to identity, image, start/running state and the exact ownership label.</summary>
    internal const string ContainerIdentityFormat = """[{"Id":{{json .Id}},"Name":{{json .Name}},"Image":{{json .Image}},"Config":{"Image":{{json .Config.Image}},"Labels":{"p1r.owner":{{json (index .Config.Labels "p1r.owner")}}}},"State":{"StartedAt":{{json .State.StartedAt}},"Running":{{json .State.Running}}}}]""";

    private readonly object _gate = new();
    private readonly Action? _beforeProcessStart;
    private readonly Func<string, Task>? _beforeContainerRemoval;
    private int _nextReceipt;
    private readonly List<OwnedProcess> _processes = [];
    private readonly HashSet<string> _containerNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _volumeNames = new(StringComparer.Ordinal);
    private readonly List<CommandObservation> _commands = [];
    private readonly List<QualificationFileFingerprint> _files = [];
    private readonly List<QualificationHttpExchange> _httpExchanges = [];
    private readonly List<QualificationScenarioInput> _scenarioInputs = [];
    private readonly List<QualificationManagedLaunch> _importedManagedLaunches = [];
    internal IReadOnlyList<QualificationManagedLaunch> ManagedLaunches
    {
        get
        {
            lock (_gate) return _importedManagedLaunches.Concat(_processes.Where(owned => owned.Consumer is not null).Select(owned => new QualificationManagedLaunch(
                owned.Marker, Id, Root, owned.Consumer!.Role, owned.Consumer.Assembly, owned.ExecutableHashes!, owned.Process.Id,
                owned.Profile, owned.Address, owned.Started, owned.Process.HasExited ? new DateTimeOffset(owned.Process.ExitTime.ToUniversalTime()) : null))).ToArray();
        }
    }
    private readonly List<QualificationDaprLaunch> _daprLaunches = [];
    internal IReadOnlyList<QualificationDaprLaunch> DaprLaunches
    {
        get
        {
            lock (_gate) return _daprLaunches.Select(launch =>
            {
                var owned = launch.Owner == Id ? _processes.SingleOrDefault(process => process.Process.Id == launch.Pid && process.Started == launch.Started) : null;
                return owned is null ? launch : launch with { Started = owned.Started,
                    Ended = owned.Process.HasExited ? new DateTimeOffset(owned.Process.ExitTime.ToUniversalTime()) : null };
            }).ToArray();
        }
    }
    internal void RecordDaprLaunch(QualificationDaprLaunch launch) { lock (_gate) _daprLaunches.Add(launch); }
    private readonly List<QualificationStateRead> _stateReads = [];
    private readonly List<QualificationProcessScan> _processScans = [];
    internal IReadOnlyList<QualificationScenarioInput> ScenarioInputs { get { lock (_gate) return _scenarioInputs.ToArray(); } }
    internal IReadOnlyList<QualificationStateRead> StateReads { get { lock (_gate) return _stateReads.ToArray(); } }
    internal IReadOnlyList<QualificationProcessScan> ProcessScans { get { lock (_gate) return _processScans.ToArray(); } }
    internal void RecordStateRead(QualificationStateRead read) { lock (_gate) _stateReads.Add(read); }
    internal async Task<QualificationScenarioInput> CaptureScenarioInputAsync(string profile, string kind, string path)
    {
        if (!Path.GetFullPath(path).StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("A prepared runtime input must belong to its actual owner.");
        byte[] bytes = await File.ReadAllBytesAsync(path);
        var input = new QualificationScenarioInput(profile, Id, Root, kind, path, bytes.LongLength, Hash(bytes), DateTimeOffset.UtcNow,
            kind == "component" ? QualificationComponentBinding.Capture(Encoding.UTF8.GetString(bytes)) : null);
        lock (_gate) _scenarioInputs.Add(input);
        return input;
    }
    private readonly HashSet<string> _importedOwners = new(StringComparer.Ordinal);
    private readonly List<QualificationCleanupPass> _cleanupPasses = [];
    private readonly List<string> _cleanupFailures = [];
    private readonly HashSet<string> _sensitivePaths = new(StringComparer.Ordinal);
    private string? _sharedContainers;
    private Task? _sharedCapture;
    private DateTimeOffset? _sharedStarted;
    private DateTimeOffset? _sharedEnded;
    private bool _cleaning;
    private bool _stopping;
    private readonly SemaphoreSlim _cleanupGate = new(1, 1);
    private Task? _disposal;
    /// <summary>Gets whether the actual final disposal completed, including an observed failure.</summary>
    internal bool Disposed => _disposal?.IsCompleted == true;

    /// <summary>Gets the invocation's unique ownership marker.</summary>
    internal string Id { get; } = "p1r-" + Guid.NewGuid().ToString("N");

    /// <summary>Gets invocation-owned temporary storage. Dumps and credentials never leave this directory.</summary>
    internal string Root { get; }

    /// <summary>Gets measured command observations.</summary>
    internal IReadOnlyList<CommandObservation> Commands { get { lock (_gate) return _commands.ToArray(); } }

    /// <summary>Gets independently observed POST byte and safe semantic commitments.</summary>
    internal IReadOnlyList<QualificationHttpExchange> HttpExchanges { get { lock (_gate) return _httpExchanges.ToArray(); } }

    /// <summary>Retains actual transport observations independently of scenario assertion records.</summary>
    internal void RecordHttpExchange(QualificationHttpExchange exchange) { lock (_gate) _httpExchanges.Add(exchange); }

    /// <summary>Gets independent safe observations of actual private provider-dump bytes and destruction.</summary>
    internal IReadOnlyList<QualificationFileFingerprint> Files { get { lock (_gate) return _files.ToArray(); } }

    /// <summary>Gets actual independent cleanup passes for this runtime and its completed child owners.</summary>
    internal IReadOnlyList<QualificationCleanupPass> CleanupPasses { get { lock (_gate) return _cleanupPasses.ToArray(); } }

    /// <summary>Retains an actual completed child owner's observations after its independent disposal, without reconstructing evidence.</summary>
    internal void ImportCompletedRuntime(QualificationRuntime child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (ReferenceEquals(child, this) || Path.GetDirectoryName(child.Root) != Path.GetDirectoryName(Root)
            || !child.Disposed || child._cleaning || child._sharedContainers is null || child.MarkedProcesses().Count != 0)
            throw new InvalidOperationException("Only an independently stopped and observed child runtime may supply its actual ledger.");
        lock (_gate)
        {
            if (!_importedOwners.Add(child.Id)) throw new InvalidOperationException("A child observation ledger cannot be imported twice.");
            _commands.AddRange(child.Commands);
            _files.AddRange(child.Files);
            _httpExchanges.AddRange(child.HttpExchanges);
            _importedManagedLaunches.AddRange(child.ManagedLaunches); _daprLaunches.AddRange(child.DaprLaunches); _scenarioInputs.AddRange(child.ScenarioInputs); _stateReads.AddRange(child.StateReads); _processScans.AddRange(child.ProcessScans);
            _cleanupPasses.AddRange(child.CleanupPasses);
            _cleanupFailures.AddRange(child.CleanupFailures.Select(failure => "Child cleanup: " + failure));
        }
        if (child.CleanupFailures.Count != 0)
            throw new InvalidOperationException("The completed child runtime has actual cleanup failures; its preserved ledger is not passing evidence.");
    }

    /// <summary>Gets every cleanup failure, including failures from earlier cleanup passes.</summary>
    internal IReadOnlyList<string> CleanupFailures => _cleanupFailures;

    /// <summary>Initializes invocation storage before any external resource is created.</summary>
    /// <param name="beforeProcessStart">An optional deterministic fixture seam inside atomic launch admission.</param>
    /// <param name="beforeContainerRemoval">An optional fixture seam after ownership verification and before immutable-ID removal.</param>
    internal QualificationRuntime(Action? beforeProcessStart = null, Func<string, Task>? beforeContainerRemoval = null)
    {
        _beforeProcessStart = beforeProcessStart;
        _beforeContainerRemoval = beforeContainerRemoval;
        string parent = Environment.GetEnvironmentVariable("P1R_QUALIFICATION_SCRATCH_ROOT")
            ?? (OperatingSystem.IsLinux() ? "/var/tmp/hexalith-eventstore-qualification" : Path.Combine(Path.GetTempPath(), "hexalith-eventstore-qualification"));
        Root = Path.Combine(parent, Id);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(Root);
        else Directory.CreateDirectory(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>Computes a stable lowercase SHA-256 identity.</summary>
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Records shared container IDs, image IDs, start times and running state before owned launches.</summary>
    internal Task CaptureSharedContainersAsync()
    {
        lock (_gate) return _sharedCapture ??= CaptureSharedContainersCoreAsync();
    }

    private async Task CaptureSharedContainersCoreAsync()
    {
        _sharedStarted = DateTimeOffset.UtcNow;
        _sharedContainers = await ContainerInventoryAsync(excludeOwned: true);
        _sharedEnded = DateTimeOffset.UtcNow;
    }

    /// <summary>Starts an operation and immediately records ownership before any cancellable await.</summary>
    internal OwnedProcess Start(QualificationCommand command, ConsumerBuild? consumer = null, string? profile = null)
    {
        lock (_gate)
        {
            if (_stopping && !IsCleanupCommand(command)) throw new ObjectDisposedException(nameof(QualificationRuntime), "Owned launches are closed once cleanup starts.");
            if (!_cleaning) TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var outputs = consumer?.VerifyExecutableHashes();
            if (consumer is not null && (command.FileName != "dotnet" || command.Arguments.FirstOrDefault() != consumer.Assembly))
                throw new InvalidOperationException("A generated consumer must execute its exact prepared assembly.");
            string? address = null;
            if (consumer is not null)
            {
                int urls = Array.IndexOf(command.Arguments, "--urls");
                if (urls >= 0) address = command.Arguments[urls + 1];
                else if (command.Environment is not null && command.Environment.TryGetValue("ASPNETCORE_URLS", out var configured)) address = configured;
            }
            var start = new ProcessStartInfo(command.FileName)
            {
                WorkingDirectory = command.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = command.Input is not null,
                UseShellExecute = false,
            };
            foreach (string argument in command.Arguments) start.ArgumentList.Add(argument);
            string marker = Guid.NewGuid().ToString("N");
            start.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            if (command.Environment is not null)
                foreach ((string key, string value) in command.Environment) start.Environment[key] = value;
            start.Environment["P1R_QUALIFICATION_OWNER"] = Id;
            start.Environment["P1R_COMMAND_OWNER"] = marker;
            var process = new Process { StartInfo = start };
            DateTimeOffset attempted = DateTimeOffset.UtcNow;
            try
            {
                _beforeProcessStart?.Invoke();
                if (!process.Start()) throw new InvalidOperationException("Process creation returned false.");
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Root, "startup-failure-" + Guid.NewGuid().ToString("N") + ".json"), JsonSerializer.Serialize(new
                    { command, started = attempted, ended = DateTimeOffset.UtcNow, exitCode = (int?)null, error = error.GetType().FullName, error.Message }));
                process.Dispose();
                throw;
            }
            var owned = new OwnedProcess(process, new(process.StandardOutput), new(process.StandardError), marker, command)
                { Consumer = consumer, ExecutableHashes = outputs, Profile = profile, Address = address, Started = attempted };
            _processes.Add(owned);
            owned.OverflowCleanup = ObserveOverflowAsync(command, owned, attempted);
            File.WriteAllText(Path.Combine(Root, $"process-{process.Id}-start.json"), JsonSerializer.Serialize(new { command, started = DateTimeOffset.UtcNow, process.Id, marker }));
            return owned;
        }
    }

    /// <summary>Requires the actual owned Redis SAVE acknowledgement before any dump can be copied.</summary>
    internal async Task RequireRedisSaveAsync(string container)
    {
        if (!_containerNames.Contains(container)) throw new InvalidOperationException("Redis SAVE requires this invocation's reserved provider.");
        string result = await RequireAsync("docker", ["exec", container, "redis-cli", "SAVE"]);
        if (result.Trim() != "OK") throw new InvalidOperationException("The owned Redis did not acknowledge its actual SAVE.");
    }

    /// <summary>Runs an operation with an independent finite deadline and retains failed command observations.</summary>
    internal async Task<CommandObservation> RunAsync(QualificationCommand command, CancellationToken cancellationToken = default, ConsumerBuild? consumer = null)
    {
        if (Path.GetFileName(command.FileName) == "docker" && command.Arguments.FirstOrDefault() == "inspect"
            && !command.Arguments.Contains("--format", StringComparer.Ordinal) && !command.Arguments.Contains("-f", StringComparer.Ordinal))
            command = command with { Arguments = ["inspect", "--format", ContainerIdentityFormat, .. command.Arguments.Skip(1)] };
        DateTimeOffset deadlineStarted = DateTimeOffset.UtcNow;
        return await CompleteStartedAsync(Start(command, consumer), cancellationToken, deadlineStarted);
    }

    /// <summary>Completes an already owned command under its unchanged deadline, after bounded pre-interruption observation.</summary>
    internal async Task<CommandObservation> CompleteStartedAsync(OwnedProcess owned, CancellationToken cancellationToken = default, DateTimeOffset? executionStarted = null)
    {
        lock (_gate)
            if (!_processes.Contains(owned)) throw new InvalidOperationException("Only an actual owned process can begin its execution deadline.");
        QualificationCommand command = owned.Command;
        DateTimeOffset began = owned.Started;
        DateTimeOffset deadlineStarted = executionStarted ?? DateTimeOffset.UtcNow;
        CancellationToken testToken = _cleaning ? CancellationToken.None : TestContext.Current.CancellationToken;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, testToken);
        TimeSpan remaining = command.Timeout - (DateTimeOffset.UtcNow - deadlineStarted);
        if (remaining <= TimeSpan.Zero) deadline.Cancel();
        else deadline.CancelAfter(remaining);
        try
        {
            if (command.Input is not null)
            {
                await owned.Process.StandardInput.WriteAsync(command.Input.AsMemory(), deadline.Token);
                await owned.Process.StandardInput.FlushAsync(deadline.Token);
                owned.Process.StandardInput.Close();
            }
            await Task.WhenAny(owned.Process.WaitForExitAsync(deadline.Token), owned.OverflowCleanup).WaitAsync(deadline.Token);
            if (owned.Overflowed)
            {
                await owned.OverflowCleanup;
                throw new InvalidOperationException("Command output exceeded its bounded capture limit.");
            }
            await owned.Process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException cancelledError)
        {
            bool cancelled = cancellationToken.IsCancellationRequested || testToken.IsCancellationRequested;
            await StopAsync(owned);
            await StopMarkedAsync(owned.Marker);
            try { await Task.WhenAll(owned.Output, owned.Error).WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception) { /* The failed observation below still binds any completed output. */ }
            await RecordFailedAsync(command, owned, began, "Command input/execution was interrupted or exceeded its deadline.", deadlineStarted, cancelled ? "cancelled" : "timeout");
            if (cancelled) throw new OperationCanceledException("Owned command cancelled after cleanup.", cancelledError, cancellationToken.IsCancellationRequested ? cancellationToken : testToken);
            throw new TimeoutException($"Command interrupted or exceeded {command.Timeout}: {command.FileName} {string.Join(' ', command.Arguments)}");
        }
        catch (IOException)
        {
            await StopAsync(owned);
            await StopMarkedAsync(owned.Marker);
            await RecordFailedAsync(command, owned, began, "Command standard-input delivery failed because its pipe closed.");
            throw;
        }
        // An exited launcher may leave a detached child holding its redirected pipes.
        try { await Task.WhenAll(owned.Output, owned.Error).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException)
        {
            await StopMarkedAsync(owned.Marker);
            try { await Task.WhenAll(owned.Output, owned.Error).WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception) { /* Preserve failed output ownership even when a pipe cannot be drained. */ }
            await RecordFailedAsync(command, owned, began, "Detached descendants retained command output handles after parent exit.");
            throw new InvalidOperationException("Detached descendants retained command output handles after parent exit.");
        }
        if (owned.Overflowed)
        {
            await owned.OverflowCleanup;
            throw new InvalidOperationException("Command output exceeded its bounded capture limit.");
        }
        string originalOutput = await owned.Output, originalError = await owned.Error;
        string output = Redact(command, originalOutput);
        string error = Redact(command, originalError);
        var observation = new CommandObservation(command, began, DateTimeOffset.UtcNow, owned.Process.ExitCode,
            output, error, Hash(Encoding.UTF8.GetBytes(originalOutput + originalError)), DeadlineStarted: deadlineStarted, OriginalBytesRedacted: output != originalOutput || error != originalError);
        if (owned.TryRecord()) await SaveObservationAsync(observation);
        return observation;
    }

    private async Task RecordFailedAsync(QualificationCommand command, OwnedProcess owned, DateTimeOffset began, string failure, DateTimeOffset? deadlineStarted = null, string? interruption = null)
    {
        if (!owned.TryRecord()) return;
        string originalOutput = owned.StandardOutput.Snapshot(), originalError = owned.StandardError.Snapshot() + "\n" + failure;
        string output = Redact(command, originalOutput);
        string error = Redact(command, originalError);
        var observation = new CommandObservation(command, began, DateTimeOffset.UtcNow, -1, output, error, Hash(Encoding.UTF8.GetBytes(originalOutput + originalError)), owned.Overflowed, deadlineStarted, interruption, output != originalOutput || error != originalError);
        await SaveObservationAsync(observation);
    }

    /// <summary>Runs a command that must succeed, preserving exact nonzero diagnostics on failure.</summary>
    internal async Task<string> RequireAsync(string executable, string[] arguments, string? workingDirectory = null,
        TimeSpan? timeout = null, IReadOnlyDictionary<string, string>? environment = null, string? input = null, ConsumerBuild? consumer = null)
    {
        CommandObservation result = await RunAsync(new(executable, arguments, workingDirectory ?? Root,
            timeout ?? TimeSpan.FromMinutes(3), environment, input), consumer: consumer);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Exit {result.ExitCode}: {executable} {string.Join(' ', arguments)}\n{QualificationCommandObservationConverter.Capture(result).StandardOutput}\n{QualificationCommandObservationConverter.Capture(result).StandardError}");
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

    /// <summary>Observes one real registered provider dump at a finite lifecycle boundary without retaining its contents.</summary>
    internal async Task<QualificationFileFingerprint> ObserveProviderDumpAsync(string path, string purpose, CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        string name = Path.GetFileName(path);
        if (purpose is not ("provider-backup-copied" or "provider-backup-before-restore" or "provider-backup-destroyed")
            || Path.GetDirectoryName(path) != Root || !_sensitivePaths.Contains(path)
            || name.Length != 43 || !name.StartsWith("backup-", StringComparison.Ordinal) || !name.EndsWith(".rdb", StringComparison.Ordinal)
            || !Guid.TryParseExact(name[7..^4], "N", out _)
            || (File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Only this runtime's registered private provider dump may be observed.");
        DateTimeOffset began = DateTimeOffset.UtcNow;
        bool exists = true; long length = 0; string? digest = null;
        try
        {
            if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new IOException("The provider dump must be its actual regular file.");
            DateTime modified = File.GetLastWriteTimeUtc(path);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, useAsync: true);
            length = stream.Length;
            digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (stream.Length != length || new FileInfo(path).Length != length || File.GetLastWriteTimeUtc(path) != modified)
                throw new IOException("The actual provider dump changed during its independent observation.");
        }
        catch (FileNotFoundException) when (purpose == "provider-backup-destroyed") { exists = false; }
        catch (DirectoryNotFoundException) when (purpose == "provider-backup-destroyed") { exists = false; }
        if (exists == (purpose == "provider-backup-destroyed") || exists && length <= 0)
            throw new InvalidOperationException("The actual provider dump did not match its lifecycle boundary.");
        var observed = new QualificationFileFingerprint(Id, path, purpose, exists, length, digest, began, DateTimeOffset.UtcNow);
        int number;
        lock (_gate) { _files.Add(observed); number = _files.Count; }
        await File.WriteAllTextAsync(Path.Combine(Root, $"file-fingerprint-{number:D6}.json"), JsonSerializer.Serialize(observed), cancellationToken).ConfigureAwait(false);
        return observed;
    }

    /// <summary>Records a volume identity before the potentially interrupted create request.</summary>
    internal string ReserveVolume(string suffix)
    {
        string name = Id + "-" + suffix;
        _volumeNames.Add(name);
        return name;
    }

    /// <summary>Stops one directly owned process while output drains remain active.</summary>
    internal static async Task StopAsync(OwnedProcess owned, CancellationToken cancellationToken = default)
    {
        if (!owned.Process.HasExited) owned.Process.Kill(entireProcessTree: false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        await owned.Process.WaitForExitAsync(deadline.Token);
    }

    /// <summary>Continues all cleanup attempts, verifies absence, and preserves shared identities on every pass.</summary>
    internal Task CleanupAsync()
    {
        lock (_gate) return _disposal ?? CleanupCoreAsync(final: false);
    }

    private async Task CleanupCoreAsync(bool final)
    {
        await _cleanupGate.WaitAsync();
        DateTimeOffset passStarted = DateTimeOffset.UtcNow;
        string? afterInventory = null;
        lock (_gate)
        {
            _stopping = true;
            _cleaning = true;
        }
        try
        {
        foreach (OwnedProcess owned in ProcessSnapshot())
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
                        await StopMarkedProcessAsync(pid, marker: null);
                    }
                    catch (ArgumentException) { }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (Exception error) { _cleanupFailures.Add($"descendant {pid}: {error.Message}"); }
            }
                await Task.Delay(100);
        }
        foreach (OwnedProcess process in ProcessSnapshot())
        {
            try
            {
                await Task.WhenAll(process.Output, process.Error).WaitAsync(TimeSpan.FromSeconds(5));
                await File.WriteAllTextAsync(Path.Combine(Root, $"process-{process.Process.Id}.log"),
                    HistoricalSafeJson.Text(Redact(process.Command, await process.Output)) + "\n" + HistoricalSafeJson.Error(process.Command, Redact(process.Command, await process.Error)));
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
                string containerId = json.RootElement[0].GetProperty("Id").GetString()!;
                if (_beforeContainerRemoval is not null) await _beforeContainerRemoval(containerId);
                await RequireAsync("docker", ["rm", "-fv", containerId], timeout: TimeSpan.FromSeconds(30));
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
                try { _ = File.GetAttributes(path); throw new IOException("Sensitive path remains."); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
            catch (Exception error) { _cleanupFailures.Add("Sensitive scratch destruction: " + error.Message); }
        }
        DateTimeOffset scanStarted = DateTimeOffset.UtcNow;
        var pids = MarkedProcesses().Order().ToArray();
        var processScan = new QualificationProcessScan(Guid.NewGuid().ToString("N"), Id, scanStarted, DateTimeOffset.UtcNow, pids);
        lock (_gate) _processScans.Add(processScan);
        int remainingProcesses = pids.Length;
        if (remainingProcesses != 0) _cleanupFailures.Add("Owned descendant processes remain.");
        if (_sharedContainers is not null)
        {
            try
            {
                afterInventory = await ContainerInventoryAsync(excludeOwned: true);
                if (afterInventory != _sharedContainers)
                    _cleanupFailures.Add("Shared container identity/state changed.");
            }
            catch (Exception error) { _cleanupFailures.Add("Shared container preservation: " + error.Message); }
        }
        await File.WriteAllTextAsync(Path.Combine(Root, "cleanup.json"), JsonSerializer.Serialize(_cleanupFailures));
        var pass = new QualificationCleanupPass(Id, Root, passStarted, DateTimeOffset.UtcNow, _sharedStarted, _sharedEnded,
            _sharedContainers, afterInventory, _containerNames.Order(StringComparer.Ordinal).ToArray(), _volumeNames.Order(StringComparer.Ordinal).ToArray(),
            _cleanupFailures.Count, remainingProcesses, processScan.Id, final);
        int number;
        lock (_gate) { _cleanupPasses.Add(pass); number = _cleanupPasses.Count; }
        await File.WriteAllTextAsync(Path.Combine(Root, $"cleanup-pass-{number:D6}.json"), JsonSerializer.Serialize(pass));
        }
        finally { _cleaning = false; _cleanupGate.Release(); }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        lock (_gate) return new(_disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        await CleanupCoreAsync(final: true);
        if (_cleanupFailures.Count != 0)
            throw new InvalidOperationException("Final owned disposal failed; preserved cleanup evidence is nonpassing.");
    }

    private async Task SaveObservationAsync(CommandObservation observation)
    {
        int receipt = Interlocked.Increment(ref _nextReceipt);
        lock (_gate) _commands.Add(observation);
        await File.WriteAllTextAsync(Path.Combine(Root, $"command-{receipt:D6}.json"), JsonSerializer.Serialize(observation));
    }

    private OwnedProcess[] ProcessSnapshot()
    {
        lock (_gate) return _processes.ToArray();
    }

    private async Task ObserveOverflowAsync(QualificationCommand command, OwnedProcess owned, DateTimeOffset began)
    {
        Task finished = Task.WhenAll(owned.Output, owned.Error);
        Task overflow = Task.WhenAny(owned.StandardOutput.Overflow, owned.StandardError.Overflow);
        if (await Task.WhenAny(finished, overflow) != overflow && !owned.Overflowed) return;
        try
        {
            await StopAsync(owned);
            await StopMarkedAsync(owned.Marker);
        }
        catch (Exception error)
        {
            lock (_gate) _cleanupFailures.Add("Output overflow cleanup: " + error.Message);
        }
        await RecordFailedAsync(command, owned, began, "Command output exceeded the bounded capture limit; retained prefixes are incomplete and nonpassing.");
    }

    private static string Redact(QualificationCommand command, string value)
    {
        if (!string.IsNullOrEmpty(command.Input)) value = value.Replace(command.Input, "[REDACTED]", StringComparison.Ordinal);
        if (command.Environment is not null)
            foreach ((string name, string secret) in command.Environment)
                if (secret.Length > 0 && new[] { "TOKEN", "SECRET", "PASSWORD", "PRIVATE" }.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase)))
                    value = value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        return value;
    }

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
                await StopMarkedProcessAsync(pid, marker);
            }
            catch (ArgumentException) { }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private async Task StopMarkedProcessAsync(int pid, string? marker)
    {
        string path = $"/proc/{pid}";
        string start = File.ReadAllText(Path.Combine(path, "stat")).Split(')')[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19];
        bool Matches()
        {
            string[] environment = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(path, "environ"))).Split('\0');
            return environment.Contains("P1R_QUALIFICATION_OWNER=" + Id, StringComparer.Ordinal)
                && (marker is not null ? environment.Contains("P1R_COMMAND_OWNER=" + marker, StringComparer.Ordinal)
                    : ProcessSnapshot().Any(owned => environment.Contains("P1R_COMMAND_OWNER=" + owned.Marker, StringComparer.Ordinal)));
        }
        if (!Matches()) return;
        using Process process = Process.GetProcessById(pid);
        _ = process.SafeHandle;
        string current = File.ReadAllText(Path.Combine(path, "stat")).Split(')')[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19];
        if (start != current || !Matches()) return;
        process.Kill(entireProcessTree: false);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task<string> ContainerInventoryAsync(bool excludeOwned)
    {
        string ids = await RequireAsync("docker", ["ps", "-aq", "--no-trunc"]);
        var inventory = new List<string>();
        foreach (string id in ids.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string raw = await RequireAsync("docker", ["inspect", "--format", ContainerIdentityFormat, id]);
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
