using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Prevents unowned requests from reaching the configured Dapr sidecar.</summary>
internal sealed partial class GatewayDaprRoutingGuard(
    IConfiguration? configuration,
    ILogger<GatewayDaprRoutingGuard> logger) : DelegatingHandler
{
    private int _warned;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri address = request.RequestUri!;
        string? endpoint = configuration?["DAPR_HTTP_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT");
        string? port = configuration?["DAPR_HTTP_PORT"]
            ?? Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? daprEndpoint)
            && (daprEndpoint.Scheme == Uri.UriSchemeHttp || daprEndpoint.Scheme == Uri.UriSchemeHttps))
        {
            if (SameOrigin(address, daprEndpoint))
            {
                throw MissingRouting();
            }
        }
        else if (address.IsLoopback && address.Scheme == Uri.UriSchemeHttp)
        {
            if (int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int daprPort))
            {
                if (address.Port == daprPort)
                {
                    throw MissingRouting();
                }
            }
            else if (address.Port == 3500 && Interlocked.Exchange(ref _warned, 1) == 0)
            {
                MissingDaprRouting(logger);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static bool SameOrigin(Uri address, Uri endpoint) =>
        address.Scheme == endpoint.Scheme && address.Port == endpoint.Port
        && (string.Equals(address.IdnHost, endpoint.IdnHost, StringComparison.OrdinalIgnoreCase)
            || (address.IsLoopback && endpoint.IsLoopback
                && (address.HostNameType == UriHostNameType.Dns || endpoint.HostNameType == UriHostNameType.Dns)));

    private static InvalidOperationException MissingRouting() => new(
        "The EventStore gateway client targets the configured Dapr sidecar. Register AddEventStoreDaprServiceInvocation after forwarding handlers.");

    [LoggerMessage(EventId = 1900, Level = LogLevel.Warning,
        Message = "The EventStore gateway client targets the default local Dapr port without innermost AddEventStoreDaprServiceInvocation. Register the routing handler last if this address is a Dapr sidecar.")]
    private static partial void MissingDaprRouting(ILogger logger);
}
