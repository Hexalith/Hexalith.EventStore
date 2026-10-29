using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Nonsecret effective coordinates of the independently selected Dapr authority cell.</summary>
/// <param name="StateStore">Selected component name.</param>
/// <param name="StateKey">Selected shared state key.</param>
/// <param name="HttpEndpoint">Normalized local Dapr HTTP endpoint.</param>
public sealed record RouteCatalogStateCoordinates(string StateStore, string StateKey, string HttpEndpoint)
{
    /// <summary>Captures the same effective runtime configuration used for authority state RPCs.</summary>
    public static RouteCatalogStateCoordinates Capture(IConfiguration configuration, RouteCatalogHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.StateStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.StateKey);
        string endpoint = configuration["DAPR_HTTP_ENDPOINT"] ?? $"http://127.0.0.1:{configuration["DAPR_HTTP_PORT"] ?? "3500"}";
        if (!Uri.TryCreate(endpoint.TrimEnd('/') + "/", UriKind.Absolute, out Uri? address)
            || address.Scheme is not ("http" or "https") || address.UserInfo.Length != 0
            || address.Query.Length != 0 || address.Fragment.Length != 0)
            throw new RouteCatalogUnavailableException("The Dapr HTTP endpoint is invalid.", RouteCatalogRefusalReason.Configuration);
        return new(options.StateStore, options.StateKey, address.AbsoluteUri);
    }
}
