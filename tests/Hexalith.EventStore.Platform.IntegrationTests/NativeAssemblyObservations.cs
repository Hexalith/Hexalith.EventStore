namespace Hexalith.EventStore.Platform.IntegrationTests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>Retains bounded identities of SDK assemblies already loaded by the real native test.</summary>
internal static class NativeAssemblyObservations
{
    /// <summary>Writes actual test-process measurements after the persistence assertions have passed.</summary>
    /// <param name="output">The native TRX diagnostic channel.</param>
    /// <param name="ownerRunId">The validated descriptor's actual owner run.</param>
    internal static void Write(ITestOutputHelper output, string ownerRunId)
    {
        NativeDiagnosticBinding binding = NativeDiagnosticBinding.Read(ownerRunId);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && (assembly.GetName().Name!.StartsWith("Hexalith.", StringComparison.Ordinal)
                || assembly.GetName().Name == "StackExchange.Redis"))
            .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .Take(65)
            .Select(assembly =>
            {
                using FileStream bytes = File.OpenRead(assembly.Location);
                return new
                {
                    identity = assembly.FullName,
                    informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                    moduleVersionId = assembly.ManifestModule.ModuleVersionId,
                    loadedPath = assembly.Location,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                };
            }).ToArray();
        string observation = JsonSerializer.Serialize(new
        {
            kind = "real-native-test-loaded-assemblies",
            ownerRunId,
            ownerAttemptId = binding.AttemptId,
            environmentId = binding.EnvironmentId,
            revision = binding.Revision,
            mappingHash = binding.MappingHash,
            executionContext = binding.ExecutionContext,
            assemblies,
        });
        if (assemblies.Length is 0 or > 64 || Encoding.UTF8.GetByteCount(observation) > 65536)
            throw new InvalidOperationException("The native loaded-assembly observation exceeds its bounded diagnostic contract.");
        output.WriteLine(observation);
        RetainHostedObservation(observation, binding);
    }

    /// <summary>Preserves the bounded diagnostic in the existing classified integration upload before teardown.</summary>
    /// <param name="observation">The measured test-process assemblies and actual run bindings.</param>
    /// <param name="binding">The actual descriptor, mapping and owner metadata binding.</param>
    private static void RetainHostedObservation(string observation, NativeDiagnosticBinding binding)
    {
        if (binding.ExecutionContext.GetProperty("kind").GetString() == "local") return;
        if (binding.ExecutionContext.GetProperty("kind").GetString() != "ci" || !OperatingSystem.IsLinux())
            throw new InvalidDataException("Native diagnostic retention requires the actual CI owner metadata.");
        binding.VerifyCallerArtifact();
        WritePrivateObservation(binding.ArtifactDirectory, binding.RunId, observation);
    }

    /// <summary>Writes once through non-linked directories using private file creation.</summary>
    /// <param name="directory">The existing classified output directory.</param>
    /// <param name="ownerRunId">The descriptor-bound diagnostic filename suffix.</param>
    /// <param name="observation">The previously bounded diagnostic JSON.</param>
    internal static void WritePrivateObservation(string directory, string ownerRunId, string observation)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Hosted native diagnostic retention requires Linux.");
        for (DirectoryInfo? parent = new(directory); parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null) throw new IOException("Native diagnostic output cannot traverse symbolic links.");
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        };
        using FileStream stream = new(Path.Combine(directory, "loaded-assemblies-" + ownerRunId + ".json"), options);
        stream.Write(Encoding.UTF8.GetBytes(observation + "\n"));
    }

}
