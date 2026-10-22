using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Registers convention-named OpenTelemetry instruments for a domain module (Epic A5).
/// </summary>
public static class EventStoreDomainTelemetryExtensions {
    /// <summary>
    /// Registers a singleton <see cref="EventStoreDomainDiagnostics"/> for the domain and wires its convention-named
    /// activity source and meter into the OpenTelemetry tracer/meter providers configured by
    /// <c>AddServiceDefaults</c>. Domain code injects <see cref="EventStoreDomainDiagnostics"/> to emit spans/metrics.
    /// </summary>
    /// <param name="builder">The web application builder (after <c>AddEventStoreDomainService</c>).</param>
    /// <param name="domain">The kebab-case domain name (e.g. <c>"tenants"</c>).</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="domain"/> is <c>null</c> or whitespace.</exception>
    public static WebApplicationBuilder AddEventStoreDomainTelemetry(this WebApplicationBuilder builder, string domain) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        _ = builder.Services.AddEventStoreDomainTelemetry(domain);

        return builder;
    }

    /// <summary>
    /// Registers convention-named diagnostics for one or more domains.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="domains">The domain names.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDomainTelemetry(this IServiceCollection services, params string[] domains)
        => services.AddEventStoreDomainTelemetry((IEnumerable<string>)domains);

    /// <summary>
    /// Registers convention-named diagnostics for one or more domains.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="domains">The domain names.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventStoreDomainTelemetry(this IServiceCollection services, IEnumerable<string> domains) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(domains);

        services.TryAddSingleton(
            static serviceProvider => new EventStoreDomainDiagnosticsRegistry(
                serviceProvider.GetServices<EventStoreDomainTelemetryRegistration>()));
        services.TryAddSingleton(static serviceProvider => {
            EventStoreDomainDiagnosticsRegistry registry = serviceProvider.GetRequiredService<EventStoreDomainDiagnosticsRegistry>();
            if (registry.Domains.Count != 1) {
                throw new InvalidOperationException(
                    "EventStoreDomainDiagnostics can only be injected directly when exactly one domain is registered. "
                    + "Use EventStoreDomainDiagnosticsRegistry or keyed EventStoreDomainDiagnostics services for multi-domain hosts.");
            }

            string domain = registry.Domains.Single();
            return registry.GetDiagnostics(domain)
                ?? throw new InvalidOperationException($"Domain diagnostics for '{domain}' were not registered.");
        });

        foreach (string domain in domains) {
            RegisterDomainTelemetry(services, domain);
        }

        return services;
    }

    private static void RegisterDomainTelemetry(IServiceCollection services, string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        string normalizedDomain = domain.Trim();
        if (IsDomainTelemetryRegistered(services, normalizedDomain)) {
            return;
        }

        _ = services.AddSingleton(new EventStoreDomainTelemetryRegistration(normalizedDomain));
        _ = services.AddKeyedSingleton<EventStoreDomainDiagnostics>(normalizedDomain, (serviceProvider, _) =>
            serviceProvider.GetRequiredService<EventStoreDomainDiagnosticsRegistry>().GetDiagnostics(normalizedDomain)
            ?? throw new InvalidOperationException($"Domain diagnostics for '{normalizedDomain}' were not registered."));

        string sourceName = EventStoreDomainTelemetry.ActivitySourceName(normalizedDomain);
        string meterName = EventStoreDomainTelemetry.MeterName(normalizedDomain);

        _ = services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(sourceName));
        _ = services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(meterName));
    }

    private static bool IsDomainTelemetryRegistered(IServiceCollection services, string domain)
        => services.Any(descriptor => descriptor.ServiceType == typeof(EventStoreDomainTelemetryRegistration)
            && descriptor.ImplementationInstance is EventStoreDomainTelemetryRegistration registration
            && string.Equals(registration.Domain, domain, StringComparison.OrdinalIgnoreCase));
}
