using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry.Instrumentation.AspNetCore;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Installs source-side read privacy in both standard telemetry and independently composed SDK HTTP hosts.</summary>
public static class ReadDiagnosticsPrivacyExtensions
{
    /// <summary>Protects the shared read surface before authentication, routing, exception handling, or domain work.</summary>
    /// <param name="services">The host's real service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddEventStoreReadDiagnosticsPrivacy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(static descriptor => descriptor.ImplementationType == typeof(ReadDiagnosticsPrivacyStartupFilter)))
        {
            return services;
        }

        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Transient<IOptionsFactory<LoggerFilterOptions>, RequestLogPrivacyOptionsFactory>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, ReadDiagnosticsPrivacyStartupFilter>());
        services.PostConfigure<AspNetCoreTraceInstrumentationOptions>(static options =>
        {
            Func<HttpContext, bool>? existing = options.Filter;
            options.Filter = context => !ReadDiagnosticsPrivacyScope.IsActive && !ReadDiagnosticsPrivacy.IsProtectedExecution(context)
                && (existing?.Invoke(context) ?? true);
        });
        return services;
    }
}
