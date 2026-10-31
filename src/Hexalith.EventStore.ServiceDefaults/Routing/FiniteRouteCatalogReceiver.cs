using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Bounds actual Dapr prefix grants to exact authenticated endpoint references, paths and HTTP verbs.</summary>
public sealed class FiniteRouteCatalogReceiver
{
    /// <summary>The independently captured installed fence implementation.</summary>
    public const string Version = "eventstore-finite-dapr-receiver-v1";
    /// <summary>Explicit SDK-owned technical HTTP qualification; ordinary finite receivers retain v1 refusal rules.</summary>
    public const string TechnicalVersion = "eventstore-finite-sdk-http-receiver-v2";
    private readonly Dictionary<(string Path, string Verb), (RouteEndpoint Endpoint, string Operation, string[] Policies, RouteCatalogHttpTransport? Http, RouteCatalogNativeActorTransport? Native, RouteCatalogSidecarTransport? Sidecar, RouteCatalogDaprCallbackTransport? Callback)> _allowed = new();
    private readonly List<(string Prefix, string[] Verbs, string Method)> _grants = [];
    private bool _technical;
    private IDevelopmentNativeActorReceiver? _native;
    private IDevelopmentDaprCallbackReceiver? _callbacks;
    private readonly TaskCompletionSource _captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<RouteEndpoint, string?>? _operation;
    private Func<RouteEndpoint, RouteCatalogSidecarTransport?>? _sidecar;
    private Func<IEnumerable<Endpoint>>? _currentEndpoints;
    private string[] _prefixes = [];
    private RouteCatalogReceiverSurface? _surface;

    /// <summary>Installs the fence only for an explicit Development bootstrap; normal host composition is unaffected.</summary>
    public static WebApplication UseFiniteDevelopmentRouteCatalogReceiver(WebApplication app, Func<RouteEndpoint, string?> operation)
        => UseFiniteDevelopmentRouteCatalogReceiver(app, operation, false);

    /// <summary>Opts a private Development SDK host into code-bound existing administrative HTTP transports.</summary>
    public static WebApplication UseFiniteDevelopmentRouteCatalogReceiver(WebApplication app, Func<RouteEndpoint, string?> operation, bool technical)
        => UseFiniteDevelopmentRouteCatalogReceiver(app, operation, technical, null);

    /// <summary>Binds the actual SDK channel-only subscription through its finite committed receiver.</summary>
    public static WebApplication UseFiniteDevelopmentRouteCatalogReceiver(WebApplication app, Func<RouteEndpoint, string?> operation,
        Func<RouteEndpoint, RouteCatalogSidecarTransport?> sidecar)
        => UseFiniteDevelopmentRouteCatalogReceiver(app, operation, false, sidecar);

    private static WebApplication UseFiniteDevelopmentRouteCatalogReceiver(WebApplication app, Func<RouteEndpoint, string?> operation,
        bool technical, Func<RouteEndpoint, RouteCatalogSidecarTransport?>? sidecar)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(operation);
        if (string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])) return app;
        var fence = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>();
        fence._technical = technical;
        fence._sidecar = sidecar;
        fence._native = technical ? app.Services.GetService<IDevelopmentNativeActorReceiver>() : null;
        fence._callbacks = technical ? app.Services.GetService<IDevelopmentDaprCallbackReceiver>() : null;
        if (technical && !app.Environment.IsDevelopment()) throw new RouteCatalogUnavailableException("Technical HTTP qualification is private Development only.");
        // The framework route source materializes new endpoint instances on each read. Share one
        // cache with the actual routing matcher, rather than comparing independently built inventories.
        app.Use(next =>
        {
            ICollection<EndpointDataSource> sources = ((IEndpointRouteBuilder)app).DataSources;
            foreach (EndpointDataSource source in sources.ToArray())
            {
                sources.Remove(source);
                sources.Add(new FiniteRouteCatalogEndpointDataSource(source));
            }
            fence._currentEndpoints = () => sources.SelectMany(source => source.Endpoints);
            return next;
        });
        fence.ReadPrefixes(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"]!);
        if (string.IsNullOrEmpty(app.Configuration[DaprAppChannelToken.ConfigurationKey]))
            throw new RouteCatalogUnavailableException("The finite Development receiver requires a configured actual sidecar channel token.");
        app.Use(async (context, next) =>
        {
            // Dapr injects its configured APP_API_TOKEN header on every app-channel delivery. Presence
            // selects stricter transport fencing; existing authorization still validates its actual value.
            if (context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName) && fence._surface is null
                && fence.Intersects(context.Request.Path.Value ?? string.Empty))
            { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return; }
            if (context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName) && fence.Intersects(context.Request.Path.Value ?? string.Empty))
            {
                if (!DaprAppChannelToken.IsAdmitted(DaprAppChannelToken.Verify(context.Request, app.Configuration, app.Environment)))
                { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
                if (context.GetEndpoint() is RouteEndpoint candidate && fence._native?.Observe(candidate) is not null && !fence._native.Allows(context))
                { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
                // A deliberately absent programmatic subscription endpoint must retain Dapr's
                // native 404 discovery protocol. It admits no request or callback execution.
                if (fence._callbacks?.IsAbsentDiscovery(context) == true)
                { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
                bool probe = HttpMethods.IsGet(context.Request.Method)
                    && EventStoreHttpEndpointInventory.IsProbe(context.Request.Path.Value)
                    && context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
                string? refusal = probe ? null : fence.RefusalBoundary(context.Request.Path.Value ?? string.Empty, context.Request.Method, context.GetEndpoint());
                if (refusal is not null)
                {
                    RouteCatalogRefusalDiagnostics.Report(app.Services.GetService<ILoggerFactory>()?.CreateLogger<FiniteRouteCatalogReceiver>(),
                        refusal, new RouteCatalogUnavailableException("The finite receiver refused the current endpoint admission.", RouteCatalogRefusalReason.Inventory));
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }
            if (fence._technical && context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName)
                && context.GetEndpoint() is RouteEndpoint nativeEndpoint && fence._native?.Observe(nativeEndpoint) is { } native)
            {
                // Immutable native configuration and health are the sidecar's authenticated
                // startup handshake; requiring activated state here would deadlock bootstrap.
                if (native.Kind is "config" or "health") { await next(context).ConfigureAwait(false); return; }
                RouteCatalogDispatchLease? nativeLease = null;
                try
                {
                    nativeLease = await app.Services.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(context.RequestAborted).ConfigureAwait(false);
                    var snapshot = nativeLease.Snapshot;
                    if (snapshot.Envelope.SchemaVersion != 3 || snapshot.Envelope.Profile != "Development" || !fence._native.AllowsSnapshot(context, snapshot)
                        || !snapshot.AuxiliaryOperations.Any(operation => operation.AppId == snapshot.LocalAppId
                            && operation.Method == nativeEndpoint.RoutePattern.RawText!.Trim('/') && operation.NativeActor == native))
                        throw new RouteCatalogUnavailableException("The actual native actor code and registration inventory is not committed.");
                }
                catch (RouteCatalogUnavailableException)
                {
                    if (nativeLease is not null) await nativeLease.DisposeAsync().ConfigureAwait(false);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return;
                }
                await using (nativeLease.ConfigureAwait(false))
                using (RouteCatalogRemoteCall.Enter(nativeLease))
                {
                    try
                    {
                        await next(context).ConfigureAwait(false);
                        if (context.Response.Headers.ContainsKey("X-Daprerrorresponseheader")) nativeLease.Retain();
                    }
                    catch { nativeLease.Retain(); throw; }
                }
                return;
            }
            if (fence._technical && context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName)
                && context.GetEndpoint() is RouteEndpoint callbackEndpoint && fence._callbacks?.Observe(callbackEndpoint) is { } callback)
            {
                // Native discovery is an authenticated immutable startup handshake. Pub/sub
                // notifications have signed publisher/body provenance, not HTTP sender stamps.
                if (callback.Kind == "subscribe") { await next(context).ConfigureAwait(false); return; }
                RouteCatalogDispatchLease? callbackLease = null;
                try
                {
                    callbackLease = await app.Services.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(context.RequestAborted).ConfigureAwait(false);
                    var snapshot = callbackLease.Snapshot;
                    if (snapshot.Envelope.SchemaVersion != 3 || snapshot.Envelope.Profile != "Development"
                        || !snapshot.AuxiliaryOperations.Any(operation => operation.AppId == snapshot.LocalAppId
                            && operation.Method == callbackEndpoint.RoutePattern.RawText!.Trim('/') && operation.DaprCallback == callback))
                        throw new RouteCatalogUnavailableException("The actual SDK callback code and subscription inventory is not committed.");
                }
                catch (RouteCatalogUnavailableException)
                {
                    if (callbackLease is not null) await callbackLease.DisposeAsync().ConfigureAwait(false);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return;
                }
                await using (callbackLease.ConfigureAwait(false))
                using (RouteCatalogRemoteCall.Enter(callbackLease))
                {
                    try { await next(context).ConfigureAwait(false); }
                    catch { callbackLease.Retain(); throw; }
                }
                return;
            }
            if (context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName)
                && context.GetEndpoint() is RouteEndpoint sidecarEndpoint && fence._sidecar?.Invoke(sidecarEndpoint) is { } sidecar)
            {
                // Native pub/sub delivery carries the real app-channel token, not a fabricated
                // workload sender stamp. Bind its actual delegate/topic to the committed local host.
                RouteCatalogDispatchLease? sidecarLease = null;
                try
                {
                    sidecarLease = await app.Services.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(context.RequestAborted).ConfigureAwait(false);
                    var snapshot = sidecarLease.Snapshot;
                    if (snapshot.Envelope.SchemaVersion != 3 || snapshot.Envelope.Profile != "Development" || snapshot.LocalAppId is null
                        || !snapshot.AuxiliaryOperations.Any(operation => operation.AppId == snapshot.LocalAppId
                            && operation.Method == sidecarEndpoint.RoutePattern.RawText!.Trim('/')
                            && operation.Operation == "sdk-sidecar:domain-events" && operation.Sidecar == sidecar))
                        throw new RouteCatalogUnavailableException("The actual SDK subscription code, component and topic are not committed.");
                }
                catch (RouteCatalogUnavailableException)
                {
                    if (sidecarLease is not null) await sidecarLease.DisposeAsync().ConfigureAwait(false);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return;
                }
                await using (sidecarLease.ConfigureAwait(false))
                using (RouteCatalogRemoteCall.Enter(sidecarLease))
                {
                    try { await next(context).ConfigureAwait(false); }
                    catch { sidecarLease.Retain(); throw; }
                }
                return;
            }
            if (fence._technical && context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName)
                && context.GetEndpoint() is RouteEndpoint endpoint && DevelopmentTechnicalHttpTransport.Observe(endpoint) is { } http)
            {
                RouteCatalogDispatchLease? lease = null;
                try
                {
                    lease = await app.Services.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(context.RequestAborted).ConfigureAwait(false);
                    string pattern = endpoint.RoutePattern.RawText!.Trim('/');
                    var snapshot = lease.Snapshot;
                    if (snapshot.Envelope.SchemaVersion != 3 || snapshot.Envelope.Profile != "Development" || snapshot.LocalAppId is null
                        || context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].Count != 1
                        || context.Request.Headers[RouteCatalogInvocationHandler.RootHeader][0] != snapshot.RootDigest
                        || context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader].Count != 1
                        || context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader][0] != snapshot.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        || !snapshot.AuxiliaryOperations.Any(auxiliary => auxiliary.AppId == snapshot.LocalAppId
                            && auxiliary.Method == pattern && auxiliary.Http is { } bound && bound.Owner == http.Owner && bound.CodeDigest == http.CodeDigest
                            && bound.Verbs.Split(' ').Contains(context.Request.Method, StringComparer.Ordinal)
                            && DevelopmentTechnicalHttpTransport.Matches(auxiliary, context.Request.Path.Value!, context.Request.Method)))
                        throw new RouteCatalogUnavailableException("The technical receiver does not bind this committed code, verb and generation.");
                }
                catch (RouteCatalogUnavailableException)
                {
                    if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }
                await using (lease.ConfigureAwait(false))
                using (RouteCatalogRemoteCall.Enter(lease)) await next(context).ConfigureAwait(false);
                return;
            }
            await next(context).ConfigureAwait(false);
        });
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try { fence.Capture(fence._currentEndpoints!(),
                app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"]!, operation); }
            catch (Exception exception) { fence._captured.TrySetException(exception); throw; }
        });
        return app;
    }

    /// <summary>Returns actual retained metadata only after the receiver has installed its finite allow table.</summary>
    public RouteCatalogReceiverSurface Observe()
    {
        if (_surface is null) throw new RouteCatalogUnavailableException("The actual finite receiver fence has not captured its endpoint inventory.");
        if (_currentEndpoints is null) return _surface;
        return new(_technical ? TechnicalVersion : Version, ReadEndpoints(_currentEndpoints(), enroll: false));
    }

    /// <summary>Waits for the actual ApplicationStarted inventory capture within the owning startup cancellation bound.</summary>
    public Task WaitForCaptureAsync(CancellationToken token) => _captured.Task.WaitAsync(token);

    internal void Capture(IEnumerable<Endpoint> endpoints, string configurationPath, Func<RouteEndpoint, string?> operation)
    {
        _operation = operation;
        ReadPrefixes(configurationPath);
        _surface = new(_technical ? TechnicalVersion : Version, ReadEndpoints(endpoints, enroll: true));
        _captured.TrySetResult();
    }

    private void ReadPrefixes(string configurationPath)
    {
        using var configuration = JsonDocument.Parse(File.ReadAllBytes(configurationPath));
        var acl = configuration.RootElement.GetProperty("spec").GetProperty("accessControl");
        var grants = acl.GetProperty("policies").EnumerateArray().SelectMany(policy => policy.GetProperty("operations").EnumerateArray())
            .Where(grant => grant.GetProperty("action").GetString() == "allow")
            .Select(grant => grant.GetProperty("name").GetString() ?? string.Empty).ToArray();
        if (grants.Any(grant => !(_technical && grant is "/**" or "/*") && (grant.Trim('/').TrimEnd('*').TrimEnd('/').Length == 0 || grant.Contains('*', StringComparison.Ordinal)
            && (!grant.EndsWith("/*", StringComparison.Ordinal) || grant.Count(c => c == '*') != 1))))
            throw new RouteCatalogUnavailableException("The actual receiver ACL contains an unsupported wildcard shape.");
        _prefixes = grants.Select(grant => "/" + grant.Trim('/').TrimEnd('*').TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _grants.Clear();
        if (_technical)
        foreach (var grant in acl.GetProperty("policies").EnumerateArray().SelectMany(policy => policy.GetProperty("operations").EnumerateArray())
            .Where(grant => grant.GetProperty("action").GetString() == "allow"))
            _grants.Add(("/" + grant.GetProperty("name").GetString()!.Trim('/').TrimEnd('*').TrimEnd('/'),
                grant.GetProperty("httpVerb").EnumerateArray().Select(verb => verb.GetString()!).ToArray(), grant.GetProperty("name").GetString()!));
    }

    private RouteCatalogReceiverEndpoint[] ReadEndpoints(IEnumerable<Endpoint> endpoints, bool enroll)
    {
        var inventory = new List<RouteCatalogReceiverEndpoint>();
        foreach (var endpoint in endpoints)
        {
            if (endpoint is not RouteEndpoint route)
                throw new RouteCatalogUnavailableException("An unknown endpoint cannot qualify the actual receiver surface.");
            string pattern = "/" + (route.RoutePattern.RawText ?? throw new RouteCatalogUnavailableException("An actual endpoint lacks its route pattern.")).Trim('/');
            string[] methods = route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [];
            string[] policies = route.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
            bool anonymous = route.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            bool isPattern = route.RoutePattern.Parameters.Count > 0 || pattern.Contains('{', StringComparison.Ordinal) || pattern.Contains('*', StringComparison.Ordinal);
            var native = _technical ? _native?.Observe(route) : null;
            var callback = _technical ? _callbacks?.Observe(route) : null;
            var sidecar = _sidecar?.Invoke(route);
            if (native?.Kind == "health" && methods.Length == 0) methods = ["GET"];
            string? canonical = callback is not null ? "sdk-dapr-callback:" + callback.Kind : native is null ? _operation!(route) : "sdk-native-actor:" + native.Kind;
            var http = _technical ? DevelopmentTechnicalHttpTransport.Observe(route) : null;
            if (http is not null) http = http with { Grants = _grants.Where(grant => DevelopmentTechnicalHttpTransport.GrantReaches(grant.Method, pattern))
                .Select(grant => new RouteCatalogHttpGrant(grant.Method, string.Join(' ', grant.Verbs.Where(verb => methods.Contains(verb, StringComparer.Ordinal)).Order(StringComparer.Ordinal)))).Where(grant => grant.Verbs.Length > 0).OrderBy(grant => grant.Method, StringComparer.Ordinal).ToArray() };
            bool admitted = !anonymous && (!isPattern || http is not null || native is not null) && methods.Length > 0 && policies.Length > 0 && policies.All(p => p.Length > 0) && !string.IsNullOrEmpty(canonical);
            if (http is not null) admitted = admitted && methods.All(verb => _grants.Any(grant => grant.Verbs.Contains(verb, StringComparer.Ordinal)
                && DevelopmentTechnicalHttpTransport.GrantReaches(grant.Method, pattern)));
            if (admitted && enroll)
                foreach (string verb in methods)
                    if (!_allowed.TryAdd((pattern.ToLowerInvariant(), verb), (route, canonical!, policies, http, native, sidecar, callback)))
                        throw new RouteCatalogUnavailableException("The finite receiver has ambiguous concrete operations.");
            if (!enroll) admitted = admitted && methods.All(verb => RefusalBoundary(pattern, verb, route, runtime: false) is null);
            inventory.Add(new(pattern, methods, canonical, policies, anonymous, isPattern, admitted) { Http = http, NativeActor = native, Sidecar = sidecar, DaprCallback = callback });
        }
        return inventory.OrderBy(e => e.Pattern, StringComparer.Ordinal).ThenBy(e => string.Join(',', e.Methods), StringComparer.Ordinal).ToArray();
    }

    private bool Intersects(string path) => _prefixes.Any(prefix => path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || prefix == "/" || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));

    internal bool Allows(string path, string verb, Endpoint? endpoint) => RefusalBoundary(path, verb, endpoint) is null;

    private string? RefusalBoundary(string path, string verb, Endpoint? endpoint, bool runtime = true)
    {
        if (_surface is null) return "finite-capture-pending";
        string key = _technical && endpoint is RouteEndpoint route && (DevelopmentTechnicalHttpTransport.Observe(route) is not null || _native?.Observe(route) is not null || _callbacks?.Observe(route) is not null)
            ? "/" + route.RoutePattern.RawText!.Trim('/') : path;
        if (!_allowed.TryGetValue((key.ToLowerInvariant(), verb), out var actual)) return "finite-operation-unenrolled";
        if (actual.Http is { } captured && (DevelopmentTechnicalHttpTransport.Observe((RouteEndpoint)endpoint!) is not { } observed
            || observed.Owner != captured.Owner || observed.Verbs != captured.Verbs || observed.CodeDigest != captured.CodeDigest)) return "finite-code-changed";
        if (runtime && actual.Http is not null && !_grants.Any(grant => grant.Verbs.Contains(verb, StringComparer.Ordinal)
            && DevelopmentTechnicalHttpTransport.GrantAllows(grant.Method, path))) return "finite-acl-path-ungranted";
        if (actual.Native is not null && _native?.Observe((RouteEndpoint)endpoint!) != actual.Native) return "finite-native-inventory-changed";
        if (actual.Sidecar is not null && _sidecar?.Invoke((RouteEndpoint)endpoint!) != actual.Sidecar) return "finite-subscription-owner-changed";
        if (actual.Callback is not null && _callbacks?.Observe((RouteEndpoint)endpoint!) != actual.Callback) return "finite-callback-inventory-changed";
        if (!ReferenceEquals(actual.Endpoint, endpoint)) return "finite-endpoint-changed";
        if (endpoint!.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || (actual.Native?.Kind == "health" ? verb != "GET" || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Count is > 0
                : endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(verb, StringComparer.Ordinal) != true)
            || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? string.Empty).Order(StringComparer.Ordinal)
                .SequenceEqual(actual.Policies, StringComparer.Ordinal)) return "finite-policy-changed";
        return (actual.Callback is { } callback ? "sdk-dapr-callback:" + callback.Kind : actual.Native is { } native ? "sdk-native-actor:" + native.Kind : _operation!(actual.Endpoint)) == actual.Operation ? null : "finite-operation-changed";
    }
}
