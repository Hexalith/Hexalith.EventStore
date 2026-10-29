using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Shared authority through the Dapr HTTP state API, with strong reads and first-write ETags.</summary>
public sealed class DaprRouteCatalogStateStore(IHttpClientFactory clients, IConfiguration configuration,
    IOptions<RouteCatalogHostOptions> options) : IRouteCatalogStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <inheritdoc/>
    public RouteCatalogStateCoordinates Coordinates { get; } = RouteCatalogStateCoordinates.Capture(configuration, options.Value);

    /// <inheritdoc/>
    public async Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = Request(HttpMethod.Get,
                $"v1.0/state/{Component()}/{Uri.EscapeDataString(Coordinates.StateKey)}?consistency=strong");
            using HttpResponseMessage response = await clients.CreateClient("route-catalog-state").SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.NoContent) return (RouteCatalogActivationState.Empty, string.Empty);
            if (!response.Headers.TryGetValues("ETag", out IEnumerable<string>? values))
                throw new RouteCatalogUnavailableException("The Dapr authority component did not supply an ETag.");
            string etag = values.Single();
            RouteCatalogActivationState state = await response.Content.ReadFromJsonAsync<RouteCatalogActivationState>(JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new RouteCatalogUnavailableException("The persisted Dapr authority is unreadable.");
            if (string.IsNullOrWhiteSpace(etag.Trim('"')) || state.Epoch < 0 || state.LastLease < 0
                || state.Ready is null || state.Leases is null || state.Attempts is null || state.CompletedTransitions is null
                || state.Ready.Any(receipt => receipt is null) || state.Leases.Any(lease => lease is null)
                || state.Attempts.Any(attempt => attempt is null) || state.CompletedTransitions.Any(transition => transition is null))
                throw new RouteCatalogUnavailableException("The persisted catalog authority is unusable.", RouteCatalogRefusalReason.Backend);
            foreach (var activation in new[] { state.Committed, state.Previous, state.Pending })
                if (activation is not null) _ = RouteCatalogActivationBinding.ComputeDigest(activation);
            return (state, etag.Trim('"'));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (RouteCatalogUnavailableException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidOperationException or OperationCanceledException or ArgumentException or Contracts.Routing.RouteCatalogValidationException)
        {
            throw new RouteCatalogUnavailableException("The catalog authority backend is unavailable.", RouteCatalogRefusalReason.Backend);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = Request(HttpMethod.Post, $"v1.0/state/{Component()}");
            request.Content = JsonContent.Create(new[] { new
            {
                key = Coordinates.StateKey, value = state, etag,
                options = new { concurrency = "first-write", consistency = "strong" },
            } }, options: JsonOptions);
            using HttpResponseMessage response = await clients.CreateClient("route-catalog-state").SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict) return false;
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                // The qualified Redis component returns ERR_STATE_SAVE/500 on an ETag conflict.
                // A fresh strong read must prove another version exists; an error or absent readback never proves failure.
                (_, string observedEtag) = await ReadAsync(cancellationToken).ConfigureAwait(false);
                if (observedEtag != etag && observedEtag.Length > 0) return false;
            }
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (RouteCatalogUnavailableException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidOperationException or OperationCanceledException)
        {
            throw new RouteCatalogUnavailableException("The catalog authority backend is unavailable.", RouteCatalogRefusalReason.Backend);
        }
    }

    private string Component()
    {
        return Uri.EscapeDataString(Coordinates.StateStore);
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var address = new Uri(Coordinates.HttpEndpoint, UriKind.Absolute);
        var request = new HttpRequestMessage(method, new Uri(address, path));
        if (configuration["DAPR_API_TOKEN"] is { Length: > 0 } token)
            request.Headers.TryAddWithoutValidation("dapr-api-token", token);
        return request;
    }
}
