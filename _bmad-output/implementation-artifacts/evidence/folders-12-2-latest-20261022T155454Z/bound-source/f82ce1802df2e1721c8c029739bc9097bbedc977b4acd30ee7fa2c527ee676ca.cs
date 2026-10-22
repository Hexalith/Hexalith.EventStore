using System.Net.Http.Headers;

namespace Hexalith.EventStore.DomainService;

/// <summary>Attaches an acquired authority access token without replacing an explicit caller token.</summary>
internal sealed class GatewayAuthorityAccessTokenHandler(GatewayAuthorityAccessTokenProvider provider) : DelegatingHandler
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization ??= new AuthenticationHeaderValue("Bearer",
            await provider.GetAsync(cancellationToken).ConfigureAwait(false));
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
