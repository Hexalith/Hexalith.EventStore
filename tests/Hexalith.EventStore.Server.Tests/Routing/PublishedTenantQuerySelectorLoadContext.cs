using System.Reflection;
using System.Runtime.Loader;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Loads only the pinned historical Tenants fixture privately, binding its SDK contracts to the newly built default cohort.</summary>
internal sealed class PublishedTenantQuerySelectorLoadContext() : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly? Load(AssemblyName name) => Default.LoadFromAssemblyName(name);
}
