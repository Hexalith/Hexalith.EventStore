using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.CatalogInvocation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Explicit private Debug/Development synthetic-primary qualification; ordinary native streams retain actor authority.</summary>
public static class SyntheticOriginalSourceServiceCollectionExtensions
{
    /// <summary>Registers the finite owner source only for the exact selected private run and catalog.</summary>
    public static IServiceCollection AddPrivateSyntheticOriginalSource(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        const string prefix = "EventStore:Testing:OriginalSource";
        if (!configuration.GetValue<bool>(prefix + ":Enabled")) return services;
#if !DEBUG
        throw new InvalidOperationException("Synthetic original-primary fixture requires Debug.");
#else
        var selection = configuration.GetSection(prefix).Get<SyntheticOriginalSourceOptions>()
            ?? throw new InvalidOperationException("Synthetic original-primary selection is missing.");
        if (!environment.IsDevelopment() || selection.Mode != SyntheticOriginalSourceOptions.SelectedMode
            || selection.RunId.Length != 16 || selection.RunId.Any(character => !char.IsAsciiHexDigitLower(character))
            || selection.Tenant != "durability-" + selection.RunId || selection.Domain != "folders"
            || selection.AggregatePrefix != "capacity-"
            || configuration["Authentication:WorkloadIssuer:Workload"] != "eventstore-" + selection.RunId)
            throw new InvalidOperationException("Synthetic original-primary fixture selection refused.");
        _ = new CatalogInvocationFiles(selection.Directory, SyntheticOriginalSourceStore.MaximumBytes);
        services.AddSingleton(provider =>
        {
            if (provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value is not
                { DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true })
                throw new InvalidOperationException("Synthetic original-primary fixture requires the actual private Development catalog.");
            return new SyntheticOriginalSourceStore(selection);
        });
        services.AddSingleton<INativeSourceScanSource>(provider => provider.GetRequiredService<SyntheticOriginalSourceStore>());
        services.AddSingleton(selection);
        services.AddHostedService<SyntheticOriginalSourceWorker>();
        return services;
#endif
    }
}
