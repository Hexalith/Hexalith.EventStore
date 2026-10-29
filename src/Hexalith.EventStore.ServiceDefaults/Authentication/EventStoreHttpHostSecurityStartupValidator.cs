using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Fails before listening when a conforming host has unclassified endpoints or embeds an issuer.</summary>
internal sealed class EventStoreHttpHostSecurityStartupValidator(
    EventStoreHttpEndpointSource source,
    IServiceProvider services,
    IConfiguration configuration) : IHostedLifecycleService
{
    /// <inheritdoc />
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (configuration.GetValue<bool>(WorkloadAuthorityOptions.SectionName + ":Enabled"))
        {
            throw new InvalidOperationException("Conforming HTTP hosts cannot activate the embedded workload authority.");
        }
        if (source.Endpoints is null)
        {
            throw new InvalidOperationException("Shared HTTP security requires UseEventStoreHttpHostSecurity() enrollment before startup.");
        }
        IReadOnlyList<string> failures = await EventStoreHttpEndpointInventory.ValidateAsync(
            source.Endpoints.DataSources.SelectMany(static item => item.Endpoints), services).ConfigureAwait(false);
        if (failures.Count > 0)
        {
            throw new InvalidOperationException("HTTP endpoint inventory failed: " + string.Join(" ", failures));
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
