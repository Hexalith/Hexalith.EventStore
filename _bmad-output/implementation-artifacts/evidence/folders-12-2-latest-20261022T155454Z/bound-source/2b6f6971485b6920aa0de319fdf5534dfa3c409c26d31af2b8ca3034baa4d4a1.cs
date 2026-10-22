using Hexalith.EventStore.Client.Queries;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Extension methods for registering the protected query cursor codec.
/// </summary>
public static class QueryCursorCodecServiceCollectionExtensions {
    /// <summary>
    /// Registers a Data Protection backed <see cref="IQueryCursorCodec"/> for a domain module that serves
    /// paginated queries. Requires a registered <c>IDataProtectionProvider</c> (present by default in
    /// ASP.NET Core hosts; otherwise add <c>AddDataProtection()</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="purpose">
    /// A stable, domain-unique Data Protection purpose used to isolate this domain's cursors from any
    /// other's (e.g. <c>"Hexalith.Tenants.QueryCursor.v1"</c>). Changing it invalidates outstanding
    /// cursors, which is a safe failure.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreQueryCursorCodec(this IServiceCollection services, string purpose) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        services.TryAddSingleton<IQueryCursorCodec>(sp =>
            new QueryCursorCodec(sp.GetRequiredService<IDataProtectionProvider>(), purpose));
        return services;
    }
}
