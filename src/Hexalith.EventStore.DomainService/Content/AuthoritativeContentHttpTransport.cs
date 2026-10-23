using System.Security.Claims;

using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.DomainService.Content;

/// <summary>SDK-owned bounded body transport for an existing authenticated domain route.</summary>
/// <remarks>The route authenticates/delegates the original caller; the store independently performs fresh resource authorization. Do not model bodies as event payloads.</remarks>
public static class AuthoritativeContentHttpTransport
{
    /// <summary>Streams the actual request body into the authoritative store after its current authorization.</summary>
    public static Task<ContentReceipt> UploadAsync(HttpRequest httpRequest, ClaimsPrincipal principal, ContentUploadRequest request,
        IAuthoritativeContentStore store, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);
        if (httpRequest.ContentLength is { } length && length != request.ExpectedLength)
            throw new ArgumentException("Content transport length differs from its bounded declaration.", nameof(httpRequest));
        return store.UploadAsync(principal, request, httpRequest.Body, cancellationToken);
    }

    /// <summary>Writes exact verified bytes to an authenticated response with caching disabled.</summary>
    public static async Task ReadAsync(HttpResponse response, ClaimsPrincipal principal, ContentReadRequest request,
        IAuthoritativeContentStore store, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(store);
        ContentReadResult result = await store.ReadAsync(principal, request, cancellationToken).ConfigureAwait(false);
        response.Headers.CacheControl = "no-store";
        response.ContentType = result.Receipt.MediaType;
        response.ContentLength = result.Bytes.LongLength;
        await response.Body.WriteAsync(result.Bytes, cancellationToken).ConfigureAwait(false);
    }
}
