
using System.Text.Json;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.HealthChecks;

/// <summary>
/// Default <see cref="IDaprActorPlacementProbe"/> that reads the local DAPR sidecar's
/// <c>/v1.0/metadata</c> document and extracts the <c>actorRuntime</c> placement state.
/// </summary>
/// <remarks>
/// The typed <c>DaprClient.GetMetadataAsync</c> result does not expose <c>actorRuntime.hostReady</c>
/// or <c>actorRuntime.placement</c> (it surfaces only the registered actor type list), so this probe
/// reads the raw metadata JSON over HTTP. The endpoint is resolved from the sidecar-injected
/// <c>DAPR_HTTP_ENDPOINT</c> / <c>DAPR_HTTP_PORT</c> environment variables (default port 3500).
/// </remarks>
public sealed class DaprActorPlacementProbe : IDaprActorPlacementProbe {
    private readonly HttpClient _httpClient;
    private readonly string _metadataUri;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprActorPlacementProbe"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the local sidecar.</param>
    /// <param name="configuration">Application configuration providing the DAPR HTTP endpoint.</param>
    public DaprActorPlacementProbe(HttpClient httpClient, IConfiguration configuration) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(configuration);

        string? configuredEndpoint = configuration["DAPR_HTTP_ENDPOINT"];
        string? configuredPort = configuration["DAPR_HTTP_PORT"];
        string endpoint = !string.IsNullOrWhiteSpace(configuredEndpoint)
            ? configuredEndpoint
            : $"http://localhost:{(!string.IsNullOrWhiteSpace(configuredPort) ? configuredPort : "3500")}";
        _metadataUri = $"{endpoint.TrimEnd('/')}/v1.0/metadata";
    }

    /// <inheritdoc/>
    public async Task<DaprActorPlacementStatus> CheckAsync(CancellationToken cancellationToken) {
        using HttpResponseMessage response = await _httpClient
            .GetAsync(_metadataUri, cancellationToken)
            .ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        string json = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("actorRuntime", out JsonElement actorRuntime)
            || actorRuntime.ValueKind != JsonValueKind.Object) {
            // Metadata responded but exposes no actor runtime section — treat as host-not-ready.
            return new DaprActorPlacementStatus(MetadataReachable: true, HostReady: false, Placement: null, RuntimeStatus: null);
        }

        bool hostReady = actorRuntime.TryGetProperty("hostReady", out JsonElement hostReadyElement)
            && hostReadyElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            && hostReadyElement.GetBoolean();
        string? placement = actorRuntime.TryGetProperty("placement", out JsonElement placementElement)
            ? placementElement.GetString()
            : null;
        string? runtimeStatus = actorRuntime.TryGetProperty("runtimeStatus", out JsonElement runtimeStatusElement)
            ? runtimeStatusElement.GetString()
            : null;

        return new DaprActorPlacementStatus(MetadataReachable: true, hostReady, placement, runtimeStatus);
    }
}
