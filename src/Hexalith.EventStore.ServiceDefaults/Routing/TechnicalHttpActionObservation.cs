using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Recaptures actual installed SDK MVC action, loaded code, binding, schema, policy and filter identities.</summary>
internal static class TechnicalHttpActionObservation
{
    internal static bool IsMarked(ControllerActionDescriptor action) =>
        action.ControllerTypeInfo.IsDefined(typeof(EventStoreTechnicalHttpControllerAttribute), inherit: false)
        || action.MethodInfo.IsDefined(typeof(EventStoreTechnicalHttpControllerAttribute), inherit: false);

    internal static bool IsCoreAction(ControllerActionDescriptor action) => action.MethodInfo.DeclaringType?.Assembly.GetName().Name == "Hexalith.EventStore.Gateway"
        && action.MethodInfo.DeclaringType.Namespace == "Hexalith.EventStore.Controllers"
        && (action.MethodInfo.DeclaringType.Name, action.MethodInfo.Name) is ("CommandsController", "Submit" or "SubmitWorkload" or "SubmitBoundWorkload")
            or ("QueriesController", "Submit" or "SubmitBoundOwnerAuthorityAsync") or ("StreamsController", "ReadStreamAsync" or "ReadWorkloadStreamAsync")
            or ("RoutingCatalogMetadataController", "Get");

    internal static bool DedicatedWorkloadPolicy(AuthorizationPolicy? policy, string scheme, string operation) => policy is not null
        && policy.AuthenticationSchemes.SequenceEqual([scheme], StringComparer.Ordinal) && policy.Requirements.Count == 3
        && policy.Requirements.Any(requirement => requirement is DenyAnonymousAuthorizationRequirement)
        && policy.Requirements.OfType<ClaimsAuthorizationRequirement>().Any(claim => claim.ClaimType == EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType && claim.AllowedValues is null)
        && policy.Requirements.OfType<ClaimsAuthorizationRequirement>().Any(claim => claim.ClaimType == EventStoreWorkloadAuthenticationDefaults.OperationClaimType
            && claim.AllowedValues?.SequenceEqual([operation], StringComparer.Ordinal) == true);

    internal static RouteCatalogTechnicalHttpEndpoint? Describe(RouteEndpoint endpoint, IServiceProvider services, TechnicalHttpObservationPass pass, bool required = false)
    {
        try { return DescribeCore(endpoint, services, pass); }
        catch (Exception failure) when (!required && failure is InvalidOperationException or NotSupportedException or ArgumentException or IOException or RouteCatalogUnavailableException)
        { return null; }
    }

    private static RouteCatalogTechnicalHttpEndpoint? DescribeCore(RouteEndpoint endpoint, IServiceProvider services, TechnicalHttpObservationPass pass)
    {
        var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is null || !IsMarked(action)
            || action.ControllerTypeInfo.Assembly.GetName().Name is not ("Hexalith.EventStore.Gateway" or "Hexalith.EventStore.Admin.Server")
            || action.MethodInfo.DeclaringType != action.ControllerTypeInfo.AsType()
            || !DevelopmentTechnicalHttpTransport.ExecutesAction(endpoint.RequestDelegate, action)
            || !services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.Contains(action)
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint.RoutePattern.Parameters.Any(parameter => parameter.IsCatchAll)
            || endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Count != 0) throw new RouteCatalogUnavailableException("technical-http-action-source");
        string[] methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Order(StringComparer.Ordinal).ToArray() ?? [];
        string[] declaredMethods = action.MethodInfo.GetCustomAttributes().OfType<Microsoft.AspNetCore.Mvc.Routing.IActionHttpMethodProvider>()
            .SelectMany(attribute => attribute.HttpMethods).Order(StringComparer.Ordinal).ToArray();
        if (methods.Length != 1 || !methods.SequenceEqual(declaredMethods, StringComparer.Ordinal)) throw new RouteCatalogUnavailableException("technical-http-http-method");
        IAuthorizeData[] installed = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
        IAuthorizeData[] declared = action.ControllerTypeInfo.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.MethodInfo.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
        string Authorization(IAuthorizeData data) => $"{data.Policy}|{data.Roles}|{data.AuthenticationSchemes}";
        string[] policies = installed.Select(data => data.Policy ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
        if (!installed.Select(Authorization).Order(StringComparer.Ordinal).SequenceEqual(declared.Select(Authorization).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || installed.Any(data => !string.IsNullOrEmpty(data.Roles) || !string.IsNullOrEmpty(data.AuthenticationSchemes))) throw new RouteCatalogUnavailableException("technical-http-authorization-metadata");
        bool workload = policies.Length == 1 && policies[0].StartsWith("DaprInternal:", StringComparison.Ordinal);
        if (!workload && !policies.Contains(EventStoreHttpHostSecurityExtensions.HumanPolicy, StringComparer.Ordinal))
            throw new RouteCatalogUnavailableException("technical-http-credential-kind");
        List<object> effectivePolicies = [];
        foreach (string name in policies)
        {
            var policy = pass.Policy(name);
            if (workload)
            {
                string operation = name["DaprInternal:".Length..];
                if (string.IsNullOrWhiteSpace(operation) || !DedicatedWorkloadPolicy(policy, "DaprInternal", operation))
                    throw new RouteCatalogUnavailableException("technical-http-effective-workload-policy");
                effectivePolicies.Add(new { name, schemes = policy!.AuthenticationSchemes, requirements = policy.Requirements.Select(requirement => requirement is ClaimsAuthorizationRequirement claim
                    ? $"claim:{claim.ClaimType}:{string.Join(',', (claim.AllowedValues ?? []).Order(StringComparer.Ordinal))}" : requirement.GetType().AssemblyQualifiedName).Order(StringComparer.Ordinal).ToArray() });
                continue;
            }
            if (policy is null || !policy.AuthenticationSchemes.SequenceEqual([JwtBearerDefaults.AuthenticationScheme], StringComparer.Ordinal)
                || !policy.Requirements.Any(requirement => requirement is DenyAnonymousAuthorizationRequirement)
                || !policy.Requirements.Any(requirement => requirement is EventStoreHumanCredentialRequirement)
                || policy.Requirements.Any(requirement => requirement is not (DenyAnonymousAuthorizationRequirement or EventStoreHumanCredentialRequirement or ClaimsAuthorizationRequirement))) throw new RouteCatalogUnavailableException("technical-http-effective-human-policy");
            string[]? roles = name switch
            {
                "AdminReadOnly" => ["Admin", "Operator", "ReadOnly"],
                "AdminOperator" => ["Admin", "Operator"],
                "AdminFull" => ["Admin"],
                _ => null,
            };
            var claims = policy.Requirements.OfType<ClaimsAuthorizationRequirement>().ToArray();
            if (name == EventStoreHttpHostSecurityExtensions.HumanPolicy ? claims.Length != 0
                : roles is null || claims.Length != 1 || claims[0].ClaimType != "eventstore:admin-role"
                    || claims[0].AllowedValues?.Order(StringComparer.Ordinal).SequenceEqual(roles, StringComparer.Ordinal) != true) throw new RouteCatalogUnavailableException("technical-http-effective-admin-policy");
            effectivePolicies.Add(new { name, schemes = policy.AuthenticationSchemes, requirements = policy.Requirements.Select(requirement =>
                requirement is ClaimsAuthorizationRequirement claim ? $"claim:{claim.ClaimType}:{string.Join(',', claim.AllowedValues!.Order(StringComparer.Ordinal))}"
                    : requirement.GetType().AssemblyQualifiedName).Order(StringComparer.Ordinal).ToArray() });
        }
        var expectedFilters = action.ControllerTypeInfo.GetCustomAttributes<ServiceFilterAttribute>()
            .Concat(action.MethodInfo.GetCustomAttributes<ServiceFilterAttribute>()).ToArray();
        // MVC scope10 is global. The bootstrap's SDK generated-route filter is separately
        // installed for business endpoints and is inert on these unmarked technical actions.
        var globalServices = action.FilterDescriptors.Where(descriptor => descriptor.Scope == 10)
            .Select(descriptor => descriptor.Filter).OfType<ServiceFilterAttribute>().ToArray();
        if (globalServices.Any(filter => filter.ServiceType != typeof(GeneratedRouteCatalogFilter)))
            throw new RouteCatalogUnavailableException("technical-http-unknown-global-filter");
        var installedFilters = action.FilterDescriptors.Where(descriptor => descriptor.Scope != 10)
            .Select(descriptor => descriptor.Filter).OfType<ServiceFilterAttribute>().ToArray();
        string Filter(ServiceFilterAttribute filter) => $"{filter.ServiceType.AssemblyQualifiedName}|{filter.Order}|{filter.IsReusable}";
        if (!expectedFilters.Select(Filter).Order(StringComparer.Ordinal).SequenceEqual(installedFilters.Select(Filter).Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new RouteCatalogUnavailableException("technical-http-service-filter-metadata");
        using var scope = services.CreateScope();
        List<object> filters = [];
        foreach (var descriptor in action.FilterDescriptors)
        {
            Type implementation = descriptor.Filter is ServiceFilterAttribute service
                ? scope.ServiceProvider.GetRequiredService(service.ServiceType).GetType() : descriptor.Filter.GetType();
            if (descriptor.Filter is ServiceFilterAttribute expected && implementation != expected.ServiceType) throw new RouteCatalogUnavailableException("technical-http-service-filter-implementation");
            filters.Add(new { settings = FilterSettings(descriptor.Filter), type = implementation.FullName, code = pass.AssemblyIdentity(implementation.Assembly), descriptor.Order, descriptor.Scope,
                service = descriptor.Filter is ServiceFilterAttribute factory ? Filter(factory) : null });
        }
        var parameters = action.Parameters.OfType<ControllerParameterDescriptor>().ToArray();
        if (parameters.Length != action.Parameters.Count || parameters.Any(parameter =>
            parameter.ParameterInfo != action.MethodInfo.GetParameters().SingleOrDefault(value => value.Name == parameter.Name)
            || parameter.ParameterType != parameter.ParameterInfo.ParameterType || !BindingMatches(parameter, endpoint, services))) throw new RouteCatalogUnavailableException("technical-http-parameter-binding");
        var requests = parameters.Where(parameter => parameter.ParameterType != typeof(CancellationToken)
            && parameter.BindingInfo?.BindingSource?.Id != "Services").Select(parameter => new
            {
                parameter.Name, type = pass.TypeIdentity(parameter.ParameterType),
                source = parameter.BindingInfo?.BindingSource?.Id, parameter.BindingInfo?.BinderModelName,
                binder = parameter.BindingInfo?.BinderType is { } binder ? pass.TypeIdentity(binder) : null, parameter.BindingInfo?.EmptyBodyBehavior,
                schema = typeof(IFormFile).IsAssignableFrom(parameter.ParameterType) ? "http-multipart-file" : pass.Schema(parameter.ParameterType),
            }).ToArray();
        var responses = endpoint.Metadata.GetOrderedMetadata<ProducesResponseTypeAttribute>()
            .DistinctBy(response => (response.StatusCode, response.Type)).OrderBy(response => response.StatusCode)
            .ThenBy(response => response.Type.FullName, StringComparer.Ordinal).ToArray();
        var declaredResponses = action.ControllerTypeInfo.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Concat(action.MethodInfo.GetCustomAttributes<ProducesResponseTypeAttribute>()).DistinctBy(response => (response.StatusCode, response.Type)).OrderBy(response => response.StatusCode)
            .ThenBy(response => response.Type.FullName, StringComparer.Ordinal).ToArray();
        if (!responses.Select(response => (response.StatusCode, response.Type)).SequenceEqual(declaredResponses.Select(response => (response.StatusCode, response.Type)))
            || !responses.Any(response => response.StatusCode is >= 200 and < 300 && (response.Type != typeof(void) || response.StatusCode == StatusCodes.Status204NoContent))) throw new RouteCatalogUnavailableException("technical-http-response-metadata");
        return new(Pattern(endpoint), methods, $"{pass.AssemblyIdentity(action.ControllerTypeInfo.Assembly)}:{action.MethodInfo.MetadataToken}:{action.MethodInfo}",
            Hash(new { parameters = requests, constraints = TechnicalHttpRouteTemplate.ObserveConstraints(endpoint, services, pass) }), Hash(responses.Select(response => new { response.StatusCode, type = pass.TypeIdentity(response.Type), schema = pass.Schema(response.Type) }).ToArray()),
            policies, Hash(effectivePolicies), Hash(filters)) { CredentialKind = workload ? "workload" : null };
    }

    internal static string Pattern(RouteEndpoint endpoint) => "/" + (endpoint.RoutePattern.RawText
        ?? throw new RouteCatalogUnavailableException("The actual technical endpoint has no route pattern.")).Trim('/');

    internal static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static bool BindingMatches(ControllerParameterDescriptor parameter, RouteEndpoint endpoint, IServiceProvider services)
    {
        var metadata = services.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(parameter.ParameterType);
        var declared = BindingInfo.GetBindingInfo(parameter.ParameterInfo.GetCustomAttributes(), metadata);
        string source = declared?.BindingSource?.Id ?? (parameter.ParameterType == typeof(CancellationToken) ? "Special"
            : services.GetRequiredService<IServiceProviderIsService>().IsService(parameter.ParameterType) ? "Services"
            : metadata.IsComplexType ? "Body" : endpoint.RoutePattern.Parameters.Any(route => route.Name == parameter.Name) ? "Path" : "Query");
        var actual = parameter.BindingInfo;
        if (actual?.BindingSource?.Id != source || actual.BinderModelName != declared?.BinderModelName
            || actual.BinderType != declared?.BinderType || actual.PropertyFilterProvider is not null
            || actual.RequestPredicate is not null || actual.ServiceKey is not null) return false;
        if (actual.EmptyBodyBehavior != (declared?.EmptyBodyBehavior ?? EmptyBodyBehavior.Default)
            && !(source == "Body" && actual.EmptyBodyBehavior == EmptyBodyBehavior.Allow
                && (parameter.ParameterInfo.IsOptional || new NullabilityInfoContext().Create(parameter.ParameterInfo).ReadState == NullabilityState.Nullable))) return false;
        if (actual.BinderType is { } binder)
        {
            var installed = services.GetService(binder);
            if (installed is not null && installed.GetType() != binder) return false;
        }
        return true;
    }

    private static object FilterSettings(object filter) => filter.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 && (property.PropertyType.IsPrimitive
            || property.PropertyType.IsEnum || property.PropertyType == typeof(string) || Nullable.GetUnderlyingType(property.PropertyType)?.IsPrimitive == true))
        .OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => new { property.Name, value = property.GetValue(filter) }).ToArray();

    internal static string AssemblyIdentity(Assembly assembly)
    {
        if (string.IsNullOrEmpty(assembly.Location)) throw new RouteCatalogUnavailableException("The loaded technical SDK assembly has no independently readable image.");
        byte[] bytes = File.ReadAllBytes(assembly.Location);
        using var pe = new PEReader(new MemoryStream(bytes, writable: false));
        var metadata = pe.GetMetadataReader();
        if (metadata.GetGuid(metadata.GetModuleDefinition().Mvid) != assembly.ManifestModule.ModuleVersionId)
            throw new RouteCatalogUnavailableException("The installed technical SDK image differs from its loaded module.");
        return $"{assembly.FullName}:{assembly.ManifestModule.ModuleVersionId}:{Convert.ToHexStringLower(SHA256.HashData(bytes))}";
    }
}
