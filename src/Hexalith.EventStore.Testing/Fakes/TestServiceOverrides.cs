
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Shared helper for replacing DAPR-dependent services with test fakes in integration tests.
/// </summary>
public static class TestServiceOverrides {
    /// <summary>
    /// Replaces ICommandRouter with a FakeCommandRouter that does not require DAPR actor infrastructure.
    /// </summary>
    public static void ReplaceCommandRouter(IServiceCollection services, FakeCommandRouter? router = null) {
        ArgumentNullException.ThrowIfNull(services);
        ServiceDescriptor? routerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ICommandRouter));
        if (routerDescriptor is not null) {
            _ = services.Remove(routerDescriptor);
        }

        _ = services.AddSingleton<ICommandRouter>(router ?? new FakeCommandRouter());
    }

    /// <summary>
    /// Removes all Dapr health check registrations that require a running sidecar.
    /// Call this in WebApplicationFactory tests where no Dapr sidecar is available.
    /// </summary>
    public static void RemoveDaprHealthChecks(IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.Configure<HealthCheckServiceOptions>(options => {
            var daprChecks = options.Registrations
                .Where(r => r.Name.StartsWith("dapr-", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (HealthCheckRegistration check in daprChecks) {
                _ = options.Registrations.Remove(check);
            }
        });
    }
}
