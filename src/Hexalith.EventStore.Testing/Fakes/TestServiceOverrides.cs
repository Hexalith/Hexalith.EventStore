namespace Hexalith.EventStore.Testing.Fakes;

using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared helper for replacing DAPR-dependent services with test fakes in integration tests.
/// </summary>
public static class TestServiceOverrides
{
    /// <summary>
    /// Replaces ICommandRouter with a FakeCommandRouter that does not require DAPR actor infrastructure.
    /// </summary>
    public static void ReplaceCommandRouter(IServiceCollection services, FakeCommandRouter? router = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ServiceDescriptor? routerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ICommandRouter));
        if (routerDescriptor is not null)
        {
            services.Remove(routerDescriptor);
        }

        services.AddSingleton<ICommandRouter>(router ?? new FakeCommandRouter());
    }
}
