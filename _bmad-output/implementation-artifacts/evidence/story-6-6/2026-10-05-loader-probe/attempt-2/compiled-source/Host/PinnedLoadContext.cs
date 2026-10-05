using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace Probe.Host;

/// <summary>Models the proposed strict per-context managed/native resolution hooks for this isolated fixture.</summary>
internal sealed class PinnedLoadContext : AssemblyLoadContext
{
    private readonly IReadOnlyDictionary<string, PinnedAssembly> _managed;
    private readonly IReadOnlyDictionary<string, Assembly> _sharedFramework;
    private readonly ProbeAudit _audit;

    /// <summary>Creates a collectible context with exact managed pins and an explicit shared-framework allowlist.</summary>
    internal PinnedLoadContext(IReadOnlyDictionary<string, PinnedAssembly> managed,
        IReadOnlyDictionary<string, Assembly> sharedFramework, ProbeAudit audit)
        : base("fixture-pinned-candidate", isCollectible: true)
    {
        _managed = managed;
        _sharedFramework = sharedFramework;
        _audit = audit;
    }

    /// <summary>Gets how often a managed request reached the candidate's override.</summary>
    internal int ManagedResolutionCalls { get; private set; }

    /// <summary>Gets how often an unmanaged request reached the candidate's override.</summary>
    internal int NativeResolutionCalls { get; private set; }

    /// <summary>Admits a root by exact content and identity before loading it into the candidate context.</summary>
    internal Assembly LoadPinnedRoot(string name)
    {
        PinnedAssembly pin = _managed[name];
        RequirePin(pin);
        _audit.Record("pinned-root-load", name);
        return LoadFromAssemblyPath(pin.Path);
    }

    /// <summary>Resolves only exact declared fixture rows or the closed shared-framework allowlist.</summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        ManagedResolutionCalls++;
        string name = assemblyName.Name ?? throw new FileLoadException("Missing assembly identity.");
        _audit.Record("candidate-managed-resolution", name);
        if (_sharedFramework.TryGetValue(name, out Assembly? framework))
        {
            if (!AssemblyName.ReferenceMatchesDefinition(assemblyName, framework.GetName()))
            {
                _audit.Invalidate("shared-framework-identity:" + name);
                throw new FileLoadException("CapabilityMismatch: shared framework identity differs.");
            }

            return framework;
        }

        if (!_managed.TryGetValue(name, out PinnedAssembly? pin))
        {
            _audit.Invalidate("unmanifested-managed-resolution:" + name);
            throw new FileLoadException("CapabilityMismatch: undeclared managed dependency " + name);
        }

        RequirePin(pin);
        return LoadFromAssemblyPath(pin.Path);
    }

    /// <summary>Refuses every ordinary native dependency because this fixture has no native manifest rows.</summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        NativeResolutionCalls++;
        _audit.Record("candidate-native-resolution", unmanagedDllName);
        _audit.Invalidate("unmanifested-native-resolution:" + unmanagedDllName);
        throw new DllNotFoundException("CapabilityMismatch: undeclared native dependency " + unmanagedDllName);
    }

    private void RequirePin(PinnedAssembly pin)
    {
        string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pin.Path)));
        if (!string.Equals(actual, pin.Sha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(AssemblyName.GetAssemblyName(pin.Path).Name, pin.Name, StringComparison.Ordinal))
        {
            _audit.Invalidate("managed-pin-mismatch:" + pin.Name);
            throw new FileLoadException("CapabilityMismatch: managed content or identity differs.");
        }

        _audit.Record("managed-pin-verified", pin.Name + ":" + actual);
    }
}
