using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Sample.Tests;

internal static class SampleApiCatalogTestServices
{
    internal static void AddSampleApiCommittedCatalog(this IServiceCollection services, int schema = 2)
    {
        services.RemoveAll<IRouteCatalogAuthority>();
        services.AddSingleton<IRouteCatalogAuthority>(new SampleApiCommittedCatalog(schema));
    }
}
