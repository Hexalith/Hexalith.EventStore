using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Adds test-only OQ8 seams to independently launched production host binaries.</summary>
public sealed class Oq8HostingStartup : IHostingStartup
{
    /// <inheritdoc/>
    public void Configure(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ConfigureServices(services =>
        {
            if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(Oq8FileTimeProvider.ClockFileEnvironmentVariable)))
            {
                return;
            }

            services.TryAddSingleton<TimeProvider, Oq8FileTimeProvider>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IIdempotencyIntentAdapter, LiveIncrementCounterIdempotencyIntentAdapter>());
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IStartupFilter, Oq8BoundaryCounterStartupFilter>());
        });
    }
}
