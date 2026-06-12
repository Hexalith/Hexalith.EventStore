using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Registration extensions for the optional domain-service pre-commit admission chain.
/// </summary>
public static class EventStoreDomainAdmissionServiceCollectionExtensions {
    /// <summary>
    /// Registers an admission stage. Multiple stages execute in the same order they are registered in
    /// <see cref="IServiceCollection"/>, and execution stops at the first rejected result.
    /// </summary>
    /// <typeparam name="TStage">The admission stage type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDomainAdmissionStage<TStage>(this IServiceCollection services)
        where TStage : class, IDomainServiceAdmissionStage {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddScoped<IDomainServiceAdmissionStage, TStage>();
    }

    /// <summary>
    /// Registers an admission stage factory. Multiple stages execute in the same order they are registered in
    /// <see cref="IServiceCollection"/>, and execution stops at the first rejected result.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="implementationFactory">The stage factory.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDomainAdmissionStage(
        this IServiceCollection services,
        Func<IServiceProvider, IDomainServiceAdmissionStage> implementationFactory) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationFactory);
        return services.AddScoped(implementationFactory);
    }

    /// <summary>
    /// Registers an admission stage on a domain-service host builder. Multiple stages execute in the same order
    /// they are registered, and execution stops at the first rejected result.
    /// </summary>
    /// <typeparam name="TStage">The admission stage type.</typeparam>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static WebApplicationBuilder AddEventStoreDomainAdmissionStage<TStage>(this WebApplicationBuilder builder)
        where TStage : class, IDomainServiceAdmissionStage {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.Services.AddEventStoreDomainAdmissionStage<TStage>();
        return builder;
    }
}
