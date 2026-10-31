using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Fences opted-in Dapr human ingress to actual generated public GET actions and the SDK catalog filter.</summary>
public sealed class GeneratedRestRouteCatalogReceiver
{
    private readonly Dictionary<RouteEndpoint, string> _captured = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<RouteEndpoint, string> _technical = new(ReferenceEqualityComparer.Instance);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<IEnumerable<Endpoint>>? _endpoints;
    private IServiceProvider? _services;

    /// <summary>Installs a separate generated-query fence without changing finite service dispatch.</summary>
    public static WebApplication UseDevelopmentGeneratedRestCatalogReceiver(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (string.IsNullOrEmpty(app.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"])) return app;
        if (!app.Environment.IsDevelopment() || string.IsNullOrEmpty(app.Configuration[DaprAppChannelToken.ConfigurationKey]))
            throw new RouteCatalogUnavailableException("Generated Development ingress requires its owning profile and actual app-channel token.");
        var fence = app.Services.GetRequiredService<GeneratedRestRouteCatalogReceiver>();
        fence._services = app.Services;
        app.Use(next =>
        {
            ICollection<EndpointDataSource> sources = ((IEndpointRouteBuilder)app).DataSources;
            foreach (EndpointDataSource source in sources.ToArray())
            {
                sources.Remove(source);
                sources.Add(new FiniteRouteCatalogEndpointDataSource(source));
            }
            fence._endpoints = () => sources.SelectMany(source => source.Endpoints);
            return next;
        });
        app.Use(async (context, next) =>
        {
            if (!context.Request.Headers.ContainsKey(DaprAppChannelToken.HeaderName))
            {
                await next(context).ConfigureAwait(false);
                return;
            }
            if (!DaprAppChannelToken.IsAdmitted(DaprAppChannelToken.Verify(context.Request, app.Configuration, app.Environment)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (context.GetEndpoint() is RouteEndpoint technical && fence._ready.Task.IsCompletedSuccessfully
                && fence._technical.TryGetValue(technical, out string? capturedTechnical)
                && fence.DescribeTechnical(technical) is { } currentTechnical && currentTechnical == capturedTechnical
                && (!capturedTechnical.StartsWith("probe:", StringComparison.Ordinal) || HttpMethods.IsGet(context.Request.Method)))
            {
                // Only captured exact probes and SDK-mapped callbacks reach their existing
                // anonymous-probe or workload authorization boundary. No human policy substitutes for it.
                await next(context).ConfigureAwait(false);
                return;
            }
            if (context.GetEndpoint() is not RouteEndpoint selected || !HttpMethods.IsGet(context.Request.Method)
                || !fence._ready.Task.IsCompletedSuccessfully || !fence._captured.TryGetValue(selected, out string? fingerprint)
                || fence.Describe(selected) is not { } actual || Fingerprint(actual) != fingerprint)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            AuthenticateResult human = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme).ConfigureAwait(false);
            if (!human.Succeeded || human.Principal is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            AuthorizationResult admitted = await context.RequestServices.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(human.Principal, context, EventStoreHttpHostSecurityExtensions.HumanPolicy).ConfigureAwait(false);
            if (!admitted.Succeeded || RouteCatalogSurfacePolicy.Surface(human.Principal) is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            // The installed real SDK action filter acquires the current committed lease and
            // validates the actual resolved tenant, full declaration, schemas and eligibility.
            await next(context).ConfigureAwait(false);
        });
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                foreach (RouteEndpoint endpoint in fence.Current())
                {
                    if (fence.Describe(endpoint) is { } description) fence._captured.Add(endpoint, Fingerprint(description));
                    if (fence.DescribeTechnical(endpoint) is { } technical) fence._technical.Add(endpoint, technical);
                }
                if (fence._captured.Count == 0) throw new RouteCatalogUnavailableException("No qualified generated public query receiver is installed.");
                fence._ready.TrySetResult();
            }
            catch (Exception failure) { fence._ready.TrySetException(failure); throw; }
        });
        return app;
    }

    /// <summary>Waits for the independently captured real endpoint source.</summary>
    public Task WaitForCaptureAsync(CancellationToken cancellationToken) => _ready.Task.WaitAsync(cancellationToken);

    /// <summary>Recaptures actual metadata, retaining admission only for unchanged endpoint identities.</summary>
    public RouteCatalogReceiverSurface Observe()
    {
        if (!_ready.Task.IsCompletedSuccessfully) throw new RouteCatalogUnavailableException("The generated receiver has not captured its real endpoint inventory.");
        List<RouteCatalogGeneratedRestEndpoint> generated = [];
        List<RouteCatalogReceiverEndpoint> endpoints = [];
        foreach (RouteEndpoint endpoint in Current())
        {
            var description = Describe(endpoint);
            bool admitted = description is not null && _captured.TryGetValue(endpoint, out string? prior) && Fingerprint(description) == prior;
            bool technical = _technical.TryGetValue(endpoint, out string? capturedTechnical) && DescribeTechnical(endpoint) == capturedTechnical;
            string pattern = Pattern(endpoint);
            string[] methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [];
            string[] policies = Policies(endpoint);
            if (admitted) generated.Add(description!);
            string? operation = admitted ? "generated-human-query" : technical && endpoint.Metadata.GetMetadata<RouteCatalogControlEndpointMetadata>() is not null
                ? pattern == "/" + RouteCatalogEndpointExtensions.ReadbackMethod ? EventStoreOperationIdentifiers.RouteCatalogRead : EventStoreOperationIdentifiers.RouteCatalogActivate : null;
            endpoints.Add(new(pattern, methods, operation, policies,
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null, endpoint.RoutePattern.Parameters.Count > 0, admitted || technical && operation is not null));
        }
        return new(RouteCatalogGeneratedRestPolicy.FenceVersion, endpoints.OrderBy(value => value.Pattern, StringComparer.Ordinal).ToArray())
        {
            GeneratedRest = generated.OrderBy(value => value.Pattern, StringComparer.Ordinal).ToArray(),
        };
    }

    private IEnumerable<RouteEndpoint> Current() => (_endpoints?.Invoke() ?? throw new RouteCatalogUnavailableException("The generated endpoint source is unavailable."))
        .Select(endpoint => endpoint as RouteEndpoint ?? throw new RouteCatalogUnavailableException("Unknown generated endpoint source."));

    private RouteCatalogGeneratedRestEndpoint? Describe(RouteEndpoint endpoint)
    {
        var metadata = endpoint.Metadata.GetMetadata<RouteCatalogEndpointAttribute>();
        var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        string[] policies = Policies(endpoint);
        IServiceProvider services = _services ?? throw new RouteCatalogUnavailableException("The generated receiver services are unavailable.");
        var effectivePolicy = services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy(EventStoreHttpHostSecurityExtensions.HumanPolicy);
        if (metadata is not { Kind: "query" } || action is null || endpoint.RoutePattern.Parameters.Any(parameter => parameter.IsCatchAll) || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.SequenceEqual(["GET"], StringComparer.Ordinal) != true
            || !policies.SequenceEqual([EventStoreHttpHostSecurityExtensions.HumanPolicy], StringComparer.Ordinal)
            || endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => !string.IsNullOrEmpty(data.Roles) || !string.IsNullOrEmpty(data.AuthenticationSchemes))
            || effectivePolicy?.AuthenticationSchemes.SequenceEqual([JwtBearerDefaults.AuthenticationScheme], StringComparer.Ordinal) != true
            || effectivePolicy.Requirements.Count != 2 || !effectivePolicy.Requirements.Any(requirement => requirement is EventStoreHumanCredentialRequirement)
            || !effectivePolicy.Requirements.Any(requirement => requirement is DenyAnonymousAuthorizationRequirement)
            || !action.FilterDescriptors.Any(filter => filter.Filter is ServiceFilterAttribute service && service.ServiceType == typeof(GeneratedRouteCatalogFilter))
            || !services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(GeneratedRouteCatalogFilter))) return null;
        string? domain = metadata.ContractType.GetProperty("Domain")?.GetValue(null) as string;
        string? discriminator = metadata.ContractType.GetProperty("QueryType")?.GetValue(null) as string;
        var declarations = CompiledRouteCatalogDeclarations.Read([metadata.ContractType.Assembly]);
        var declaration = declarations.SingleOrDefault(value => value.Kind == "query" && value.Domain == domain && value.MessageType == discriminator);
        if (declaration is not { Execution: not null } || declaration.Execution.HandlerType != metadata.ContractType.FullName) return null;
        var operation = CompiledRouteCatalogDeclarations.ReadOperations([metadata.ContractType.Assembly]).SingleOrDefault(value => value.RouteId == declaration.RouteId);
        if (operation is not { Ingress: "public" }) return null;
        return new(Pattern(endpoint), declaration.RouteId, RouteCatalogCodec.ComputeDeclarationDigest(declaration),
            operation.Digest, metadata.ContractType.FullName!, policies);
    }

    private string? DescribeTechnical(RouteEndpoint endpoint)
    {
        string path = Pattern(endpoint);
        string[] methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [];
        string[] policies = Policies(endpoint);
        if (endpoint.RoutePattern.Parameters.Count != 0) return null;
        bool anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        // MapHealthChecks intentionally has no verb metadata. Its exact captured probe
        // still requires GET at the request boundary; this does not admit other anonymous routes.
        if ((methods.Length == 0 || methods.SequenceEqual(["GET"], StringComparer.Ordinal))
            && EventStoreHttpEndpointInventory.IsProbe(path) && anonymous && policies.Length == 0)
            return "probe:" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { path, methods, policies, anonymous })));
        if (methods.Length != 1) return null;
        if (anonymous || endpoint.Metadata.GetMetadata<RouteCatalogControlEndpointMetadata>() is null
            || !RouteCatalogEndpointExtensions.IsControlMethod(path.TrimStart('/'), new HttpMethod(methods[0]))) return null;
        string operation = path == "/" + RouteCatalogEndpointExtensions.ReadbackMethod ? EventStoreOperationIdentifiers.RouteCatalogRead : EventStoreOperationIdentifiers.RouteCatalogActivate;
        string policy = EventStoreWorkloadAuthenticationDefaults.OperationPolicy(RouteCatalogEndpointExtensions.WorkloadScheme, operation);
        var effective = _services!.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy(policy);
        if (!policies.SequenceEqual([policy], StringComparer.Ordinal)
            || effective?.AuthenticationSchemes.SequenceEqual([RouteCatalogEndpointExtensions.WorkloadScheme], StringComparer.Ordinal) != true
            || effective.Requirements.Count != 3 || !effective.Requirements.Any(requirement => requirement is DenyAnonymousAuthorizationRequirement)
            || !effective.Requirements.OfType<ClaimsAuthorizationRequirement>().Any(requirement => requirement.ClaimType == EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)
            || !effective.Requirements.OfType<ClaimsAuthorizationRequirement>().Any(requirement => requirement.ClaimType == EventStoreWorkloadAuthenticationDefaults.OperationClaimType
                && requirement.AllowedValues?.SequenceEqual([operation], StringComparer.Ordinal) == true)) return null;
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { path, methods, policies, anonymous, operation })));
    }

    private static string Pattern(RouteEndpoint endpoint) => "/" + (endpoint.RoutePattern.RawText ?? throw new RouteCatalogUnavailableException("A generated endpoint lacks its actual route template.")).Trim('/');
    private static string[] Policies(RouteEndpoint endpoint) => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(value => value.Policy ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
    private static string Fingerprint(RouteCatalogGeneratedRestEndpoint endpoint) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(endpoint)));
}
