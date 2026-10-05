using System.Reflection;
using System.Runtime.Loader;

namespace Probe.Host;

/// <summary>Invalidates host readiness on an observed foreign load but cannot interpose on its nested invocation.</summary>
internal sealed class AssemblyObservation : IDisposable
{
    private readonly ProbeAudit _audit;
    private readonly HashSet<Assembly> _baseline;
    private readonly HashSet<string> _managedNames;
    private readonly HashSet<Assembly> _shared;

    /// <summary>Installs an audit listener with the owned fixture pins and trusted prewarmed host baseline.</summary>
    internal AssemblyObservation(ProbeAudit audit, HashSet<Assembly> baseline,
        HashSet<string> managedNames, HashSet<Assembly> shared)
    {
        _audit = audit;
        _baseline = baseline;
        _managedNames = managedNames;
        _shared = shared;
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
    }

    /// <summary>Removes only this fixture listener.</summary>
    public void Dispose() => AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

    private void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        Assembly assembly = args.LoadedAssembly;
        string name = assembly.GetName().Name!;
        string? context = AssemblyLoadContext.GetLoadContext(assembly)?.Name;
        _audit.Record("assembly-load-observed", name + ":" + context);
        if (!_baseline.Contains(assembly) && !_shared.Contains(assembly) && !_managedNames.Contains(name))
        {
            _audit.Invalidate("observed-unmanifested-managed-load:" + name + ":" + context);
        }
    }
}
