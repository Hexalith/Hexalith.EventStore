using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Platform-owned outbound handler that proves EventStore's identity to a domain service (FR28, AD-18).
/// </summary>
/// <remarks>
/// For every Dapr service-invocation request to a canonical domain-service method, the handler removes any
/// caller-supplied or forwarded workload assertion and human bearer, then attaches a fresh short-lived assertion
/// for exactly that receiving application and operation. Requests to other methods are left untouched. When no
/// assertion can be issued the request is sent without one and the receiver denies it.
/// </remarks>
public sealed partial class DomainServiceWorkloadAssertionHandler(IServiceProvider serviceProvider) : DelegatingHandler
{
    private const string AuthorizationHeaderName = "Authorization";

    /// <summary>
    /// Resolves the receiving application and canonical route of a Dapr service-invocation URI.
    /// </summary>
    /// <param name="requestUri">The outbound request URI.</param>
    /// <param name="appId">The receiving Dapr application id.</param>
    /// <param name="route">The canonical domain-service route.</param>
    /// <returns><see langword="true"/> when the URI invokes a canonical domain-service method.</returns>
    public static bool TryGetDomainServiceTarget(Uri? requestUri, out string? appId, out EventStoreDomainServiceRoute? route)
    {
        appId = null;
        route = null;
        if (requestUri is null || !requestUri.IsAbsoluteUri)
        {
            return false;
        }

        string[] segments = requestUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 5
            || !string.Equals(segments[0], "v1.0", StringComparison.Ordinal)
            || !string.Equals(segments[1], "invoke", StringComparison.Ordinal)
            || !string.Equals(segments[3], "method", StringComparison.Ordinal))
        {
            return false;
        }

        string target = Uri.UnescapeDataString(segments[2]);
        string method = string.Join('/', segments.Skip(4).Select(Uri.UnescapeDataString));
        if (string.IsNullOrWhiteSpace(target) || !EventStoreDomainServiceRoutes.TryGet(method, out route))
        {
            route = null;
            return false;
        }

        appId = target;
        return true;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetDomainServiceTarget(request.RequestUri, out string? appId, out EventStoreDomainServiceRoute? route))
        {
            _ = request.Headers.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
            _ = request.Headers.Remove(AuthorizationHeaderName);

            // Resolved per request so building an unrelated HTTP client never requires the issuer's configuration.
            IWorkloadAssertionIssuer? issuer = ResolveIssuer();
            string? assertion = issuer is null
                ? null
                : await issuer
                    .IssueAsync(new WorkloadAssertionRequest(appId!, route!.Operation), cancellationToken)
                    .ConfigureAwait(false);
            if (assertion is null)
            {
                LogAssertionUnavailable(
                    serviceProvider.GetService<ILogger<DomainServiceWorkloadAssertionHandler>>() ?? NullLogger<DomainServiceWorkloadAssertionHandler>.Instance,
                    route!.Operation);
            }
            else
            {
                _ = request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private IWorkloadAssertionIssuer? ResolveIssuer()
    {
        try
        {
            return serviceProvider.GetService<IWorkloadAssertionIssuer>();
        }
        catch (InvalidOperationException)
        {
            // An issuer whose dependencies are not registered cannot issue; the receiver denies the call.
            return null;
        }
    }

    [LoggerMessage(
        EventId = 5541,
        Level = LogLevel.Warning,
        Message = "Domain-service invocation sent without a workload assertion; the receiver will deny it. Operation={Operation}")]
    private static partial void LogAssertionUnavailable(ILogger logger, string operation);
}
