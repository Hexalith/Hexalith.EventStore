using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Aspire;

/// <summary>Loads one selected child with its own dependency resolver, sharing only identical SDK contracts.</summary>
internal sealed class DevelopmentRouteCatalogOwnerLoadContext(string targetPath, IReadOnlyDictionary<Assembly, string> loadedSdk) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(targetPath);

    /// <summary>Freezes the files that correspond to the SDK modules actually loaded before any child build.</summary>
    internal static IReadOnlyDictionary<Assembly, string> CaptureLoadedSdkIdentity() => Default.Assemblies
        .Where(assembly => assembly.GetName().Name?.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) == true)
        .ToDictionary(assembly => assembly, LoadedSdkDigest);

    /// <summary>Compares the actual file module ID to its loaded image before accepting its content hash.</summary>
    internal static string LoadedSdkDigest(Assembly assembly)
    {
        if (string.IsNullOrEmpty(assembly.Location)) throw new InvalidOperationException("The actual loaded SDK image has no file identity.");
        byte[] bytes = File.ReadAllBytes(assembly.Location);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        if (metadata.GetGuid(metadata.GetModuleDefinition().Mvid) != assembly.ManifestModule.ModuleVersionId)
            throw new InvalidOperationException($"The SDK file '{assembly.GetName().Name}' differs from its actual loaded module.");
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private string SharedDigest(Assembly shared)
    {
        string digest = LoadedSdkDigest(shared);
        if (loadedSdk.TryGetValue(shared, out string? beforeBuild) && digest != beforeBuild)
            throw new InvalidOperationException($"The actual loaded SDK file '{shared.GetName().Name}' changed during selected child preparation.");
        return digest;
    }

    /// <summary>Rejects shared SDK byte drift before evaluating any owner declaration source.</summary>
    internal void VerifySharedSdkIdentity()
    {
        foreach (Assembly shared in Default.Assemblies.Where(assembly => assembly.GetName().Name?.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) == true))
        {
            string digest = SharedDigest(shared);
            string dependency = Path.Combine(Path.GetDirectoryName(targetPath)!, shared.GetName().Name + ".dll");
            if (File.Exists(dependency) && Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(dependency))) != digest)
                throw new InvalidOperationException($"Selected child SDK dependency '{shared.GetName().Name}' differs from the actual AppHost SDK.");
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        Assembly? shared = Default.Assemblies.SingleOrDefault(assembly => assembly.GetName().Name == assemblyName.Name);
        if (shared is not null && assemblyName.Name?.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal) == true)
        {
            if (path is null || shared.GetName().FullName != assemblyName.FullName
                || string.IsNullOrEmpty(shared.Location)
                || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != SharedDigest(shared))
                throw new InvalidOperationException($"Selected child SDK dependency '{assemblyName.Name}' differs from the actual AppHost SDK.");
            return shared;
        }
        if (path is not null)
        {
            if (!Path.GetFullPath(path).StartsWith(Path.GetDirectoryName(targetPath)! + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException($"Selected child dependency '{assemblyName.Name}' escaped its immutable output snapshot.");
            return LoadFromAssemblyPath(path);
        }
        Assembly framework = Default.LoadFromAssemblyName(assemblyName);
        string sharedFrameworkRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(object).Assembly.Location)))! + Path.DirectorySeparatorChar;
        if (string.IsNullOrEmpty(framework.Location) || !framework.Location.StartsWith(sharedFrameworkRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"Selected child dependency '{assemblyName.Name}' is absent from its immutable output snapshot.");
        return framework;
    }
}
