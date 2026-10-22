using System.Net.Http.Headers;

namespace Hexalith.EventStore.Sample.BlazorUI.Services;

/// <summary>
/// Adds a bearer token to outgoing HTTP requests made to the protected EventStore.
/// </summary>
public sealed class EventStoreApiAuthorizationHandler(EventStoreApiAccessTokenProvider tokenProvider) : DelegatingHandler {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        string token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}