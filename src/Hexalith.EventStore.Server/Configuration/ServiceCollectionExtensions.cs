namespace Microsoft.Extensions.DependencyInjection;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration extension methods for the EventStore Server components.
/// </summary>
public static class EventStoreServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers EventStore Server services including command routing and DAPR actor infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddEventStoreServer(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<ICommandRouter, CommandRouter>();
        services.TryAddSingleton<IDomainServiceResolver, DomainServiceResolver>();
        services.TryAddTransient<IDomainServiceInvoker, DaprDomainServiceInvoker>();
        services.Configure<DomainServiceOptions>(configuration.GetSection("EventStore:DomainServices"));
        services.AddActors(options =>
        {
            options.Actors.RegisterActor<AggregateActor>();
        });

        return services;
    }
}
