using System.Reflection;
using System.Runtime.Loader;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Loads the genuine previous SDK pair without unifying their contracts with the candidate process.</summary>
internal sealed class PreviousRouteCatalogPeerLoadContext(string directory) : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name is "Hexalith.EventStore.Contracts" or "Hexalith.EventStore.ServiceDefaults")
            return LoadFromAssemblyPath(Path.Combine(directory, name.Name + ".dll"));
        if (name.Name?.StartsWith("Hexalith.EventStore", StringComparison.Ordinal) == true)
            throw new FileLoadException("The previous peer must never resolve a candidate SDK assembly.", name.Name);
        return null;
    }
}
