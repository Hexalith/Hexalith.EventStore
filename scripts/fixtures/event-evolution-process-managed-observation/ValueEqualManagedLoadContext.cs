using System.Runtime.Loader;

namespace Hexalith.EventStore.Client.Tests;

/// <summary>Reproduces distinct runtime contexts whose user-defined value equality agrees.</summary>
internal sealed class ValueEqualManagedLoadContext() : AssemblyLoadContext("same-context-name", isCollectible: true)
{
    public override bool Equals(object? obj) => obj is ValueEqualManagedLoadContext;

    public override int GetHashCode() => 17;
}
