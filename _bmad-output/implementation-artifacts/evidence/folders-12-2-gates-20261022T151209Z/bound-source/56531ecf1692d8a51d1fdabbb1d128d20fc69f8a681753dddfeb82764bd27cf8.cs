using System.Globalization;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.Sample.Api.Services;

/// <summary>
/// Resolves the local DAPR HTTP sidecar endpoint for the sample API host.
/// </summary>
public static class DaprHttpEndpointResolver
{
    /// <summary>
    /// Resolves the configured DAPR HTTP endpoint, falling back to the default local sidecar port.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The normalized DAPR HTTP origin URI.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>DAPR_HTTP_ENDPOINT</c> or <c>DAPR_HTTP_PORT</c> is malformed.
    /// </exception>
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? endpoint = configuration["DAPR_HTTP_ENDPOINT"]?.Trim();
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.Port <= 0
                || !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException("DAPR_HTTP_ENDPOINT must be an absolute HTTP or HTTPS origin URI.");
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }

        string? port = configuration["DAPR_HTTP_PORT"]?.Trim();
        if (string.IsNullOrWhiteSpace(port))
        {
            return "http://localhost:3500";
        }

        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedPort)
            || parsedPort <= 0
            || parsedPort > 65535)
        {
            throw new InvalidOperationException("DAPR_HTTP_PORT must be a TCP port number between 1 and 65535.");
        }

        return $"http://localhost:{parsedPort.ToString(CultureInfo.InvariantCulture)}";
    }
}
