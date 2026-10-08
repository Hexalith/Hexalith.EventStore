using System.Reflection;
using System.Runtime.Loader;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Resolves managed dependencies only through its retained-image composition.</summary>
/// <remarks>Explicit path/stream loads can bypass this hook and still require qualified observations.</remarks>
/// <param name="contextId">The common declared identity for privately loaded managed images.</param>
/// <param name="resolve">The composition's exact declared managed resolution.</param>
/// <param name="capabilityLoss">The shared sticky policy-loss boundary.</param>
internal sealed class EventManagedArtifactLoadContext(string contextId, Func<AssemblyName, Assembly> resolve,
    EventEvolutionCapabilityLoss capabilityLoss) : AssemblyLoadContext(contextId, isCollectible: true)
{
    /// <inheritdoc/>
    protected override Assembly Load(AssemblyName assemblyName) => resolve(assemblyName);

    /// <inheritdoc/>
    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        capabilityLoss.ObserveViolation();
        // Never return zero: that would allow the runtime's native probing fallback.
        throw new InvalidOperationException("CapabilityMismatch: this private managed composition admits no native load route.");
    }
}
