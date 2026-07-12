using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Extension methods for registering the persisted read-model store.
/// </summary>
public static class ReadModelStoreServiceCollectionExtensions {
    /// <summary>
    /// Registers the DAPR-backed read-model store for domain modules that maintain persisted,
    /// incrementally-updated read models. One <see cref="DaprReadModelStore"/> singleton backs both
    /// <see cref="IReadModelStore"/> and the additive <see cref="IReadModelBatchStore"/>. Requires a
    /// registered <c>DaprClient</c> (e.g. via <c>AddDaprClient</c>).
    /// </summary>
    /// <remarks>
    /// Registration is idempotent (<c>TryAdd</c>). A consumer that pre-registers a custom
    /// <see cref="IReadModelStore"/> keeps it; the batch interface then resolves to that same instance when
    /// it also implements <see cref="IReadModelBatchStore"/>, otherwise to the default DAPR batch store.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureBatch">An optional delegate to tune batch limits and per-store profiles.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreReadModelStore(
        this IServiceCollection services,
        Action<ReadModelBatchOptions>? configureBatch = null) {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ReadModelBatchOptions();
        configureBatch?.Invoke(options);
        options.Validate();
        services.TryAddSingleton<IOptions<ReadModelBatchOptions>>(Options.Create(options));

        services.TryAddSingleton<DaprReadModelStore>();
        services.TryAddSingleton<IReadModelStore>(static sp => sp.GetRequiredService<DaprReadModelStore>());
        services.TryAddSingleton<IReadModelBatchStore>(static sp =>
            sp.GetService<IReadModelStore>() as IReadModelBatchStore
            ?? sp.GetRequiredService<DaprReadModelStore>());
        services.TryAddSingleton<IReadModelConditionalEraser>(static sp =>
            sp.GetService<IReadModelStore>() as IReadModelConditionalEraser
            ?? sp.GetRequiredService<DaprReadModelStore>());
        return services;
    }
}
