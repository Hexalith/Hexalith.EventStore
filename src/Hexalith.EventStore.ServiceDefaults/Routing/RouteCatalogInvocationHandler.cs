using System.Net;
using System.Reflection;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Fences every Dapr HTTP invocation, including inventoried auxiliary replay and reconstruction calls.</summary>
public sealed class RouteCatalogInvocationHandler : HttpMessageHandler
{
    private const string InvokePrefix = "/v1.0/invoke/";
    private static readonly MethodInfo? DisableAuthenticationOnRedirect = typeof(HttpRequestMessage).GetMethod(
        "DisableAuth", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly IRouteCatalogAuthority _authority;
    private readonly Microsoft.Extensions.Options.IOptions<RouteCatalogHostOptions>? _options;
    private readonly HttpMessageInvoker _transport;
    private readonly HttpMessageInvoker? _invocationTransport;
    private readonly HttpMessageInvoker? _ordinaryRedirectTransport;
    private readonly bool _authorityStateClient;
    private readonly bool _followOrdinaryRedirects;
    private readonly int _maxAutomaticRedirections;
    private readonly bool _disableRedirectAuthentication;
    internal HttpMessageHandler Transport { get; }

    /// <summary>Creates the guard with independently owned ordinary and invocation primary transports.</summary>
    public RouteCatalogInvocationHandler(IRouteCatalogAuthority authority, HttpMessageHandler transport,
        Microsoft.Extensions.Options.IOptions<RouteCatalogHostOptions>? options = null)
        : this(authority, transport, options, false) { }

    /// <summary>Also isolates this SDK's authoritative state client from redirects.</summary>
    internal RouteCatalogInvocationHandler(IRouteCatalogAuthority authority, HttpMessageHandler transport,
        Microsoft.Extensions.Options.IOptions<RouteCatalogHostOptions>? options, bool authorityStateClient)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(transport);
        _authority = authority;
        _options = options;
        Transport = transport;
        _transport = new(transport);
        _authorityStateClient = authorityStateClient;
        HttpMessageHandler? guarded = null;
        try
        {
            guarded = options?.Value.InvocationTransportFactory is { } factory
                ? factory(transport) : RouteCatalogInvocationTransport.Create(transport);
            if (guarded is null || ReferenceEquals(guarded, transport)) _invocationTransport = null;
            else
            {
                // This is the separate owned transport, so enforcing native redirect refusal
                // cannot change ordinary HTTP even when an owner factory supplied it.
                if (guarded is SocketsHttpHandler sockets) sockets.AllowAutoRedirect = false;
                if (guarded is HttpClientHandler client) client.AllowAutoRedirect = false;
                _invocationTransport = new(guarded);
            }
        }
        catch (Exception)
        {
            // A custom invocation factory cannot prevent unrelated ordinary HTTP client creation.
            if (guarded is not null && !ReferenceEquals(guarded, transport)) guarded.Dispose();
            _invocationTransport = null;
        }

        // Snapshot the owner's redirect policy without writing AllowAutoRedirect back onto that handler.
        _followOrdinaryRedirects = transport is SocketsHttpHandler { AllowAutoRedirect: true }
            || transport is HttpClientHandler { AllowAutoRedirect: true };
        _maxAutomaticRedirections = transport switch
        {
            SocketsHttpHandler sockets => sockets.MaxAutomaticRedirections,
            HttpClientHandler client => client.MaxAutomaticRedirections,
            _ => 50,
        };
        _disableRedirectAuthentication = transport switch
        {
            SocketsHttpHandler sockets => sockets.Credentials is not CredentialCache,
            HttpClientHandler client => client.Credentials is not CredentialCache,
            _ => true,
        };
        _ordinaryRedirectTransport = CreateOrdinaryRedirectTransport(transport, _followOrdinaryRedirects);
    }

    /// <summary>Copies native settings onto a redirect-disabled transport used only to observe ordinary hops.</summary>
    private static HttpMessageInvoker? CreateOrdinaryRedirectTransport(HttpMessageHandler transport, bool followRedirects)
    {
        if (!followRedirects) return null;
        HttpMessageHandler? copy = null;
        try
        {
            copy = RouteCatalogInvocationTransport.Create(transport);
            if (copy is null || ReferenceEquals(copy, transport)) return null;
            return new HttpMessageInvoker(copy);
        }
        catch (Exception)
        {
            if (copy is not null && !ReferenceEquals(copy, transport)) copy.Dispose();
            return null;
        }
    }

    /// <summary>Header binding a downstream call to exact retained bytes.</summary>
    public const string RootHeader = "X-Hexalith-Route-Catalog-Root";
    /// <summary>Header binding a downstream call to its monotonic commit/rollback fence.</summary>
    public const string EpochHeader = "X-Hexalith-Route-Catalog-Epoch";

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_options?.Value is { DevelopmentFixture: true, Profile: "Development" } && DevelopmentActorStateTransport.GetOwner(request) is { } owner)
            return await SendOwnerReadAsync(request, owner, cancellationToken).ConfigureAwait(false);
        if (IsUnsupportedBasicInvocation(request))
            throw new RouteCatalogUnavailableException("The Basic Dapr invocation form is unsupported.");
        string? path = request.RequestUri?.AbsolutePath;
        if (path is not null && Uri.UnescapeDataString(path).StartsWith("/v1.0/actors/", StringComparison.OrdinalIgnoreCase))
            throw new RouteCatalogUnavailableException("Raw actor invocation has no proven typed catalog scope.");
        bool pathInvocation = path?.StartsWith(InvokePrefix, StringComparison.Ordinal) == true;
        string[] targets = request.Headers.TryGetValues("dapr-app-id", out var headerTargets) ? headerTargets.ToArray() : [];
        if (!pathInvocation && targets.Length == 0)
            return await SendOrdinaryAsync(request, cancellationToken).ConfigureAwait(false);
        if (targets.Length > 1) throw new RouteCatalogUnavailableException("Ambiguous Dapr invocation target.");
        int separator = pathInvocation ? path!.IndexOf("/method/", InvokePrefix.Length, StringComparison.Ordinal) : -1;
        if (pathInvocation && separator < 0) throw new RouteCatalogUnavailableException("Malformed Dapr invocation target.");
        string appId = pathInvocation ? Uri.UnescapeDataString(path![InvokePrefix.Length..separator]) : targets.Single();
        string rawMethod = pathInvocation ? path![(separator + 8)..] : path![1..];
        if (!DevelopmentTechnicalHttpTransport.CanonicalPath(rawMethod)) throw new RouteCatalogUnavailableException("The invocation method contains ambiguous escaped path structure.");
        string method = Uri.UnescapeDataString(rawMethod);
        if (pathInvocation && targets.Length == 1 && targets[0] != appId)
            throw new RouteCatalogUnavailableException("Conflicting Dapr invocation targets.");
        if (rawMethod == method && RouteCatalogEndpointExtensions.IsControlMethod(method, request.Method))
        {
            if (_options?.Value.ControlPlaneAppIds.Contains(appId, StringComparer.Ordinal) != true)
                throw new RouteCatalogUnavailableException("The catalog control recipient is not independently configured.");
            request.Headers.Remove(RootHeader);
            request.Headers.Remove(EpochHeader);
            return await InvocationTransport().SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        HttpMessageInvoker invocation = InvocationTransport();
        RouteCatalogDispatchLease lease = await _authority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var release = lease.ConfigureAwait(false);
        if (!lease.Snapshot.Envelope.Routes.Any(r => r.AppId == appId && r.Method == method)
            && !lease.Snapshot.AuxiliaryOperations.Any(a => a.NativeActor is null && a.DaprCallback is null && a.AppId == appId && (a.Http is null
                ? a.Method == method : DevelopmentTechnicalHttpTransport.Matches(a, rawMethod, request.Method.Method))))
            throw new RouteCatalogUnavailableException("The Dapr invocation target is outside the committed catalog and auxiliary inventory.");
        request.Headers.Remove(RootHeader);
        request.Headers.Remove(EpochHeader);
        request.Headers.TryAddWithoutValidation(RootHeader, lease.Snapshot.RootDigest);
        request.Headers.TryAddWithoutValidation(EpochHeader, lease.Snapshot.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return await lease.InvokeRemoteAsync(() => invocation.SendAsync(request, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendOwnerReadAsync(HttpRequestMessage request, string appId, CancellationToken token)
    {
        var options = _options?.Value;
        var owners = options?.DevelopmentActorStateOwners.Where(owner => owner.AppId == appId).ToArray() ?? [];
        if (request.Headers.Contains("dapr-app-id") || IsUnsupportedBasicInvocation(request)
            || options is not { DevelopmentFixture: true, Profile: "Development" } || owners.Length != 1
            || request.Method != HttpMethod.Get || request.RequestUri is not { IsAbsoluteUri: true } uri
            || string.IsNullOrWhiteSpace(owners[0].ApiToken)
            || !Uri.TryCreate(owners[0].HttpEndpoint.TrimEnd('/') + "/", UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("http" or "https") || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0
            || uri.Scheme != endpoint.Scheme || uri.Authority != endpoint.Authority || uri.UserInfo.Length != 0
            || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.AbsolutePath.StartsWith(endpoint.AbsolutePath, StringComparison.Ordinal))
            throw new RouteCatalogUnavailableException("The actual owner sidecar read coordinates are not independently qualified.");
        string path = uri.AbsolutePath[endpoint.AbsolutePath.Length..];
        bool metadata = path == "v1.0/metadata";
        string[] pieces = path.Split('/');
        if (!metadata && (pieces.Length != 6 || pieces[0] != "v1.0" || pieces[1] != "actors" || pieces[4] != "state"
            || pieces.Any(string.IsNullOrEmpty) || !DevelopmentTechnicalHttpTransport.CanonicalPath(path)))
            throw new RouteCatalogUnavailableException("Only the existing exact owner actor-state GET shape is supported.");
        // Actor state has six path segments: v1.0/actors/type/id/state/key.
        if (!metadata && pieces.Length != 6) throw new RouteCatalogUnavailableException("The owner state key must be one scalar segment.");
        if (!metadata && !RouteCatalogRemoteCall.Current.Any(lease => lease.Snapshot.Envelope.SchemaVersion == 3
            && lease.Snapshot.Envelope.Profile == "Development" && lease.Snapshot.AuxiliaryOperations.Any(operation => operation.AppId == lease.Snapshot.LocalAppId
                && operation.Http?.Owner.StartsWith("Hexalith.EventStore.Admin.Server.Controllers.", StringComparison.Ordinal) == true)
            && (lease.Snapshot.Envelope.Routes.Any(route => route.AppId == appId)
                || lease.Snapshot.AuxiliaryOperations.Any(operation => operation.AppId == appId))))
            throw new RouteCatalogUnavailableException("Owner actor-state reads require the actual enrolled Admin receiver scope.");
        request.Headers.Remove("dapr-api-token");
        request.Headers.TryAddWithoutValidation("dapr-api-token", owners[0].ApiToken);
        return await RouteCatalogRemoteCall.InvokeAsync(() => InvocationTransport().SendAsync(request, token), token).ConfigureAwait(false);
    }

    private HttpMessageInvoker InvocationTransport() => _invocationTransport
        ?? throw new RouteCatalogUnavailableException("A configured redirect-disabled invocation transport is required.", RouteCatalogRefusalReason.Configuration);

    /// <summary>
    /// Follows configured ordinary redirects itself so a later Dapr hop cannot run beneath this guard.
    /// Disabled redirects and the authoritative state client keep their existing direct transports.
    /// </summary>
    private async Task<HttpResponseMessage> SendOrdinaryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_authorityStateClient)
            return await InvocationTransport().SendAsync(request, cancellationToken).ConfigureAwait(false);
        // A missing isolated copy keeps the owner's configured redirect behavior. Invocation remains fail-closed.
        if (!_followOrdinaryRedirects || _ordinaryRedirectTransport is null)
            return await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        HttpMessageInvoker transport = _ordinaryRedirectTransport;
        (byte[] Bytes, KeyValuePair<string, string[]>[] Headers)? replayable =
            await CaptureReplayableContentAsync(request, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage response = await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        uint redirectCount = 0;
        while (TryGetRedirectUri(request.RequestUri, response) is Uri redirectUri)
        {
            redirectCount++;
            if (redirectCount > _maxAutomaticRedirections) break;
            bool forceGet = RequiresForceGet(response.StatusCode, request.Method);
            Uri? previous = request.RequestUri;
            response.Dispose();
            if (ChangesHostOrScheme(previous, redirectUri)) ClearRedirectAuthentication(request);
            request.RequestUri = redirectUri;
            if (forceGet)
            {
                request.Method = HttpMethod.Get;
                request.Content = null;
                if (request.Headers.TransferEncodingChunked == true) request.Headers.TransferEncodingChunked = false;
            }
            else if (replayable is { } captured) request.Content = CreateReplayableContent(captured.Bytes, captured.Headers);
            if (IsSameHost(previous, redirectUri) && RequiresCatalogFence(request))
                return await SendAsync(request, cancellationToken).ConfigureAwait(false);
            response = await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>Matches the ordinary-branch exclusions above, including Basic app-id and raw actor forms.</summary>
    private static bool RequiresCatalogFence(HttpRequestMessage request)
    {
        if (IsUnsupportedBasicInvocation(request)) return true;
        string? path = request.RequestUri?.AbsolutePath;
        if (path is not null && Uri.UnescapeDataString(path).StartsWith("/v1.0/actors/", StringComparison.OrdinalIgnoreCase))
            return true;
        if (path?.StartsWith(InvokePrefix, StringComparison.Ordinal) == true) return true;
        return request.Headers.TryGetValues("dapr-app-id", out IEnumerable<string>? targets) && targets.Any();
    }

    private static bool IsSameHost(Uri? current, Uri next) =>
        current is { IsAbsoluteUri: true } && current.Host.Equals(next.Host, StringComparison.OrdinalIgnoreCase);

    private static bool ChangesHostOrScheme(Uri? current, Uri next) =>
        current is { IsAbsoluteUri: true } && (!current.Host.Equals(next.Host, StringComparison.OrdinalIgnoreCase)
            || !current.Scheme.Equals(next.Scheme, StringComparison.OrdinalIgnoreCase));

    private void ClearRedirectAuthentication(HttpRequestMessage request)
    {
        request.Headers.Authorization = null;
        request.Headers.ProxyAuthorization = null;
        if (_disableRedirectAuthentication) DisableAuthenticationOnRedirect?.Invoke(request, null);
    }

    private static async Task<(byte[] Bytes, KeyValuePair<string, string[]>[] Headers)?> CaptureReplayableContentAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null) return null;
        HttpContent original = request.Content;
        byte[] bytes = await original.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var headers = original.Headers.Select(header => new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray())).ToArray();
        request.Content = CreateReplayableContent(bytes, headers);
        original.Dispose();
        return (bytes, headers);
    }

    private static ByteArrayContent CreateReplayableContent(byte[] bytes, KeyValuePair<string, string[]>[] headers)
    {
        var content = new ByteArrayContent(bytes);
        foreach (var header in headers)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return content;
    }

    private static Uri? TryGetRedirectUri(Uri? requestUri, HttpResponseMessage response)
    {
        if (requestUri is not { IsAbsoluteUri: true }) return null;
        if (response.StatusCode is not (HttpStatusCode.MultipleChoices or HttpStatusCode.Moved or HttpStatusCode.Found
            or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
            return null;
        Uri? location = response.Headers.Location;
        if (location is null) return null;
        if (!location.IsAbsoluteUri) location = new Uri(requestUri, location);
        if (!string.IsNullOrEmpty(requestUri.Fragment) && string.IsNullOrEmpty(location.Fragment))
            location = new UriBuilder(location) { Fragment = requestUri.Fragment }.Uri;
        if (IsSecureScheme(requestUri.Scheme) && !IsSecureScheme(location.Scheme)) return null;
        return location;
    }

    private static bool IsSecureScheme(string scheme) =>
        scheme.Equals("https", StringComparison.OrdinalIgnoreCase) || scheme.Equals("wss", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresForceGet(HttpStatusCode statusCode, HttpMethod requestMethod) => statusCode switch
    {
        HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.MultipleChoices => requestMethod == HttpMethod.Post,
        HttpStatusCode.SeeOther => requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head,
        _ => false,
    };

    private static bool IsUnsupportedBasicInvocation(HttpRequestMessage request)
    {
        if (request.Headers.Authorization is not { } authorization
            || !authorization.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)
            || authorization.Parameter is null) return false;
        try
        {
            string credentials = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Parameter));
            int separator = credentials.IndexOf(':');
            return separator >= 0 && credentials[..separator].Equals("dapr-app-id", StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException) { return false; }
    }
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _transport.Dispose(); _invocationTransport?.Dispose(); _ordinaryRedirectTransport?.Dispose(); }
        base.Dispose(disposing);
    }
}
