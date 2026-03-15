using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.CommandApi.SignalR;

/// <summary>
/// DI registration extension methods for EventStore SignalR integration.
/// </summary>
public static class SignalRServiceCollectionExtensions {
    /// <summary>
    /// Registers EventStore SignalR services for real-time projection change notifications.
    /// When enabled, overrides the default <see cref="IProjectionChangedBroadcaster"/> with the SignalR implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddEventStoreSignalR(this IServiceCollection services, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection("EventStore:SignalR");
        services.TryAddSingleton<IValidateOptions<SignalROptions>, ValidateSignalROptions>();
        _ = services.AddOptions<SignalROptions>()
            .Bind(section)
            .ValidateOnStart();

        Microsoft.AspNetCore.SignalR.ISignalRServerBuilder signalRBuilder = services.AddSignalR();

        SignalROptions? options = section.Get<SignalROptions>();
        if (options?.Enabled != true) {
            return services;
        }

        // Redis backplane for multi-instance deployments
        ConfigureBackplane(signalRBuilder, options);

        // Override no-op broadcaster with SignalR implementation
        _ = services.AddSingleton<IProjectionChangedBroadcaster, SignalRProjectionChangedBroadcaster>();

        return services;
    }

    private static void ConfigureBackplane(Microsoft.AspNetCore.SignalR.ISignalRServerBuilder builder, SignalROptions options) {
        string? redis = options.BackplaneRedisConnectionString
            ?? Environment.GetEnvironmentVariable("EVENTSTORE_SIGNALR_REDIS");

        if (!string.IsNullOrWhiteSpace(redis)) {
            _ = builder.AddStackExchangeRedis(redis);
        }
    }
}
