using System.Net.Http.Headers;
using System.Net.Http.Json;

using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>Finite private callbacks from the actual enrolled gateway; no HTTP relay or factory filters.</summary>
internal sealed class CatalogInvocationWorker : BackgroundService
{
    private readonly CatalogInvocationFiles _files;
    private readonly CatalogInvocationOptions _selection;
    private readonly IWorkloadAssertionIssuer _issuer;
    private readonly Uri _endpoint;
    private readonly HttpClient _http;

    /// <summary>Uses only the real selected gateway authority, issuer and owned local Dapr endpoint.</summary>
    /// <param name="selection">The finite private proof selection.</param>
    /// <param name="authority">The actual enrolled gateway authority.</param>
    /// <param name="catalog">The actual local catalog options.</param>
    /// <param name="issuer">The gateway's real configured issuer.</param>
    /// <param name="endpoint">The actual owned Dapr endpoint.</param>
    internal CatalogInvocationWorker(CatalogInvocationOptions selection, IRouteCatalogAuthority authority,
        IOptions<RouteCatalogHostOptions> catalog, IWorkloadAssertionIssuer issuer, Uri endpoint)
    {
        _selection = selection;
        _files = new(selection.Directory);
        _issuer = issuer;
        _endpoint = endpoint;
        if (catalog.Value is not { DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true })
            throw new InvalidOperationException("Private invocation requires the real selected Development catalog.");
        // Deliberately constructed: the factory's workload filter must never replace negative credentials.
        _http = new(new RouteCatalogInvocationHandler(authority,
            new SocketsHttpHandler { AllowAutoRedirect = false }, catalog)) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (string path in _files.Pending())
            {
                string id = Path.GetFileNameWithoutExtension(path);
                _files.Claim(id);
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                operation.CancelAfter(TimeSpan.FromSeconds(45));
                CatalogInvocationResult result;
                try
                {
                    var request = await _files.ReadAsync<CatalogInvocationRequest>(id, ".working", operation.Token).ConfigureAwait(false);
                    if (request.Id != id) throw new InvalidOperationException("Mismatched private identity.");
                    result = await InvokeAsync(request, operation.Token).ConfigureAwait(false);
                    if (DateTimeOffset.UtcNow >= request.ExpiresAt)
                        throw new OperationCanceledException("Private invocation deadline elapsed.");
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Never turn an admission/SDK/transport exception into an HTTP response.
                    string diagnostic = exception.ToString();
                    result = new(id, null, [], exception.GetType().Name, diagnostic[..Math.Min(diagnostic.Length, 8192)]);
                }
                try
                {
                    await _files.WriteAsync(id, ".response", result, operation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // An expired private operation publishes no late HTTP result. The caller's
                    // own complete deadline fails, and launcher cleanup retains/removes IPC files.
                }
            }
            await Task.Delay(25, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Executes one finite callback under linked host/caller deadlines and real SDK generation fencing.</summary>
    /// <param name="request">The verified private request.</param>
    /// <param name="hostToken">The host lifetime deadline.</param>
    /// <returns>An actual bounded HTTP result; SDK/transport failures propagate.</returns>
    internal async Task<CatalogInvocationResult> InvokeAsync(CatalogInvocationRequest request, CancellationToken hostToken)
    {
        Validate(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
        deadline.CancelAfter(request.ExpiresAt - DateTimeOffset.UtcNow);
        deadline.Token.ThrowIfCancellationRequested();
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri(_endpoint, "v1.0/invoke/" + request.AppId + "/method/" + request.Method))
        {
            Content = JsonContent.Create(request.Payload, options: CatalogInvocationFiles.Json),
        };
        await message.Content.LoadIntoBufferAsync(CatalogInvocationFiles.MaximumBytes, deadline.Token).ConfigureAwait(false);
        if (request.Credential == CatalogInvocationCredential.HumanBearer)
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.Token);
        else if (request.Credential == CatalogInvocationCredential.PublicIssuerWorkload)
            message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, request.Token);
        else if (request.Credential is CatalogInvocationCredential.Workload or CatalogInvocationCredential.WrongOperation)
        {
            string operation = request.Method == "admin/operational-index-metadata" || request.Credential == CatalogInvocationCredential.WrongOperation
                ? "domain-service:metadata" : "domain-service:project";
            string assertion = await _issuer.IssueAsync(new(request.AppId, operation), deadline.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Actual gateway issuer refused the selected assertion.");
            message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        }
        deadline.Token.ThrowIfCancellationRequested();
        using HttpResponseMessage response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > CatalogInvocationFiles.MaximumBytes / 2) throw new IOException("Private response too large.");
        using Stream stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var body = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
        {
            if (body.Length + read > CatalogInvocationFiles.MaximumBytes / 2) throw new IOException("Private response too large.");
            await body.WriteAsync(buffer.AsMemory(0, read), deadline.Token).ConfigureAwait(false);
        }
        deadline.Token.ThrowIfCancellationRequested();
        if (DateTimeOffset.UtcNow >= request.ExpiresAt) throw new OperationCanceledException("Private invocation deadline elapsed.", deadline.Token);
        return new(request.Id, (int)response.StatusCode, body.ToArray(), null);
    }

    /// <summary>Refuses expanded targets, credential modes, body kinds and stale or excessive deadlines.</summary>
    /// <param name="request">The private request.</param>
    internal void Validate(CatalogInvocationRequest request)
    {
        bool metadata = request.Method == "admin/operational-index-metadata" && _selection.MetadataAppIds.Contains(request.AppId, StringComparer.Ordinal);
        bool projection = request.AppId == _selection.ProjectionAppId && request.Method is "project/v2" or "project/rebuild/v1";
        bool suppliedToken = request.Credential is CatalogInvocationCredential.HumanBearer or CatalogInvocationCredential.PublicIssuerWorkload;
        TimeSpan remaining = request.ExpiresAt - DateTimeOffset.UtcNow;
        if ((!metadata && !projection) || !Enum.IsDefined(request.Credential)
            || suppliedToken != !string.IsNullOrWhiteSpace(request.Token) || request.Token?.Length > 32768
            || request.Token?.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)) == true
            || request.Payload.ValueKind != System.Text.Json.JsonValueKind.Object
            || remaining <= TimeSpan.Zero || remaining > TimeSpan.FromSeconds(45)
            || request.Credential == CatalogInvocationCredential.WrongOperation && !projection)
            throw new InvalidOperationException("Private catalog request is outside the finite proof selection.");
        _ = _files.PathFor(request.Id, ".working");
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }
}
