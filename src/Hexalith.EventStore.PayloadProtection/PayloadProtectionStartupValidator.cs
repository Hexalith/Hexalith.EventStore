// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// Base authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e, sections 5.2-5.3; on-premises supplement sections 1-2, 6-7.
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Fails closed at the explicit qualification/capacity gate; it never resolves a backend on omitted or disabled registration.</summary>
internal sealed class PayloadProtectionStartupValidator(IServiceProvider services, PayloadProtectionOptions options, IServiceCollection descriptors) : IHostedService
{
    /// <summary>Checks single-service semantics and environment without activating an unqualified provider.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Enabled)
        {
            ServiceDescriptor[] registered = descriptors.Where(d => d.ServiceType == typeof(IEventPayloadProtectionService)).ToArray();
            Type? concrete = registered.Length == 1 ? registered[0].ImplementationType ?? registered[0].ImplementationInstance?.GetType() : null;
            if (concrete?.FullName != "Hexalith.EventStore.Server.Events.NoOpEventPayloadProtectionService"
                || concrete.Assembly.GetName().Name != "Hexalith.EventStore.Server") { throw Invalid(); }
            if (options.StaleBackendConfiguration && services.GetService<ILogger<PayloadProtectionStartupValidator>>() is { } logger)
            { PayloadProtectionRegistrationLog.DisabledConfiguration(logger); }
            return Task.CompletedTask;
        }
        if (!AesGcm.IsSupported || (options.Backend == "LocalDevelopment" && services.GetService<IHostEnvironment>()?.IsDevelopment() != true)) { throw Invalid(); }
        // Platform conformance, PF-02 profile/shared namespace, capacity and current fleet authority are separate prerequisites.
        // No public configuration or test double can turn this first-phase guard into Server host activation.
        throw Invalid();
    }

    /// <summary>Stops without contacting any provider or changing durable authority.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    private static InvalidOperationException Invalid() => new("Payload protection startup admission is unavailable.");
}
