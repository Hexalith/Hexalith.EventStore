using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Compares runtime loader objects by reference and assembly identity text ordinally.</summary>
internal sealed class EventManagedAssemblyBindingComparer : IEqualityComparer<(AssemblyLoadContext Context, string FullName)>
{
    /// <inheritdoc/>
    public bool Equals((AssemblyLoadContext Context, string FullName) x, (AssemblyLoadContext Context, string FullName) y)
        => ReferenceEquals(x.Context, y.Context) && string.Equals(x.FullName, y.FullName, StringComparison.Ordinal);

    /// <inheritdoc/>
    public int GetHashCode((AssemblyLoadContext Context, string FullName) obj)
        => HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Context), StringComparer.Ordinal.GetHashCode(obj.FullName));
}
