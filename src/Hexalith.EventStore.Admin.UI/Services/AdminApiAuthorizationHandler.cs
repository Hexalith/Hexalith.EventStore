using System.Net.Http.Headers;
using System.Net;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Adds a bearer token to outgoing HTTP requests made to the Admin.Server REST API.
/// </summary>
public sealed class AdminApiAuthorizationHandler(AdminApiAccessTokenProvider tokenProvider) : DelegatingHandler {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        string token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized) {
            throw new UnauthorizedAccessException("Admin API request was unauthorized. Sign in again and retry.");
        }

        return response;
    }
}
