using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

internal static class LiveAdmissionCatalogRegistration
{
    internal static void AddLiveAdmissionCatalog(this IServiceCollection services, LiveAdmissionCatalogBinding binding)
    {
        binding.Validate();
        services.AddHttpContextAccessor();
        services.Configure<RouteCatalogHostOptions>(options =>
        {
            options.Identity = binding.Identity;
            options.Profile = binding.Profile;
            options.DevelopmentFixture = false;
        });
        services.AddSingleton(binding);
        services.Replace(ServiceDescriptor.Singleton<IRouteCatalogAuthority, LiveAdmissionRouteCatalog>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, LiveAdmissionCatalogStartupFilter>());
    }
}
