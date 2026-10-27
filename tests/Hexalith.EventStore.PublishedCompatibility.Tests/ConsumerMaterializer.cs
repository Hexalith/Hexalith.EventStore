using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Builds independent package Release and exact-tag source Debug consumers outside repository inheritance.</summary>
/// <param name="runtime">The invocation owner.</param>
/// <param name="repository">The owning repository root.</param>
internal sealed class ConsumerMaterializer(QualificationRuntime runtime, string repository)
{
    private string _templates = string.Empty;
    /// <summary>The selected immutable package coordinate.</summary>
    internal const string SelectedVersion = "3.128.2";

    /// <summary>The exact source behind the selected tag and package nuspecs.</summary>
    internal const string SelectedSource = "935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8";

    /// <summary>The Builds revision recorded by the selected tag.</summary>
    internal const string SelectedBuilds = "db897eac9f2819220b32d237109d7255218b46a9";

    /// <summary>The required independently executed package versions.</summary>
    internal static readonly string[] Versions = ["3.70.1", "3.110.0", SelectedVersion];

    /// <summary>The five selected archives, also bound explicitly in each comparison graph.</summary>
    internal static readonly string[] Packages = ["Client", "Contracts", "DomainService", "Server", "ServiceDefaults"];

    /// <summary>Gets independently measured package provenance.</summary>
    internal List<PublishedCoordinate> Coordinates { get; } = [];

    /// <summary>Gets independently materialized executables.</summary>
    internal List<ConsumerBuild> Builds { get; } = [];

    /// <summary>Gets every independently preserved materialization failure.</summary>
    internal List<string> Failures { get; } = [];

    /// <summary>Downloads and verifies all archives before building nine package consumers and three tagged-source consumers.</summary>
    internal async Task MaterializeAsync(bool prerequisiteOnly = false, bool selectedPairOnly = false)
    {
        _templates = Path.Combine(runtime.Root, "consumer-inputs");
        string originalTemplates = Environment.GetEnvironmentVariable("P1R_QUALIFICATION_CONSUMER_INPUTS")
            ?? Path.Combine(repository, "tests", "Hexalith.EventStore.PublishedCompatibility.Tests", "Consumers");
        var templateHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string original in Directory.EnumerateFiles(originalTemplates, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(originalTemplates, original);
            string target = Path.Combine(_templates, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            byte[] bytes = await File.ReadAllBytesAsync(original);
            await File.WriteAllBytesAsync(target, bytes);
            templateHashes.Add(relative, QualificationRuntime.Hash(bytes));
        }
        string executor = (await runtime.RequireAsync("git", ["rev-parse", "HEAD"], repository)).Trim();
        string diff = await runtime.RequireAsync("git", ["diff", "--binary", "HEAD"], repository);
        string untracked = await runtime.RequireAsync("git", ["ls-files", "--others", "--exclude-standard", "-z"], repository);
        var untrackedHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in untracked.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            untrackedHashes.Add(path, QualificationRuntime.Hash(await File.ReadAllBytesAsync(Path.Combine(repository, path))));
        string capturedHead = (await runtime.RequireAsync("git", ["rev-parse", "HEAD"], repository)).Trim();
        string capturedDiff = await runtime.RequireAsync("git", ["diff", "--binary", "HEAD"], repository);
        string capturedUntracked = await runtime.RequireAsync("git", ["ls-files", "--others", "--exclude-standard", "-z"], repository);
        if (capturedHead != executor || capturedDiff != diff || capturedUntracked != untracked)
            throw new InvalidOperationException("Executor inputs changed during capture; no execution provenance can be bound.");
        foreach ((string path, string hash) in untrackedHashes)
            if (QualificationRuntime.Hash(await File.ReadAllBytesAsync(Path.Combine(repository, path))) != hash)
                throw new InvalidOperationException("An untracked executor input changed during capture: " + path);
        foreach ((string path, string hash) in templateHashes)
            if (QualificationRuntime.Hash(await File.ReadAllBytesAsync(Path.Combine(originalTemplates, path))) != hash)
                throw new InvalidOperationException("A consumer template changed during executor capture: " + path);
        string capturedInputsPath = Path.Combine(runtime.Root, "executor-inputs.json");
        await File.WriteAllTextAsync(capturedInputsPath + ".tmp", JsonSerializer.Serialize(new
        {
            executor, diffHash = QualificationRuntime.Hash(System.Text.Encoding.UTF8.GetBytes(diff)), untrackedHashes, templateHashes,
            consumerInputOrigin = Path.GetFullPath(originalTemplates),
            coordinatorHash = QualificationRuntime.Hash(await File.ReadAllBytesAsync(typeof(ConsumerMaterializer).Assembly.Location)),
            builds = (await runtime.RequireAsync("git", ["rev-parse", "HEAD"], Path.Combine(repository, "references", "Hexalith.Builds"))).Trim(),
        }));
        File.Move(capturedInputsPath + ".tmp", capturedInputsPath);
        foreach (string version in prerequisiteOnly || selectedPairOnly ? [SelectedVersion] : Versions)
        {
            try
            {
            string tag = "v" + version;
            string source = (await runtime.RequireAsync("git", ["rev-parse", tag + "^{}"], repository)).Trim();
            string buildsTree = await runtime.RequireAsync("git", ["ls-tree", source, "references/Hexalith.Builds"], repository);
            string builds = buildsTree.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)[2];
            if (version == SelectedVersion && (source != SelectedSource || builds != SelectedBuilds))
                throw new InvalidOperationException("Selected tag/source/Builds does not match the approved tuple.");
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            var assemblyHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            foreach (string package in version == SelectedVersion ? [.. Packages, "Gateway"] : Packages)
            {
                string id = "Hexalith.EventStore." + package;
                string lower = id.ToLowerInvariant();
                byte[] bytes = await http.GetByteArrayAsync($"https://api.nuget.org/v3-flatcontainer/{lower}/{version}/{lower}.{version}.nupkg", TestContext.Current.CancellationToken);
                string archive = Path.Combine(runtime.Root, lower + "." + version + ".nupkg");
                await File.WriteAllBytesAsync(archive, bytes);
                await runtime.RequireAsync("dotnet", ["nuget", "verify", archive, "--all"]);
                using var zip = new ZipArchive(new MemoryStream(bytes));
                ZipArchiveEntry assembly = zip.GetEntry("lib/net10.0/" + id + ".dll")
                    ?? throw new InvalidOperationException("The verified archive lacks its required SDK DLL entry: " + id);
                using var assemblyBytes = new MemoryStream();
                using (Stream assemblyStream = assembly.Open()) await assemblyStream.CopyToAsync(assemblyBytes);
                assemblyHashes.Add(id, QualificationRuntime.Hash(assemblyBytes.ToArray()));
                ZipArchiveEntry nuspec = zip.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
                using Stream stream = nuspec.Open();
                XDocument xml = XDocument.Load(stream);
                XElement metadata = xml.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
                string actualVersion = metadata.Elements().Single(element => element.Name.LocalName == "version").Value;
                XElement repo = metadata.Elements().Single(element => element.Name.LocalName == "repository");
                string archiveSource = repo.Attribute("commit")?.Value ?? throw new InvalidOperationException("Archive lacks repository commit.");
                if (actualVersion != version || repo.Attribute("url")?.Value.TrimEnd('/') != "https://github.com/Hexalith/Hexalith.EventStore")
                    throw new InvalidOperationException("Unexpected archive version or repository.");
                if (version == SelectedVersion && archiveSource != SelectedSource)
                    throw new InvalidOperationException("Selected archive source differs from the approved source.");
                sources.Add(id, archiveSource);
                hashes.Add(id, QualificationRuntime.Hash(bytes));
            }
            Coordinates.Add(new(version, tag, source, builds, sources, hashes, assemblyHashes));
            foreach (string role in new[] { "Host", "Domain", "Probe" })
                await TryBuildAsync(role, version, source: null);
            }
            catch (Exception error) { Failures.Add(version + ": " + error); }
        }
        if (!prerequisiteOnly)
        try
        {
        string exactSource = Path.Combine(runtime.Root, "tagged-source");
        Directory.CreateDirectory(exactSource);
        string sourceTar = Path.Combine(runtime.Root, "tagged-source.tar");
        await runtime.RequireAsync("git", ["archive", "--format=tar", "-o", sourceTar, SelectedSource], repository);
        await runtime.RequireAsync("tar", ["-xf", sourceTar, "-C", exactSource]);
        string buildsDirectory = Path.Combine(exactSource, "references", "Hexalith.Builds");
        Directory.CreateDirectory(buildsDirectory);
        string buildsTar = Path.Combine(runtime.Root, "tagged-builds.tar");
        await runtime.RequireAsync("git", ["archive", "--format=tar", "-o", buildsTar, SelectedBuilds], Path.Combine(repository, "references", "Hexalith.Builds"));
        await runtime.RequireAsync("tar", ["-xf", buildsTar, "-C", buildsDirectory]);
        foreach (string role in new[] { "Host", "Domain", "Probe" })
            await TryBuildAsync(role, SelectedVersion, exactSource);
        }
        catch (Exception error) { Failures.Add("tagged-source: " + error); }
        await File.WriteAllTextAsync(Path.Combine(runtime.Root, "materialized-inputs.json"), JsonSerializer.Serialize(new { Coordinates, Builds, Failures }));
    }

    private async Task TryBuildAsync(string role, string version, string? source)
    {
        try { Builds.Add(await BuildAsync(role, version, source)); }
        catch (Exception error) { Failures.Add(version + "/" + role + "/" + (source is null ? "package" : "source") + ": " + error); }
    }

    /// <summary>Rejects project substitution, wrong versions and unresolved or extra EventStore package edges.</summary>
    internal static void ValidateAssets(string assetsPath, string version, bool source, string? sourceRoot = null, string role = "Probe")
    {
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var required = new HashSet<string>((role == "Host" && version == SelectedVersion ? [.. Packages, "Gateway", "Admin.Abstractions"] : Packages)
            .Select(name => "Hexalith.EventStore." + name), StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty library in assets.RootElement.GetProperty("libraries").EnumerateObject())
        {
            string type = library.Value.GetProperty("type").GetString()!;
            if (!source && type == "project") throw new InvalidOperationException("Published consumer contains a source project edge: " + library.Name);
            if (!library.Name.StartsWith("Hexalith.EventStore.", StringComparison.OrdinalIgnoreCase)) continue;
            string[] coordinate = library.Name.Split('/');
            if (!required.Contains(coordinate[0])) throw new InvalidOperationException("Unexpected EventStore dependency: " + library.Name);
            if (!source && coordinate[1] != version) throw new InvalidOperationException("Mixed EventStore versions: " + library.Name);
            if (source)
            {
                if (sourceRoot is null || type != "project") throw new InvalidOperationException("Tagged EventStore source was substituted by a package.");
                string projectPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(assetsPath))!, library.Value.GetProperty("msbuildProject").GetString()!));
                if (!projectPath.StartsWith(Path.GetFullPath(sourceRoot) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected source dependency outside the tagged tree: " + projectPath);
            }
            if (!names.Add(coordinate[0])) throw new InvalidOperationException("Duplicate EventStore dependency: " + library.Name);
        }
        if (!names.SetEquals(required)) throw new InvalidOperationException("Incomplete EventStore dependency closure for " + role + ": "
            + string.Join(", ", required.Except(names, StringComparer.OrdinalIgnoreCase)));
    }

    private async Task<ConsumerBuild> BuildAsync(string role, string version, string? source)
    {
        string mode = source is null ? "package" : "source";
        string root = Path.Combine(runtime.Root, mode + "-" + version + "-" + role);
        Directory.CreateDirectory(root);
        string templates = _templates;
        foreach (string origin in Directory.EnumerateFiles(Path.Combine(templates, "Shared"), "*.cs"))
            File.Copy(origin, Path.Combine(root, Path.GetFileName(origin)));
        foreach (string origin in Directory.EnumerateFiles(Path.Combine(templates, role)))
            File.Copy(origin, Path.Combine(root, Path.GetFileName(origin)));
        string project = Path.Combine(root, role + ".csproj");
        XDocument xml = XDocument.Load(project);
        if (role == "Host" && version == SelectedVersion)
            xml.Root!.Add(new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Hexalith.EventStore.Gateway"))));
        foreach (XElement reference in xml.Descendants("PackageReference").ToArray())
        {
            string id = reference.Attribute("Include")!.Value;
            if (source is null) reference.SetAttributeValue("Version", "[" + version + "]");
            else reference.ReplaceWith(new XElement("ProjectReference", new XAttribute("Include",
                Path.Combine(source, "src", id, id + ".csproj"))));
        }
        string constants = version == "3.70.1" ? "LEGACY" : version == SelectedVersion ? "SELECTED;FLOOR" : "FLOOR";
        if (role == "Host" && version == SelectedVersion) constants += ";GATEWAY";
        xml.Root!.Add(new XElement("PropertyGroup", new XElement("DefineConstants", constants)));
        xml.Save(project);
        await File.WriteAllTextAsync(Path.Combine(root, "Directory.Build.props"), "<Project><PropertyGroup><NuGetAudit>true</NuGetAudit><NuGetAuditMode>all</NuGetAuditMode><NuGetAuditLevel>low</NuGetAuditLevel><WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "Directory.Build.targets"), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(root, "Directory.Packages.props"), "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "NuGet.Config"), "<configuration><packageSources><clear/><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\"/></packageSources></configuration>");
        string cache = Path.Combine(root, "packages");
        var environment = new Dictionary<string, string> { ["CI"] = "true", ["NUGET_PACKAGES"] = cache };
        string configuration = source is null ? "Release" : "Debug";
        string sdkOrigin = Path.Combine(source ?? repository, "global.json");
        byte[] sdkPolicy = await File.ReadAllBytesAsync(sdkOrigin);
        await File.WriteAllBytesAsync(Path.Combine(root, "global.json"), sdkPolicy);
        string selectedSdk = (await runtime.RequireAsync("dotnet", ["--version"], root, environment: environment)).Trim();
        ValidateSdkPolicy(sdkPolicy, selectedSdk);
        string[] properties = ["-p:UseHexalithProjectReferences=false", "-p:MinVerVersionOverride=" + version];
        await runtime.RequireAsync("dotnet", ["restore", project, "--configfile", Path.Combine(root, "NuGet.Config"), "--packages", cache,
            "--use-lock-file", "-p:Configuration=" + configuration, .. properties], root, TimeSpan.FromMinutes(5), environment);
        string assets = Path.Combine(root, "obj", "project.assets.json");
        ValidateAssets(assets, version, source is not null, source, role);
        await runtime.RequireAsync("dotnet", ["restore", project, "--locked-mode", "--configfile", Path.Combine(root, "NuGet.Config"),
            "--packages", cache, "-p:Configuration=" + configuration, .. properties], root, TimeSpan.FromMinutes(5), environment);
        await runtime.RequireAsync("dotnet", ["build", project, "-c", configuration, "--no-restore", "-m:1", "-nr:false", .. properties],
            root, TimeSpan.FromMinutes(8), environment);
        byte[] finalSdkPolicy = await File.ReadAllBytesAsync(Path.Combine(root, "global.json"));
        if ((await runtime.RequireAsync("dotnet", ["--version"], root, environment: environment)).Trim() != selectedSdk
            || !sdkPolicy.SequenceEqual(finalSdkPolicy))
            throw new InvalidOperationException("The SDK selection or explicit policy changed during the isolated build.");
        using JsonDocument resolved = JsonDocument.Parse(await File.ReadAllTextAsync(assets));
        var resolvedVersions = resolved.RootElement.GetProperty("libraries").EnumerateObject()
            .Where(library => library.Name.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
            .ToDictionary(library => library.Name.Split('/')[0], library => library.Name.Split('/')[1], StringComparer.Ordinal);
        return new(role, version, configuration, project, Path.Combine(root, "bin", configuration, "net10.0", role + ".dll"), cache,
            QualificationRuntime.Hash(await File.ReadAllBytesAsync(assets)),
            QualificationRuntime.Hash(await File.ReadAllBytesAsync(Path.Combine(root, "packages.lock.json"))),
            source is null ? null : SelectedSource,
            source is null ? Coordinates.Single(coordinate => coordinate.Version == version).ArchiveSources : null,
            resolvedVersions, QualificationRuntime.Hash(await File.ReadAllBytesAsync(project)),
            source is null ? Coordinates.Single(coordinate => coordinate.Version == version).AssemblyHashes : null,
            QualificationRuntime.Hash(sdkPolicy), selectedSdk, sdkOrigin);
    }

    /// <summary>Refuses an SDK outside the copied repository's explicit latest-patch policy.</summary>
    internal static void ValidateSdkPolicy(byte[] policy, string selected)
    {
        using JsonDocument json = JsonDocument.Parse(policy);
        JsonElement sdk = json.RootElement.GetProperty("sdk");
        Version required = Version.Parse(sdk.GetProperty("version").GetString()!);
        if (sdk.GetProperty("rollForward").GetString() != "latestPatch" || !Version.TryParse(selected, out Version? actual)
            || actual.Major != required.Major || actual.Minor != required.Minor
            || actual.Build / 100 != required.Build / 100 || actual.Build < required.Build)
            throw new InvalidOperationException("The selected SDK does not satisfy the copied repository latestPatch policy.");
    }
}
