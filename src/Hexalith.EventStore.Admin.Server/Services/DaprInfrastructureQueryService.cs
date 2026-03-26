using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IDaprInfrastructureQueryService"/>.
/// Uses DaprClient.GetMetadataAsync() as the sole data source.
/// </summary>
public sealed class DaprInfrastructureQueryService : IDaprInfrastructureQueryService
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprInfrastructureQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprInfrastructureQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprInfrastructureQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        ILogger<DaprInfrastructureQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DaprComponentDetail>> GetComponentsAsync(CancellationToken ct = default)
    {
        DaprMetadata metadata;
        try
        {
            metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DAPR sidecar metadata unavailable — cannot list components.");
            return [];
        }

        if (metadata?.Components is null || metadata.Components.Count == 0)
        {
            return [];
        }

        DaprComponentDetail[] components;
        try
        {
            components = metadata.Components
                .Where(c => !string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.Type))
                .Select(c => new DaprComponentDetail(
                    c.Name,
                    c.Type,
                    DaprComponentCategoryHelper.FromComponentType(c.Type),
                    c.Version ?? string.Empty,
                    HealthStatus.Healthy,
                    DateTimeOffset.UtcNow,
                    c.Capabilities ?? []))
                .ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to map DAPR component metadata.");
            return [];
        }

        // Run health probes for state store components in parallel
        using CancellationTokenSource probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(TimeSpan.FromSeconds(3));

        List<Task> probes = [];
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].Category == DaprComponentCategory.StateStore)
            {
                probes.Add(ProbeStateStoreAsync(components, i, probeCts.Token));
            }
        }

        if (probes.Count > 0)
        {
            try
            {
                await Task.WhenAll(probes).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Probe timeout — mark remaining probed components as Degraded
                _logger.LogWarning("State store health probes timed out after 3 seconds.");
            }
        }

        return components;
    }

    /// <inheritdoc/>
    public async Task<DaprSidecarInfo?> GetSidecarInfoAsync(CancellationToken ct = default)
    {
        try
        {
            DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
            if (metadata is null)
            {
                return null;
            }

            // DAPR SDK 1.16.1 exposes Id, Components, Actors, Extended.
            // RuntimeVersion, Subscriptions, HttpEndpoints are not available in this SDK version.
            string runtimeVersion = metadata.Extended?.TryGetValue("daprRuntimeVersion", out string? version) == true
                ? version ?? "unknown"
                : "unknown";

            return new DaprSidecarInfo(
                string.IsNullOrWhiteSpace(metadata.Id) ? "unknown" : metadata.Id,
                runtimeVersion,
                metadata.Components?.Count ?? 0,
                0,  // Subscriptions not exposed in SDK 1.16.1
                0); // HttpEndpoints not exposed in SDK 1.16.1
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DAPR sidecar metadata unavailable — cannot get sidecar info.");
            return null;
        }
    }

    private async Task ProbeStateStoreAsync(
        DaprComponentDetail[] components,
        int index,
        CancellationToken ct)
    {
        DaprComponentDetail component = components[index];
        try
        {
            _ = await _daprClient
                .GetStateAsync<string>(component.ComponentName, "admin:dapr-probe", cancellationToken: ct)
                .ConfigureAwait(false);

            // Success (null return = key missing, but store responded) → Healthy
        }
        catch (OperationCanceledException)
        {
            // Probe timed out or was cancelled — mark as Degraded (inconclusive)
            components[index] = component with { Status = HealthStatus.Degraded, LastCheckUtc = DateTimeOffset.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "State store probe failed for {ComponentName}.", component.ComponentName);
            components[index] = component with { Status = HealthStatus.Unhealthy, LastCheckUtc = DateTimeOffset.UtcNow };
        }
    }
}
