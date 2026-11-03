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
            repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY"),
            revision = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            workflowRunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
            workflowAttempt = Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT"),
            jobId = Environment.GetEnvironmentVariable("GITHUB_JOB"),
            assemblies,
        });
        if (assemblies.Length is 0 or > 64 || Encoding.UTF8.GetByteCount(observation) > 65536)
            throw new InvalidOperationException("The native loaded-assembly observation exceeds its bounded diagnostic contract.");
        output.WriteLine(observation);
    }
}
