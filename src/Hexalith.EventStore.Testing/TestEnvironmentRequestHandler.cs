namespace Hexalith.EventStore.Testing;

using System.Net.Http.Headers;

/// <summary>Links every request to fixture disposal and prevents credential-bearing redirects.</summary>
internal sealed class TestEnvironmentRequestHandler(Uri endpoint, CancellationToken lifetime, TimeSpan timeout, string? endpointCredential = null) : DelegatingHandler(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
{
    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri target = request.RequestUri ?? throw new TestEnvironmentDescriptorException("HXT109", "A request URI is required.");
        if (target.Scheme != endpoint.Scheme || target.IdnHost != endpoint.IdnHost || target.Port != endpoint.Port || target.UserInfo.Length != 0)
        {
            throw new TestEnvironmentDescriptorException("HXT109", "Fixture requests must remain within their exact declared endpoint origin.");
        }

        if (request.Headers.Host is not null)
            throw new TestEnvironmentDescriptorException("HXT109", "Requests must preserve the exact supplied endpoint authority.");

        if (endpointCredential is not null)
        {
            string[] authorizations = request.Headers.TryGetValues("Authorization", out var values) ? values.ToArray() : [];
            bool bearer = authorizations.Length == 1 && AuthenticationHeaderValue.TryParse(authorizations[0], out var authorization)
                && authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(authorization.Parameter);
            if (!bearer || request.Headers.Contains("dapr-app-id") || request.Content?.Headers.Contains("dapr-app-id") == true
                || !target.AbsolutePath.StartsWith(endpoint.AbsolutePath, StringComparison.Ordinal) || HasStructuralEncoding(target)
                || request.Headers.Contains("dapr-api-token") || request.Content?.Headers.Contains("dapr-api-token") == true)
                throw new TestEnvironmentDescriptorException("HXT109", "Credential-bearing requests must remain within the exact supplied invocation prefix without conflicting credentials.");
            request.Headers.Add("dapr-api-token", endpointCredential);
        }

        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
        linked.CancelAfter(timeout);
        try
        {
            HttpResponseMessage response = await base.SendAsync(request, linked.Token).ConfigureAwait(false);
            response.Content = new TestEnvironmentResponseContent(response.Content, linked);
            return response;
        }
        catch
        {
            linked.Dispose();
            throw;
        }
    }
    private static bool HasStructuralEncoding(Uri target)
    {
        string path = target.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (path.Contains('\\', StringComparison.Ordinal) || path.Contains("%2f", StringComparison.OrdinalIgnoreCase)
                || path.Contains("%5c", StringComparison.OrdinalIgnoreCase) || path.Contains("%2e", StringComparison.OrdinalIgnoreCase)
                || path.Split('/').Any(segment => segment is "." or "..")) return true;
            string decoded = Uri.UnescapeDataString(path);
            if (decoded == path) return path.Contains('%', StringComparison.Ordinal);
            path = decoded;
        }
        return true;
    }

}
