using System.Net;
using System.Text.Json;

namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>One-shot client for the explicitly selected private gateway proof worker.</summary>
public sealed class CatalogInvocationClient
{
    private readonly CatalogInvocationFiles _files;

    /// <summary>Validates the existing launcher-owned private directory.</summary>
    /// <param name="directory">The absolute owner-private IPC directory.</param>
    public CatalogInvocationClient(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _files = new(directory);
    }

    /// <summary>Sends one finite callback; returns only an actual HTTP response and propagates SDK/transport failures.</summary>
    /// <param name="appId">Selected owner.</param>
    /// <param name="method">Finite callback path.</param>
    /// <param name="payload">Original callback body.</param>
    /// <param name="credential">Positive or negative credential mode.</param>
    /// <param name="token">Only a negative human/public-issuer token.</param>
    /// <param name="cancellationToken">Caller deadline.</param>
    /// <returns>The actual private status/body.</returns>
    public async Task<HttpResponseMessage> InvokeAsync(string appId, string method, JsonElement payload,
        CatalogInvocationCredential credential = CatalogInvocationCredential.Workload, string? token = null,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        deadline.Token.ThrowIfCancellationRequested();
        string id = Guid.NewGuid().ToString("N");
        var request = new CatalogInvocationRequest(id, DateTimeOffset.UtcNow.AddSeconds(45), appId, method, credential, token, payload);
        await _files.WriteAsync(id, ".request", request, deadline.Token).ConfigureAwait(false);
        string resultPath = _files.PathFor(id, ".response");
        while (!File.Exists(resultPath)) await Task.Delay(25, deadline.Token).ConfigureAwait(false);
        deadline.Token.ThrowIfCancellationRequested();
        CatalogInvocationResult result = await _files.ReadAsync<CatalogInvocationResult>(id, ".response", deadline.Token).ConfigureAwait(false);
        deadline.Token.ThrowIfCancellationRequested();
        if (DateTimeOffset.UtcNow >= request.ExpiresAt) throw new OperationCanceledException("Private invocation deadline elapsed.", deadline.Token);
        if (result.Id != id || result.Failure is not null || result.StatusCode is not (>= 100 and <= 599)
            || result.Body is null || result.Body.Length > CatalogInvocationFiles.MaximumBytes / 2)
            throw new InvalidOperationException("Private catalog invocation failed: " + (result.Failure ?? "invalid-response") + ".");
        return new((HttpStatusCode)result.StatusCode.Value) { Content = new ByteArrayContent(result.Body) };
    }
}
