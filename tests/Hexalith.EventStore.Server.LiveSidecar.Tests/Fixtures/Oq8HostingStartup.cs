using Hexalith.EventStore.DomainService;
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

            string catalogJson = Environment.GetEnvironmentVariable(LiveAdmissionCatalogBinding.EnvironmentVariable)
                ?? throw new InvalidOperationException("The private live-admission worker catalog binding is required.");
            var catalog = System.Text.Json.JsonSerializer.Deserialize<LiveAdmissionCatalogBinding>(catalogJson)
                ?? throw new InvalidOperationException("The private live-admission catalog binding is invalid.");
            if (Environment.GetEnvironmentVariable("DAPR_APP_ID") != catalog.AppId)
                throw new InvalidOperationException("The private catalog owner differs from the independently configured worker application.");
            services.AddLiveAdmissionCatalog(catalog);

            services.TryAddSingleton<TimeProvider, Oq8FileTimeProvider>();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IIdempotencyIntentAdapter, LiveIncrementCounterIdempotencyIntentAdapter>());
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IStartupFilter, Oq8BoundaryCounterStartupFilter>());
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HEXALITH_OQ8_DIAGNOSTICS_PATH")))
            {
                services.TryAddTransient<Oq8InvocationDiagnosticHandler>();
                _ = services.AddHttpClient("domain-service-invocation")
                    .AddHttpMessageHandler<Oq8InvocationDiagnosticHandler>();
            }
        });
    }
}
