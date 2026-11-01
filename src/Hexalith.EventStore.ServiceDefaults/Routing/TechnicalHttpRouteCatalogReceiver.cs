using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Explicit technical MVC composite, preserving separate core, workload, channel and probe authorization.</summary>
public sealed class TechnicalHttpRouteCatalogReceiver
{
    private readonly Dictionary<RouteEndpoint, string> _captured = new(ReferenceEqualityComparer.Instance);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<IEnumerable<Endpoint>>? _endpoints;
    private readonly List<EndpointDataSource> _mvcSources = [];
    private Func<RouteEndpoint, string?>? _core;
    private IServiceProvider? _services;
    private IDevelopmentNativeActorReceiver? _native;
    private IDevelopmentDaprCallbackReceiver? _callbacks;

    /// <summary>Tests the exact explicit profile; an unknown configured family refuses rather than falling back.</summary>
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string? profile = configuration["EventStore:RouteCatalog:DevelopmentBootstrap:ReceiverProfile"];
        return profile is null ? false : profile == RouteCatalogTechnicalHttpPolicy.FenceVersion ? true
            : throw new RouteCatalogUnavailableException("The configured Development receiver profile is unsupported.");
    }

    /// <summary>Installs only the opted-in composite. Direct HTTP and all actual MVC action guards retain their normal behavior.</summary>
    public static WebApplication UseDevelopmentTechnicalHttpCatalogReceiver(WebApplication app, Func<RouteEndpoint, string?> core)
        => UseDevelopmentTechnicalHttpCatalogReceiver(app, core, null);

    /// <summary>Refuses observer substitution before either supported technical receiver can enroll its startup handshake.</summary>
    internal static void RequireClosedSdkObservers(IDevelopmentNativeActorReceiver? native, IDevelopmentDaprCallbackReceiver? callbacks)
    {
        if (native is not null && (native.GetType().FullName != "Hexalith.EventStore.Server.Routing.DevelopmentNativeActorReceiver"
                || native.GetType().Assembly.GetName().Name != "Hexalith.EventStore.Server")
            || callbacks is not null && (callbacks.GetType().FullName != "Hexalith.EventStore.Routing.DevelopmentDaprCallbackReceiver"
                || callbacks.GetType().Assembly.GetName().Name != "Hexalith.EventStore.Gateway"))
            throw new RouteCatalogUnavailableException("The installed native observers must be the actual closed SDK implementations.", RouteCatalogRefusalReason.Inventory);
    }

    internal static WebApplication UseDevelopmentTechnicalHttpCatalogReceiver(WebApplication app, Func<RouteEndpoint, string?> core, TechnicalHttpRouteCatalogReceiver? instance)
    {
        ArgumentNullException.ThrowIfNull(app); ArgumentNullException.ThrowIfNull(core);
        if (!IsEnabled(app.Configuration)) throw new RouteCatalogUnavailableException("The technical receiver requires explicit versioned opt-in.");
        if (!app.Environment.IsDevelopment() || string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])
            || string.IsNullOrEmpty(app.Configuration[DaprAppChannelToken.ConfigurationKey]))
            throw new RouteCatalogUnavailableException("The technical receiver requires an owned Development composition and actual channel credential.");
        var fence = instance ?? app.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>();
        fence._services = app.Services; fence._core = core;
        fence._native = app.Services.GetService<IDevelopmentNativeActorReceiver>();
        fence._callbacks = app.Services.GetService<IDevelopmentDaprCallbackReceiver>();
        RequireClosedSdkObservers(fence._native, fence._callbacks);
        app.Use(next =>
        {
            ICollection<EndpointDataSource> sources = ((IEndpointRouteBuilder)app).DataSources;
            foreach (EndpointDataSource source in sources.ToArray())
            {
                sources.Remove(source);
                var cached = new FiniteRouteCatalogEndpointDataSource(source);
                sources.Add(cached);
                if (source.GetType().Assembly == typeof(ControllerBase).Assembly
                    && source.GetType().FullName == "Microsoft.AspNetCore.Mvc.Routing.ControllerActionEndpointDataSource")
                    fence._mvcSources.Add(cached);
            }
            fence._endpoints = () => sources.SelectMany(source => source.Endpoints);
            return next;
        });
        app.Use(async (context, next) =>
        {
            bool channel = context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName);
            var selectedAction = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
            bool markedSdkAction = selectedAction?.ControllerTypeInfo.Assembly.GetName().Name is "Hexalith.EventStore.Gateway" or "Hexalith.EventStore.Admin.Server"
                && TechnicalHttpActionObservation.IsMarked(selectedAction);
            if (!channel && !markedSdkAction) { await next(context).ConfigureAwait(false); return; }
            if (channel && !DaprAppChannelToken.IsAdmitted(DaprAppChannelToken.Verify(context.Request, app.Configuration, app.Environment)))
            { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
            RouteCatalogNativeActorTransport? native = null; RouteCatalogDaprCallbackTransport? callback = null;
            if (context.GetEndpoint() is RouteEndpoint nativeEndpoint)
            {
                var nativePass = new TechnicalHttpObservationPass(app.Services);
                native = fence._native?.Observe(nativeEndpoint, nativePass); callback = fence._callbacks?.Observe(nativeEndpoint, nativePass);
                string? nativeFingerprint = native is not null || callback is not null ? fence.Fingerprint(nativeEndpoint, nativePass, out _) : null;
                nativePass.Complete();
                if (native is not null && (!fence._native!.Allows(context) || nativeFingerprint is null || native.Verbs != context.Request.Method)
                    || callback is not null && (nativeFingerprint is null || callback.Verbs != context.Request.Method))
                { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
                // These real sidecar handshake/discovery protocols must work before catalog activation.
                if (native?.Kind is "config" or "health" || callback?.Kind == "subscribe")
                { await next(context).ConfigureAwait(false); return; }
            }
            RouteCatalogTechnicalHttpEndpoint? description = null;
            if (!fence._ready.Task.IsCompletedSuccessfully || context.GetEndpoint() is not RouteEndpoint endpoint
                || !fence._captured.TryGetValue(endpoint, out string? captured) || fence.RequestFingerprint(endpoint, out description) != captured
                || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>() is { } methods && !methods.HttpMethods.Contains(context.Request.Method, StringComparer.Ordinal)
                || captured.StartsWith("probe:", StringComparison.Ordinal) && !HttpMethods.IsGet(context.Request.Method))
            {
                // An absent declarative discovery route is a closed refusal, never an
                // alternative that can skip the authentication of an admitted action.
                context.Response.StatusCode = fence._callbacks?.IsAbsentDiscovery(context) == true
                    ? StatusCodes.Status404NotFound : StatusCodes.Status403Forbidden;
                return;
            }
            if (native is not null || callback is not null)
            {
                await fence.InvokeNativeAsync(context, next, endpoint, native, callback).ConfigureAwait(false); return;
            }
            if (captured.StartsWith("technical:", StringComparison.Ordinal))
            {
                bool workload = description?.CredentialKind == "workload";
                string scheme = workload ? "DaprInternal" : JwtBearerDefaults.AuthenticationScheme;
                AuthenticateResult human = await context.AuthenticateAsync(scheme).ConfigureAwait(false);
                if (!human.Succeeded || human.Principal is null) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
                AuthorizationResult authorized = await context.RequestServices.GetRequiredService<IAuthorizationService>()
                    .AuthorizeAsync(human.Principal, context, workload ? description!.Policies.Single() : EventStoreHttpHostSecurityExtensions.HumanPolicy).ConfigureAwait(false);
                if (!authorized.Succeeded)
                { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            }
            if (captured.StartsWith("technical:", StringComparison.Ordinal) || captured.StartsWith("core:", StringComparison.Ordinal)
                && !RouteCatalogEndpointExtensions.IsControlMethod(context.Request.Path.Value!.TrimStart('/'), new HttpMethod(context.Request.Method)))
            {
                RouteCatalogDispatchLease? lease = null;
                try
                {
                    lease = await context.RequestServices.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(context.RequestAborted).ConfigureAwait(false);
                    var snapshot = lease.Snapshot;
                    if (snapshot.ActivationBindingDigest is null || string.IsNullOrEmpty(snapshot.LocalAppId)
                        || description is not null && !TechnicalHttpCatalogBinding.Contains(snapshot.TechnicalHttpReceivers, snapshot.LocalAppId, description))
                        throw new RouteCatalogUnavailableException("The installed technical receiver is absent from this committed v2 activation.", RouteCatalogRefusalReason.Inventory);
                    if (channel && description is not null && !snapshot.TechnicalHttpReceivers.Any(receiver => receiver.AppId == snapshot.LocalAppId
                        && receiver.Invocations?.Any(invocation => invocation.HttpMethods.Contains(context.Request.Method, StringComparer.Ordinal)
                            && DevelopmentTechnicalHttpTransport.GrantAllows(invocation.Method, context.Request.Path.Value!)) == true))
                    {
                        await lease.DisposeAsync().ConfigureAwait(false); lease = null;
                        context.Response.StatusCode = StatusCodes.Status403Forbidden; return;
                    }
                    // The external UI technical proxy has its existing human/channel ingress. Any
                    // supplied peer identity must be complete; enrolled business dispatch always binds.
                    if ((description is null || description.CredentialKind == "workload" || context.Request.Headers.ContainsKey(RouteCatalogInvocationHandler.RootHeader)
                        || context.Request.Headers.ContainsKey(RouteCatalogInvocationHandler.EpochHeader)
                        || context.Request.Headers.ContainsKey(RouteCatalogInvocationHandler.BindingHeader))
                        && !RouteCatalogPeerBinding.Matches(context.Request.Headers, snapshot))
                        throw new RouteCatalogUnavailableException("The incoming peer does not bind this complete committed run.");
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
                catch (RouteCatalogUnavailableException)
                {
                    if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return;
                }
                await using var release = lease!.ConfigureAwait(false);
                using var remote = RouteCatalogRemoteCall.Enter(lease);
                RouteCatalogRemoteExecution.Current = new RouteCatalogBackgroundBoundary(RouteCatalogRemoteExecution.Current!,
                    context.RequestServices.GetRequiredService<IRouteCatalogAuthority>(), lease);
                await next(context).ConfigureAwait(false);
                return;
            }
            // Controls retain dedicated workload/preactivation authorization; probes and channel
            // callbacks retain their own existing guards without catalog execution membership.
            await next(context).ConfigureAwait(false);
        });
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var pass = new TechnicalHttpObservationPass(app.Services);
                foreach (var endpoint in fence.Current())
                {
                    string? fingerprint = fence.Fingerprint(endpoint, pass, out _);
                    var action = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
                    if (fence.IsActualMvcEndpoint(endpoint)
                        && action?.ControllerTypeInfo.Assembly.GetName().Name is "Hexalith.EventStore.Gateway" or "Hexalith.EventStore.Admin.Server"
                        && (TechnicalHttpActionObservation.IsMarked(action) || TechnicalHttpActionObservation.IsCoreAction(action))
                        && fingerprint is null)
                    {
                        string reason = "technical-http-mvc-delegate";
                        try { _ = TechnicalHttpActionObservation.Describe(endpoint, app.Services, pass, required: true); }
                        catch (Exception failure) { reason = failure is RouteCatalogUnavailableException ? failure.Message : failure.GetType().Name; }
                        throw new RouteCatalogUnavailableException($"SDK technical action did not qualify: {action.ControllerTypeInfo.FullName}.{action.MethodInfo.Name} ({TechnicalHttpActionObservation.Pattern(endpoint)}): {reason}.");
                    }
                    if (fence.IsOwnedNativePath(endpoint) && fingerprint is null)
                        throw new RouteCatalogUnavailableException("The installed SDK native endpoint did not qualify: " + TechnicalHttpActionObservation.Pattern(endpoint), RouteCatalogRefusalReason.Inventory);
                    if (endpoint.Metadata.GetMetadata<RouteCatalogControlEndpointMetadata>() is not null && fingerprint is null)
                        throw new RouteCatalogUnavailableException("The installed SDK control endpoint has changed dedicated authorization: " + TechnicalHttpActionObservation.Pattern(endpoint), RouteCatalogRefusalReason.Inventory);
                    if (fingerprint is not null)
                    {
                        fence._captured.Add(endpoint, fingerprint);
                    }
                }
                if (!fence._captured.Values.Any(value => value.StartsWith("technical:", StringComparison.Ordinal)))
                    throw new RouteCatalogUnavailableException("No installed SDK technical MVC action qualifies this receiver.");
                pass.Complete();
                fence._ready.TrySetResult();
            }
            catch (Exception failure) { fence._ready.TrySetException(failure); throw; }
        });
        return app;
    }

    /// <summary>Waits within the owning startup bound for real installed endpoint capture.</summary>
    public Task WaitForCaptureAsync(CancellationToken token) => _ready.Task.WaitAsync(token);

    /// <summary>Recaptures every real endpoint, retaining admission only for unchanged installed action identities.</summary>
    public RouteCatalogReceiverSurface Observe()
    {
        if (!_ready.Task.IsCompletedSuccessfully) throw new RouteCatalogUnavailableException("The technical receiver has not captured its actual endpoint source.");
        List<RouteCatalogReceiverEndpoint> endpoints = []; List<RouteCatalogTechnicalHttpEndpoint> technical = [];
        var pass = new TechnicalHttpObservationPass(_services!);
        foreach (var endpoint in Current())
        {
            string? current = Fingerprint(endpoint, pass, out var currentDescription);
            bool admitted = current is not null && _captured.TryGetValue(endpoint, out string? captured) && current == captured;
            var installedAction = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
            if (IsActualMvcEndpoint(endpoint) && installedAction?.ControllerTypeInfo.Assembly.GetName().Name is "Hexalith.EventStore.Gateway" or "Hexalith.EventStore.Admin.Server"
                && (TechnicalHttpActionObservation.IsMarked(installedAction) || TechnicalHttpActionObservation.IsCoreAction(installedAction)) && !admitted)
                throw new RouteCatalogUnavailableException($"The installed SDK technical action changed: {installedAction.ControllerTypeInfo.FullName}.{installedAction.MethodInfo.Name} ({TechnicalHttpActionObservation.Pattern(endpoint)}).");
            if (IsOwnedNativePath(endpoint) && !admitted)
                throw new RouteCatalogUnavailableException("The installed SDK native endpoint changed: " + TechnicalHttpActionObservation.Pattern(endpoint), RouteCatalogRefusalReason.Inventory);
            if (endpoint.Metadata.GetMetadata<RouteCatalogControlEndpointMetadata>() is not null && !admitted)
                throw new RouteCatalogUnavailableException("The installed SDK control endpoint changed: " + TechnicalHttpActionObservation.Pattern(endpoint), RouteCatalogRefusalReason.Inventory);
            var description = admitted && current!.StartsWith("technical:", StringComparison.Ordinal)
                ? currentDescription : null;
            if (description is not null) technical.Add(description);
            string? operation = description is not null ? RouteCatalogTechnicalHttpPolicy.Operation
                : admitted && current!.StartsWith("core:", StringComparison.Ordinal) ? _core!(endpoint) : null;
            var native = _native?.Observe(endpoint, pass); var callback = _callbacks?.Observe(endpoint, pass);
            if (admitted && native is not null) operation = "sdk-native-actor:" + native.Kind;
            if (admitted && callback is not null) operation = "sdk-dapr-callback:" + callback.Kind;
            endpoints.Add(new(TechnicalHttpActionObservation.Pattern(endpoint), native?.Kind == "health" ? ["GET"] : Methods(endpoint), operation, Policies(endpoint),
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null, endpoint.RoutePattern.Parameters.Count > 0, admitted && operation is not null)
                { NativeActor = native, DaprCallback = callback, TechnicalGrants = description is null ? null : ConcreteGrants(description) });
        }
        pass.Complete();
        return new(RouteCatalogTechnicalHttpPolicy.FenceVersion, endpoints.OrderBy(endpoint => endpoint.Pattern, StringComparer.Ordinal).ToArray())
        { TechnicalHttp = technical.OrderBy(endpoint => endpoint.Pattern, StringComparer.Ordinal).ToArray() };
    }

    private IReadOnlyList<RouteCatalogHttpGrant> ConcreteGrants(RouteCatalogTechnicalHttpEndpoint endpoint)
    {
        string? json = _services!.GetRequiredService<IConfiguration>()["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"];
        var invocations = json is null ? [] : System.Text.Json.JsonSerializer.Deserialize<RouteCatalogDeclaredInvocation[]>(json) ?? [];
        return invocations.Where(invocation => !invocation.Method.Contains('*', StringComparison.Ordinal)
            && invocation.HttpMethods.Any(verb => endpoint.Methods.Contains(verb, StringComparer.Ordinal)
                && TechnicalHttpRouteTemplate.Matches(endpoint.Pattern, invocation.Method.Trim('/'), verb)))
            .Select(invocation => new RouteCatalogHttpGrant(invocation.Method, string.Join(' ', invocation.HttpMethods.Intersect(endpoint.Methods, StringComparer.Ordinal).Order(StringComparer.Ordinal))))
            .Distinct().OrderBy(grant => grant.Method, StringComparer.Ordinal).ThenBy(grant => grant.Verbs, StringComparer.Ordinal).ToArray();
    }

    private async Task InvokeNativeAsync(HttpContext context, RequestDelegate next, RouteEndpoint endpoint,
        RouteCatalogNativeActorTransport? native, RouteCatalogDaprCallbackTransport? callback)
    {
        RouteCatalogDispatchLease? lease = null;
        try
        {
            lease = await context.RequestServices.GetRequiredService<IRouteCatalogAuthority>().AcquireAsync(context.RequestAborted).ConfigureAwait(false);
            var snapshot = lease.Snapshot;
            if (snapshot.Envelope.SchemaVersion != 3 || snapshot.Envelope.Profile != "Development" || snapshot.ActivationBindingDigest is null
                || native is not null && !_native!.AllowsSnapshot(context, snapshot)
                || !snapshot.AuxiliaryOperations.Any(operation => operation.AppId == snapshot.LocalAppId
                    && operation.Method == endpoint.RoutePattern.RawText!.Trim('/')
                    && (native is not null ? operation.NativeActor == native : operation.DaprCallback == callback)))
                throw new RouteCatalogUnavailableException("The actual SDK native callback code and runtime inventory is not committed.", RouteCatalogRefusalReason.Inventory);
        }
        catch (RouteCatalogUnavailableException)
        {
            if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return;
        }
        await using var release = lease.ConfigureAwait(false);
        using var scope = RouteCatalogRemoteCall.Enter(lease);
        try
        {
            await next(context).ConfigureAwait(false);
            if (native is not null && context.Response.Headers.ContainsKey("X-Daprerrorresponseheader")) lease.Retain();
        }
        catch { lease.Retain(); throw; }
    }

    private IEnumerable<RouteEndpoint> Current() => (_endpoints?.Invoke() ?? throw new RouteCatalogUnavailableException("The technical receiver endpoint source is unavailable."))
        .Select(endpoint => endpoint as RouteEndpoint ?? throw new RouteCatalogUnavailableException("The technical receiver encountered an unknown endpoint type."));

    private bool IsOwnedNativePath(RouteEndpoint endpoint)
    {
        string path = TechnicalHttpActionObservation.Pattern(endpoint);
        return _native is not null && (path is "/dapr/config" or "/healthz" || path.StartsWith("/actors/", StringComparison.Ordinal))
            || _callbacks is not null && path is "/dapr/subscribe" or "/projections/changed";
    }

    private bool IsActualMvcEndpoint(RouteEndpoint endpoint) => _mvcSources.SelectMany(source => source.Endpoints).Any(actual => ReferenceEquals(actual, endpoint));

    private string? RequestFingerprint(RouteEndpoint endpoint, out RouteCatalogTechnicalHttpEndpoint? description)
    {
        var pass = new TechnicalHttpObservationPass(_services!);
        string? fingerprint = Fingerprint(endpoint, pass, out description);
        pass.Complete(); return fingerprint;
    }

    private string? Fingerprint(RouteEndpoint endpoint, TechnicalHttpObservationPass pass, out RouteCatalogTechnicalHttpEndpoint? description)
    {
        description = null;
        var native = _native?.Observe(endpoint, pass); var callback = _callbacks?.Observe(endpoint, pass);
        if (native is not null || callback is not null)
        {
            var channel = pass.Policy(EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy);
            if (channel is null || !channel.AuthenticationSchemes.SequenceEqual([EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme], StringComparer.Ordinal)
                || channel.Requirements.Count != 2 || !channel.Requirements.Any(requirement => requirement is DenyAnonymousAuthorizationRequirement)
                || !channel.Requirements.OfType<ClaimsAuthorizationRequirement>().Any(claim => claim.ClaimType == EventStoreWorkloadAuthenticationDefaults.ChannelClaimType && claim.AllowedValues is null)) return null;
            if (native is not null) pass.RequireUnchanged(() => TechnicalHttpActionObservation.Hash(_native!.Observe(endpoint, pass)), TechnicalHttpActionObservation.Hash(native));
            if (callback is not null) pass.RequireUnchanged(() => TechnicalHttpActionObservation.Hash(_callbacks!.Observe(endpoint, pass)), TechnicalHttpActionObservation.Hash(callback));
            return native is not null ? "native:" + TechnicalHttpActionObservation.Hash(native) : "callback:" + TechnicalHttpActionObservation.Hash(callback);
        }
        if (IsOwnedNativePath(endpoint)) return null;
        if (IsActualMvcEndpoint(endpoint)
            && endpoint.RequestDelegate?.Method.Module.Assembly == typeof(ControllerBase).Assembly
            && TechnicalHttpActionObservation.Describe(endpoint, _services!, pass) is { } action)
        {
            description = action;
            return "technical:" + TechnicalHttpActionObservation.Hash(action);
        }
        var selectedAction = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
        if (selectedAction is not null && TechnicalHttpActionObservation.IsMarked(selectedAction)) return null;
        string path = TechnicalHttpActionObservation.Pattern(endpoint); string[] methods = Methods(endpoint); string[] policies = Policies(endpoint);
        bool anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        if (endpoint.RoutePattern.Parameters.Count == 0 && anonymous && policies.Length == 0 && EventStoreHttpEndpointInventory.IsProbe(path)
            && (methods.Length == 0 || methods.SequenceEqual(["GET"], StringComparer.Ordinal)))
            return "probe:" + TechnicalHttpActionObservation.Hash(new { path, methods, policies });
        if (anonymous || (EventStoreHttpEndpointInventory.ValidateAsync([endpoint], _services!).GetAwaiter().GetResult()).Count != 0) return null;
        bool protocol = path is "/dapr/subscribe" or "/dapr/config" or "/healthz" || path.StartsWith("/actors/", StringComparison.Ordinal)
            || endpoint.Metadata.Any(item => item.GetType().GetInterfaces().Any(type => type.FullName == "Dapr.ITopicMetadata"));
        if (protocol && policies.SequenceEqual([EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy], StringComparer.Ordinal))
            return "channel:" + TechnicalHttpActionObservation.Hash(new { path, methods, policies });
        if (endpoint.RoutePattern.Parameters.Count != 0 || methods.Length != 1 || _core!(endpoint) is not { } operation) return null;
        if (operation is not (EventStoreOperationIdentifiers.RouteCatalogActivate or EventStoreOperationIdentifiers.RouteCatalogRead)
            && (!IsActualMvcEndpoint(endpoint) || selectedAction?.ControllerTypeInfo.Assembly.GetName().Name != "Hexalith.EventStore.Gateway"
                || !DevelopmentTechnicalHttpTransport.ExecutesAction(endpoint.RequestDelegate, selectedAction))) return null;
        var effective = policies.Select(name => pass.Policy(name)).ToArray();
        if (effective.Any(policy => policy is null)) return null;
        string? source = null;
        if (selectedAction is not null)
        {
            if (!TechnicalHttpActionObservation.IsCoreAction(selectedAction) || selectedAction.MethodInfo.DeclaringType != selectedAction.ControllerTypeInfo.AsType()
                || endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count != 0) return null;
            string Authorization(IAuthorizeData value) => $"{value.Policy}|{value.Roles}|{value.AuthenticationSchemes}";
            var declared = selectedAction.ControllerTypeInfo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<IAuthorizeData>()
                .Concat(selectedAction.MethodInfo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<IAuthorizeData>());
            if (!endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(Authorization).Order(StringComparer.Ordinal)
                .SequenceEqual(declared.Select(Authorization).Order(StringComparer.Ordinal), StringComparer.Ordinal)) return null;
            foreach (string policy in policies.Where(policy => policy.StartsWith("DaprInternal:", StringComparison.Ordinal)))
                if (!TechnicalHttpActionObservation.DedicatedWorkloadPolicy(pass.Policy(policy), "DaprInternal", policy["DaprInternal:".Length..])) return null;
            source = pass.AssemblyIdentity(selectedAction.ControllerTypeInfo.Assembly) + ":" + selectedAction.MethodInfo.MetadataToken;
        }
        if (operation is EventStoreOperationIdentifiers.RouteCatalogActivate or EventStoreOperationIdentifiers.RouteCatalogRead)
        {
            string required = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, operation);
            if (endpoint.Metadata.GetMetadata<RouteCatalogControlEndpointMetadata>() is null
                || !RouteCatalogEndpointExtensions.IsControlMethod(path.TrimStart('/'), new HttpMethod(methods[0]))
                || !policies.SequenceEqual([required], StringComparer.Ordinal)
                || effective[0]!.AuthenticationSchemes.SequenceEqual([RouteCatalogEndpointExtensions.WorkloadScheme], StringComparer.Ordinal) != true
                || effective[0]!.Requirements.Count != 3
                || !effective[0]!.Requirements.Any(requirement => requirement is DenyAnonymousAuthorizationRequirement)
                || !effective[0]!.Requirements.OfType<ClaimsAuthorizationRequirement>().Any(claim =>
                    claim.ClaimType == EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType && claim.AllowedValues is null)
                || !effective[0]!.Requirements.OfType<ClaimsAuthorizationRequirement>().Any(claim =>
                    claim.ClaimType == EventStoreWorkloadAuthenticationDefaults.OperationClaimType && claim.AllowedValues?.SequenceEqual([operation], StringComparer.Ordinal) == true)) return null;
        }
        return "core:" + TechnicalHttpActionObservation.Hash(new { path, methods, policies, operation, source,
            effective = effective.Select(policy => new { policy!.AuthenticationSchemes, requirements = policy.Requirements.Select(requirement =>
                requirement is ClaimsAuthorizationRequirement claim ? $"claim:{claim.ClaimType}:{string.Join(',', (claim.AllowedValues ?? []).Order(StringComparer.Ordinal))}"
                    : requirement.GetType().AssemblyQualifiedName).Order(StringComparer.Ordinal).ToArray() }).ToArray() });
    }

    private static string[] Methods(RouteEndpoint endpoint) => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [];
    private static string[] Policies(RouteEndpoint endpoint) => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
}
