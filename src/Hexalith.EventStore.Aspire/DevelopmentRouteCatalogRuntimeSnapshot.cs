using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>Authenticates the selected child's actual managed runtime without executing application startup.</summary>
internal sealed class DevelopmentRouteCatalogRuntimeSnapshot
{
    private const string ProbeName = "Hexalith.EventStore.RuntimeIdentityProbe.dll";
    private static readonly string[] Contracts = ["System.Text.Json", "System.Text.Encodings.Web", "System.IO.Pipelines"];
    private readonly IResourceBuilder<ProjectResource> _project;
    private readonly SortedDictionary<string, string> _environment;
    private readonly Dictionary<string, string?> _inputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _originalContracts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sharedContracts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Path, string Digest)> _frameworks;
    private readonly string _host;
    private readonly string _hostDigest;
    private readonly Dictionary<string, string[]> _runtimeDirectories = new(StringComparer.Ordinal);
    private readonly string _selectedFrameworkRoot;
    private string? _probe;
    private string? _snapshotConfig;
    private string? _snapshotDependencies;
    private JsonObject? _probeEvidence;
    private Func<ProcessStartInfo, string, CancellationToken, Task>? _runProbeAsync;

    private DevelopmentRouteCatalogRuntimeSnapshot(IResourceBuilder<ProjectResource> project,
        SortedDictionary<string, string> environment, string host, Dictionary<string, (string Path, string Digest)> frameworks)
    {
        _project = project;
        _environment = environment;
        _host = host;
        _frameworks = frameworks;
        foreach (var framework in frameworks.Values) _inputs.TryAdd(framework.Path, framework.Digest);
        _hostDigest = Hash(host);
        _inputs.Add(host, _hostDigest);
        // The probe may execute only the frozen ordinary host and installed framework bodies,
        // including their native libraries. Unrelated framework versions are not snapshotted;
        // a fresh pre-start probe detects whether a new installation actually changes selection.
        string installation = Path.GetDirectoryName(CanonicalPath(host))!;
        _selectedFrameworkRoot = Path.Combine(installation, "shared") + Path.DirectorySeparatorChar;
        foreach (string directory in frameworks.Values.Select(framework => Path.GetDirectoryName(framework.Path)!).Distinct(StringComparer.Ordinal))
        {
            TrackRuntimeDirectory(directory);
            string framework = Path.GetFileName(Path.GetDirectoryName(directory)!);
            string selected = Path.Combine(_selectedFrameworkRoot, framework, Path.GetFileName(directory));
            if (Directory.Exists(selected)) TrackRuntimeDirectory(selected);
        }
        string hostFxr = Path.Combine(installation, "host", "fxr");
        if (Directory.Exists(hostFxr))
            foreach (string directory in Directory.EnumerateDirectories(hostFxr)) TrackRuntimeDirectory(directory);
    }

    /// <summary>The finite three authenticated contract files available to the isolated owner resolver.</summary>
    internal IReadOnlyDictionary<string, string> SharedContracts => _sharedContracts;

    /// <summary>Captures actual runtime-selection inputs before the selected build can change them.</summary>
    internal static async Task<DevelopmentRouteCatalogRuntimeSnapshot> CaptureAsync(IResourceBuilder<ProjectResource> project,
        CancellationToken cancellationToken)
    {
        string frameworkRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location)))! + Path.DirectorySeparatorChar;
        var frameworks = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty).Split(Path.PathSeparator)
            .Where(path => path.StartsWith(frameworkRoot, StringComparison.Ordinal) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal).ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => (Path: path, Digest: Hash(path)), StringComparer.Ordinal);
        if (frameworks.Count == 0) throw new InvalidOperationException("The actual prebuild framework inventory is unavailable.");
        var environment = await RuntimeEnvironmentAsync(project, cancellationToken).ConfigureAwait(false);
        string host = RequireOrdinaryLaunchHost(project, environment);
        return new(project, environment, host, frameworks);
    }

    /// <summary>Uses the SDK-owned probe in the exact selected app base, then freezes only its verified contract bodies.</summary>
    internal async Task<JsonObject> PrepareAsync(string target, string snapshot, IReadOnlyDictionary<Assembly, string> loadedSdk,
        Dictionary<string, string> identities, Dictionary<string, string> sources,
        Func<ProcessStartInfo, string, CancellationToken, Task> runAsync, CancellationToken cancellationToken)
    {
        string runtimeConfig = Path.ChangeExtension(target, ".runtimeconfig.json");
        string dependencies = Path.ChangeExtension(target, ".deps.json");
        if (!File.Exists(runtimeConfig)) throw new InvalidOperationException("The selected child must produce its actual runtime configuration.");
        string snapshotConfig = Path.Combine(snapshot, Path.GetFileName(runtimeConfig));
        string snapshotDependencies = Path.Combine(snapshot, Path.GetFileName(dependencies));
        if (!File.Exists(snapshotConfig) || !File.Exists(snapshotDependencies))
            throw new InvalidOperationException("The selected runtime configuration and dependencies were not frozen.");
        Track(runtimeConfig); Track(snapshotConfig); Track(dependencies); Track(snapshotDependencies);
        string runtimeConfigDev = Path.ChangeExtension(target, ".runtimeconfig.dev.json");
        string snapshotConfigDev = Path.Combine(snapshot, Path.GetFileName(runtimeConfigDev));
        string? devDigest = File.Exists(runtimeConfigDev) ? Hash(runtimeConfigDev) : null;
        if ((devDigest is not null) != File.Exists(snapshotConfigDev)
            || devDigest is not null && Hash(snapshotConfigDev) != devDigest)
            throw new InvalidOperationException("The selected optional runtime configuration changed before its immutable snapshot was prepared.");
        _inputs.Add(runtimeConfigDev, devDigest);
        _inputs.Add(snapshotConfigDev, devDigest);
        foreach (string config in new[] { snapshotConfig, Path.Combine(snapshot, Path.GetFileName(runtimeConfigDev)) }.Where(File.Exists))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllBytesAsync(config, cancellationToken).ConfigureAwait(false));
            if (settings.RootElement.TryGetProperty("runtimeOptions", out var options)
                && options.TryGetProperty("configProperties", out var properties)
                && properties.EnumerateObject().Any(property => property.Name.Equals("STARTUP_HOOKS", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("ADDITIONAL_DEPS", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Selected owner runtime configuration hooks and additional dependencies are unsupported.");
            if (settings.RootElement.TryGetProperty("runtimeOptions", out options)
                && options.TryGetProperty("additionalProbingPaths", out var probes)
                && (probes.ValueKind != JsonValueKind.Array || probes.GetArrayLength() != 0))
                throw new InvalidOperationException("Selected owner additional runtime probing paths are unsupported.");
        }
        using var declared = JsonDocument.Parse(await File.ReadAllBytesAsync(snapshotDependencies, cancellationToken).ConfigureAwait(false));
        foreach (string name in Contracts)
        {
            string local = Path.Combine(snapshot, name + ".dll");
            string original = Path.Combine(Path.GetDirectoryName(target)!, name + ".dll");
            string? originalDigest = File.Exists(original) ? Hash(original) : null;
            if ((originalDigest is not null) != File.Exists(local)
                || originalDigest is not null && Hash(local) != originalDigest)
                throw new InvalidOperationException($"Selected child runtime dependency '{name}' changed before its immutable snapshot was prepared.");
            _originalContracts.Add(original, originalDigest);
            Assembly parent = loadedSdk.Keys.Single(assembly => assembly.GetName().Name == name);
            if (File.Exists(local) && Hash(local) != loadedSdk[parent])
                throw new InvalidOperationException($"Selected child SDK dependency '{name}' differs from the actual AppHost SDK.");
            if (!File.Exists(local) && declared.RootElement.GetProperty("targets").EnumerateObject()
                .SelectMany(framework => framework.Value.EnumerateObject()).SelectMany(library => library.Value.EnumerateObject())
                .Where(group => group.Name is "runtime" or "runtimeTargets")
                .SelectMany(group => group.Value.EnumerateObject()).Any(asset => Path.GetFileName(asset.Name) == name + ".dll"))
                throw new InvalidOperationException($"Selected child JSON contract dependency '{name}' is absent from its immutable output snapshot.");
        }
        // Probe output is authority only after all framework implementations it could execute are
        // authenticated. A child may not shadow Console, cryptography or metadata code through deps.
        var resolver = new AssemblyDependencyResolver(Path.Combine(snapshot, Path.GetFileName(target)));
        foreach (var framework in _frameworks.Where(framework => !Contracts.Contains(framework.Key, StringComparer.Ordinal)))
        {
            string local = Path.Combine(snapshot, framework.Key + ".dll");
            string? resolved = resolver.ResolveAssemblyToPath(AssemblyName.GetAssemblyName(framework.Value.Path));
            if (File.Exists(local) && Hash(local) != framework.Value.Digest
                || resolved is not null && (!Path.GetFullPath(resolved).StartsWith(snapshot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || Hash(resolved) != framework.Value.Digest))
                throw new InvalidOperationException($"Selected child runtime probe framework dependency '{framework.Key}' is not the actual prebuild framework body.");
        }
        string probe = Path.Combine(snapshot, ProbeName);
        using (Stream embedded = typeof(DevelopmentRouteCatalogRuntimeSnapshot).Assembly
            .GetManifestResourceStream("Hexalith.EventStore.Aspire.RuntimeIdentityProbe.dll")
            ?? throw new InvalidOperationException("The actual SDK-owned runtime probe is absent."))
        using (var output = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await embedded.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(probe, UnixFileMode.UserRead);
        Track(probe);
        _probe = probe;
        _snapshotConfig = snapshotConfig;
        _snapshotDependencies = snapshotDependencies;
        _runProbeAsync = runAsync;
        await VerifyAsync(cancellationToken).ConfigureAwait(false);
        var evidence = await ProbeAsync("runtime", cancellationToken).ConfigureAwait(false);
        _probeEvidence = (JsonObject)evidence.DeepClone();
        if (evidence["protocol"]?.GetValue<string>() != "EventStoreSelectedOwnerManagedRuntimeV1"
            || evidence["hostSha256"]?.GetValue<string>() != _hostDigest)
            throw new InvalidOperationException("The selected runtime probe did not retain its actual host identity.");
        string actualHost = Path.GetFullPath(evidence["hostPath"]!.GetValue<string>());
        if (Hash(actualHost) != _hostDigest) throw new InvalidOperationException("The actual selected runtime host changed.");
        _inputs.TryAdd(actualHost, _hostDigest);
        evidence["selectedHostPath"] = _host;
        JsonArray modules = evidence["modules"]!.AsArray();
        string[] names = modules.Select(module => module!["name"]!.GetValue<string>()).ToArray();
        if (names.Length != 4 || !names.Order(StringComparer.Ordinal).SequenceEqual(Contracts.Append("System.Private.CoreLib").Order(StringComparer.Ordinal)))
            throw new InvalidOperationException("The selected runtime probe did not report the exact contract module set.");
        string frameworkRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location)))! + Path.DirectorySeparatorChar;
        foreach (JsonNode? module in modules)
        {
            string name = module!["name"]!.GetValue<string>();
            Assembly parent = loadedSdk.Keys.Single(assembly => assembly.GetName().Name == name);
            string source = Path.GetFullPath(module["path"]!.GetValue<string>());
            string local = Path.Combine(snapshot, name + ".dll");
            if (source != local && !source.StartsWith(frameworkRoot, StringComparison.Ordinal)
                    && !source.StartsWith(_selectedFrameworkRoot, StringComparison.Ordinal)
                || module["assembly"]!.GetValue<string>() != parent.FullName
                || module["moduleVersionId"]!.GetValue<Guid>() != parent.ManifestModule.ModuleVersionId
                || module["sha256"]!.GetValue<string>() != loadedSdk[parent] || Hash(source) != loadedSdk[parent])
                throw new InvalidOperationException($"Selected child runtime dependency '{name}' differs from the actual prebuild AppHost module.");
            _inputs.TryAdd(source, loadedSdk[parent]);
            if (name == "System.Private.CoreLib") continue;
            if (!File.Exists(local))
            {
                using (var output = new FileStream(local, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await output.WriteAsync(await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(local, UnixFileMode.UserRead);
                identities.Add(name + ".dll", loadedSdk[parent]);
                sources.Add(name + ".dll", source);
            }
            _inputs.TryAdd(local, loadedSdk[parent]);
            _sharedContracts.Add(name, local);
        }
        await VerifyAsync(cancellationToken).ConfigureAwait(false);
        evidence["originalContractInputs"] = JsonSerializer.SerializeToNode(_originalContracts.OrderBy(input => input.Key, StringComparer.Ordinal)
            .Select(input => new { path = input.Key, present = input.Value is not null, sha256 = input.Value }));
        evidence["runtimeConfigSha256"] = Hash(snapshotConfig);
        evidence["depsSha256"] = Hash(snapshotDependencies);
        evidence["probeSha256"] = Hash(probe);
        evidence["frameworkInventorySha256"] = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            _frameworks.OrderBy(framework => framework.Key, StringComparer.Ordinal).Select(framework => new { name = framework.Key, framework.Value.Path, sha256 = framework.Value.Digest }))));
        evidence["runtimeInstallationInputsSha256"] = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            _runtimeDirectories.OrderBy(directory => directory.Key, StringComparer.Ordinal)
                .Select(directory => new { path = directory.Key, files = directory.Value.Select(path => new { path, sha256 = _inputs[path] }) }))));
        evidence["runtimeEnvironmentSha256"] = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(_environment)));
        return evidence;
    }

    /// <summary>Refuses changed host, framework, snapshot, configuration, dependency or runtime-selection inputs.</summary>
    internal async Task VerifyAsync(CancellationToken cancellationToken)
    {
        var current = await RuntimeEnvironmentAsync(_project, cancellationToken).ConfigureAwait(false);
        string host = RequireOrdinaryLaunchHost(_project, current);
        if (host != _host || !_environment.SequenceEqual(current) || _inputs.Any(input => (File.Exists(input.Key) ? Hash(input.Key) : null) != input.Value)
            || _originalContracts.Any(input => (File.Exists(input.Key) ? Hash(input.Key) : null) != input.Value)
            || _runtimeDirectories.Any(directory => !Directory.Exists(directory.Key)
                || !Directory.EnumerateFiles(directory.Key).Order(StringComparer.Ordinal).SequenceEqual(directory.Value, StringComparer.Ordinal)))
            throw new InvalidOperationException("The selected managed runtime inputs changed during owner preparation.");
    }

    /// <summary>Rechecks the same frozen runtime immediately before its selected resource may start.</summary>
    internal void ObserveStarts() => _project.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(async (startup, token) =>
    {
        if (!ReferenceEquals(startup.Resource, _project.Resource)) return;
        await VerifyAsync(token).ConfigureAwait(false);
        var current = await ProbeAsync("runtime-start-" + Guid.NewGuid().ToString("N"), token).ConfigureAwait(false);
        if (!JsonNode.DeepEquals(_probeEvidence, current))
            throw new InvalidOperationException("The selected managed runtime selection changed after owner preparation.");
        await VerifyAsync(token).ConfigureAwait(false);
    });

    /// <summary>Retains every already authenticated owner source and immutable dependency until startup.</summary>
    internal void TrackOwnerInputs(string snapshot, IReadOnlyDictionary<string, string> identities, IReadOnlyDictionary<string, string> sources)
    {
        foreach (var dependency in identities)
        {
            _inputs[sources[dependency.Key]] = dependency.Value;
            _inputs[Path.Combine(snapshot, dependency.Key)] = dependency.Value;
        }
    }

    private async Task<JsonObject> ProbeAsync(string name, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_host) { WorkingDirectory = Path.GetDirectoryName(_probe!), RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        foreach (string key in start.Environment.Keys.Where(IsRuntimeVariable).ToArray()) start.Environment.Remove(key);
        foreach (var variable in _environment) start.Environment[variable.Key] = variable.Value;
        foreach (string argument in new[] { "exec", "--runtimeconfig", _snapshotConfig!, "--depsfile", _snapshotDependencies!, _probe! })
            start.ArgumentList.Add(argument);
        await _runProbeAsync!(start, name, cancellationToken).ConfigureAwait(false);
        string diagnostics = Path.Combine(Path.GetDirectoryName(start.WorkingDirectory)!, name + ".stdout.log");
        if (new FileInfo(diagnostics).Length > 65536) throw new InvalidOperationException("The selected runtime identity exceeded its bound.");
        return JsonNode.Parse(await File.ReadAllTextAsync(diagnostics, cancellationToken).ConfigureAwait(false))!.AsObject();
    }

    private void TrackRuntimeDirectory(string directory)
    {
        string[] files = Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal).ToArray();
        _runtimeDirectories.TryAdd(directory, files);
        foreach (string file in files) Track(file);
    }

    // Aspire 13.6.5's ordinary ProjectExecutableLaunchRecipe invokes literal "dotnet". Its
    // DCP process inherits the AppHost environment. Keep that lookup identical in either process;
    // custom executables and divergent resource host lookup are outside this finite contract.
    private static string RequireOrdinaryLaunchHost(IResourceBuilder<ProjectResource> project,
        SortedDictionary<string, string> environment)
    {
        string inheritedPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (project.Resource.Annotations.OfType<ExecutableAnnotation>().Any()
            || environment.GetValueOrDefault("PATH") != inheritedPath)
            throw new InvalidOperationException("Selected owner custom executable or divergent PATH host overrides are unsupported.");
        string[] paths = inheritedPath.Split(Path.PathSeparator);
        if (paths.Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            throw new InvalidOperationException("Selected owner relative or empty PATH host lookup is unsupported.");
        string executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        string host = paths.Select(path => Path.GetFullPath(Path.Combine(path, executable)))
            .FirstOrDefault(path => File.Exists(path) && (OperatingSystem.IsWindows()
                || (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0))
            ?? throw new InvalidOperationException("The selected dotnet runtime host is unavailable.");
        string? configuredHost = environment.GetValueOrDefault("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(configuredHost)
            && (!Path.IsPathFullyQualified(configuredHost) || !File.Exists(configuredHost)
                || CanonicalPath(configuredHost) != CanonicalPath(host)))
            throw new InvalidOperationException("Selected owner divergent DOTNET_HOST_PATH host overrides are unsupported.");
        return host;
    }

    private static string CanonicalPath(string path) => File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);

    private void Track(string path) => _inputs.TryAdd(path, Hash(path));
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    /// <summary>Only runtime selection and host lookup are resolved; application references remain unevaluated.</summary>
    internal static bool IsRuntimeVariable(string name) => name.Equals("PATH", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("COMPLUS_", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("CORECLR_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("COR_", StringComparison.OrdinalIgnoreCase)
        || IsNativeLoaderVariable(name);

    private static bool IsNativeLoaderVariable(string name) => name.Equals("LD_PRELOAD", StringComparison.OrdinalIgnoreCase)
        || name.Equals("LD_LIBRARY_PATH", StringComparison.OrdinalIgnoreCase) || name.Equals("LD_AUDIT", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("DYLD_", StringComparison.OrdinalIgnoreCase);

    private static async Task<SortedDictionary<string, string>> RuntimeEnvironmentAsync(IResourceBuilder<ProjectResource> project,
        CancellationToken cancellationToken)
    {
        var values = new SortedDictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            if (entry.Key is string name && entry.Value is string value && IsRuntimeVariable(name)) values[name] = value;
        var context = new EnvironmentCallbackContext(project.ApplicationBuilder.ExecutionContext, project.Resource,
            cancellationToken: cancellationToken);
        foreach (var annotation in project.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context).WaitAsync(cancellationToken).ConfigureAwait(false);
        foreach (var variable in context.EnvironmentVariables.Where(variable => IsRuntimeVariable(variable.Key)))
            values[variable.Key] = variable.Value switch
            {
                string value => value,
                IValueProvider provider => await provider.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty,
                null => string.Empty,
                _ => throw new InvalidOperationException("A selected runtime environment input has no supported value provider."),
            };
        foreach (var variable in values)
            if (!string.IsNullOrEmpty(variable.Value) && (variable.Key.Equals("DOTNET_STARTUP_HOOKS", StringComparison.OrdinalIgnoreCase)
                || variable.Key.Equals("DOTNET_ADDITIONAL_DEPS", StringComparison.OrdinalIgnoreCase)
                || variable.Key.Equals("DOTNET_SHARED_STORE", StringComparison.OrdinalIgnoreCase)
                || variable.Key.StartsWith("CORECLR_PROFILER", StringComparison.OrdinalIgnoreCase)
                || variable.Key.StartsWith("COR_PROFILER", StringComparison.OrdinalIgnoreCase)
                || variable.Key.Equals("CORECLR_ENABLE_PROFILING", StringComparison.OrdinalIgnoreCase)
                || variable.Key.Equals("COR_ENABLE_PROFILING", StringComparison.OrdinalIgnoreCase)
                || IsNativeLoaderVariable(variable.Key)))
                throw new InvalidOperationException("Selected owner runtime hooks, additional dependencies, shared stores, profilers or native loader overrides are unsupported.");
        return values;
    }
}
