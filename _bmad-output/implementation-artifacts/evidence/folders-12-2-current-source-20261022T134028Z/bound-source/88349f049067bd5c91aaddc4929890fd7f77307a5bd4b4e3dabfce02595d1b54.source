using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Outbound handler that proves a domain service's own workload identity when it submits a trusted effect to the
/// EventStore gateway (FR28).
/// </summary>
/// <remarks>
/// For a request to <see cref="HttpTrustedEffectSubmitter.Route"/>, the handler removes any caller-supplied workload
/// assertion and human bearer, then attaches a fresh short-lived assertion from the trusted issuer for exactly the
/// gateway audience and <see cref="EventStoreWorkloadOperations.TrustedEffect"/>. The caller (<c>azp</c>) is this
/// domain service: its configured workload in symmetric mode, or its own client registration in authority mode. When
/// no assertion can be issued the request is sent without one and the gateway denies it with <c>401</c>. Requests to
/// other routes are left untouched.
/// </remarks>
/// <param name="serviceProvider">The client's service provider, used to resolve the issuer per request.</param>
/// <param name="gatewayAudience">The workload audience of the receiving EventStore gateway.</param>
public sealed partial class DomainServiceTrustedEffectAssertionHandler(IServiceProvider serviceProvider, string gatewayAudience)
    : DelegatingHandler
{
    private const string AuthorizationHeaderName = "Authorization";

    /// <summary>
    /// Gets whether a request targets the trusted-effect submission route.
    /// </summary>
    /// <param name="requestUri">The request URI.</param>
    /// <returns><see langword="true"/> when the path ends with the trusted-effect route.</returns>
    public static bool IsTrustedEffectSubmission(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return false;
        }

        string path = requestUri.IsAbsoluteUri
            ? requestUri.AbsolutePath
            : requestUri.OriginalString.Split('?', 2)[0];
        return path.TrimEnd('/').EndsWith("/" + HttpTrustedEffectSubmitter.Route, StringComparison.Ordinal)
            || string.Equals(path.TrimEnd('/'), HttpTrustedEffectSubmitter.Route, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsTrustedEffectSubmission(request.RequestUri))
        {
            _ = request.Headers.Remove(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName);
            _ = request.Headers.Remove(AuthorizationHeaderName);
            IWorkloadAssertionIssuer? issuer = serviceProvider.GetService<IWorkloadAssertionIssuer>();
            string? assertion = issuer is null
                ? null
                : await issuer
                    .IssueAsync(new WorkloadAssertionRequest(gatewayAudience, EventStoreWorkloadOperations.TrustedEffect), cancellationToken)
                    .ConfigureAwait(false);
            if (assertion is null)
            {
                LogAssertionUnavailable(
                    serviceProvider.GetService<ILogger<DomainServiceTrustedEffectAssertionHandler>>()
                        ?? NullLogger<DomainServiceTrustedEffectAssertionHandler>.Instance);
            }
            else
            {
                _ = request.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 5542,
        Level = LogLevel.Warning,
        Message = "Trusted-effect submission sent without a workload assertion; the EventStore gateway will deny it. Configure Authentication:WorkloadIssuer for this domain service.")]
    private static partial void LogAssertionUnavailable(ILogger logger);
}
