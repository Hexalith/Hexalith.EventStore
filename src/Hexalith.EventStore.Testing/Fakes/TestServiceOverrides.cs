
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Shared helper for replacing DAPR-dependent services with test fakes in integration tests.
/// </summary>
public static class TestServiceOverrides {
    /// <summary>
    /// Replaces the command-routing path with fakes that do not require DAPR actor or state-store infrastructure.
    /// </summary>
    public static void ReplaceCommandRouter(IServiceCollection services, FakeCommandRouter? router = null) {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<ICommandRouter>();
        _ = services.AddSingleton<ICommandRouter>(router ?? new FakeCommandRouter());

        // Projection activation is a mandatory write-ahead step before ICommandRouter is invoked.
        // A fake router alone therefore no longer makes WebApplicationFactory command tests
        // independent of DAPR. Replace the outbox as part of the same test boundary so callers
        // cannot accidentally exercise localhost:3500 while believing the route is in-memory.
        services.RemoveAll<IProjectionActivationOutbox>();
        _ = services.AddSingleton<IProjectionActivationOutbox>(NoOpProjectionActivationOutbox.Instance);
    }

    /// <summary>
    /// Removes all Dapr health check registrations that require a running sidecar.
    /// Call this in WebApplicationFactory tests where no Dapr sidecar is available.
    /// </summary>
    public static void RemoveDaprHealthChecks(IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.PostConfigure<HealthCheckServiceOptions>(options => {
            var daprChecks = options.Registrations
                .Where(r => r.Name.StartsWith("dapr-", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        r.Name,
                        "projection-delivery-writer-protocol",
                        StringComparison.Ordinal))
                .ToList();
            foreach (HealthCheckRegistration check in daprChecks) {
                _ = options.Registrations.Remove(check);
            }
        });
    }
}
