using System.Globalization;
using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Rest;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Validates generated-host declarations under one committed lease before gateway effects.</summary>
public sealed class GeneratedRouteCatalogFilter(IRouteCatalogAuthority authority, Microsoft.Extensions.Logging.ILogger<GeneratedRouteCatalogFilter>? logger = null) : IAsyncActionFilter
{
    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        RouteCatalogEndpointAttribute? metadata = context.ActionDescriptor.EndpointMetadata.OfType<RouteCatalogEndpointAttribute>().SingleOrDefault();
        if (metadata is null) { await next().ConfigureAwait(false); return; }
        IActionResult? scopeFailure = ValidateRequestScope(context, metadata.TenantSource);
        if (scopeFailure is not null)
        {
            context.Result = scopeFailure;
            return;
        }
        RouteCatalogDispatchLease? lease = null;
        try
        {
            lease = await authority.AcquireAsync(context.HttpContext.RequestAborted).ConfigureAwait(false);
            context.HttpContext.Items[LeaseKey] = lease;
            context.HttpContext.Items[MetadataKey] = metadata;
            context.HttpContext.Items[LoggerKey] = logger;
            context.HttpContext.Items[RouteCatalogGeneratedAction.ValidatorKey] = (Func<Type, string, string, bool>)((type, kind, tenant)
                => ValidateTenant(context.HttpContext, type, kind, tenant));
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            RouteCatalogRefusalDiagnostics.Report(logger, "generated-receiver", exception);
            if (lease is not null) await lease.DisposeAsync().ConfigureAwait(false);
            context.Result = new StatusCodeResult(503);
            return;
        }
        await using var release = lease!.ConfigureAwait(false);
        try
        {
            ActionExecutedContext executed = await next().ConfigureAwait(false);
            if (context.HttpContext.Items.ContainsKey(EligibilityRefusedKey))
                executed.Result = new StatusCodeResult(403);
            else if (!context.HttpContext.Items.ContainsKey(ValidatedKey) && executed.Exception is null
                && !IsEarlyFailure(executed.Result))
                executed.Result = new StatusCodeResult(503);
        }
        finally
        {
            context.HttpContext.Items.Remove(LeaseKey);
            context.HttpContext.Items.Remove(MetadataKey);
            context.HttpContext.Items.Remove(LoggerKey);
            context.HttpContext.Items.Remove(ValidatedKey);
            context.HttpContext.Items.Remove(EligibilityRefusedKey);
            context.HttpContext.Items.Remove(RouteCatalogGeneratedAction.ValidatorKey);
        }
    }

    private static IActionResult? ValidateRequestScope(ActionExecutingContext context, RestTenantSource source)
    {
        // Older generated actions and manually cataloged controllers have no scope-source
        // metadata. Their existing action and catalog validation remain authoritative.
        if (source == RestTenantSource.Inherit)
            return null;

        HttpRequest request = context.HttpContext.Request;
        bool hasTenantHeader = request.Headers.TryGetValue("X-Hexalith-Tenant", out var tenantHeader);
        bool hasScopeHeader = request.Headers.TryGetValue("X-Hexalith-Scope", out var scopeHeader);
        if (hasTenantHeader && hasScopeHeader)
            return ScopeProblem(StatusCodes.Status400BadRequest, "Tenant and platform scopes cannot be combined.");
        if (source == RestTenantSource.System)
            return ScopeProblem(StatusCodes.Status400BadRequest, "A public system tenant is not supported.");

        string? requestedTenant = null;
        switch (source)
        {
            case RestTenantSource.Platform:
                if (hasTenantHeader || !hasScopeHeader || scopeHeader.Count != 1
                    || !TenantCanonicalizer.IsPlatformScope(scopeHeader[0]))
                    return ScopeProblem(StatusCodes.Status400BadRequest, "An explicit canonical platform scope is required.");
                break;
            case RestTenantSource.Route:
                string? tenant = context.RouteData.Values.TryGetValue("tenant", out object? tenantValue) && tenantValue is not null
                    ? Convert.ToString(tenantValue, CultureInfo.InvariantCulture) : null;
                string? tenantId = context.RouteData.Values.TryGetValue("tenantId", out object? tenantIdValue) && tenantIdValue is not null
                    ? Convert.ToString(tenantIdValue, CultureInfo.InvariantCulture) : null;
                if (hasTenantHeader || hasScopeHeader || tenant is not null && tenantId is not null)
                    return ScopeProblem(StatusCodes.Status400BadRequest, "Duplicate or mixed tenant scopes are not allowed.");
                requestedTenant = tenant ?? tenantId;
                break;
            case RestTenantSource.Claims:
                if (hasScopeHeader || !hasTenantHeader || tenantHeader.Count != 1)
                    return ScopeProblem(StatusCodes.Status400BadRequest, "An explicit tenant header is required.");
                requestedTenant = tenantHeader[0];
                break;
            default:
                return ScopeProblem(StatusCodes.Status400BadRequest, "The request scope source is invalid.");
        }

        string canonicalTenant = string.Empty;
        if (source != RestTenantSource.Platform && !TenantCanonicalizer.TryCanonicalize(requestedTenant, out canonicalTenant))
            return ScopeProblem(StatusCodes.Status400BadRequest, "The tenant is invalid or reserved.");
        if (context.HttpContext.User.Identity?.IsAuthenticated != true
            || !TenantCanonicalizer.TryCanonicalizeGrants(
                context.HttpContext.User.FindAll("eventstore:tenant").Select(claim => claim.Value), out IReadOnlySet<string> grants)
            || source != RestTenantSource.Platform && !grants.Contains(canonicalTenant))
            return ScopeProblem(StatusCodes.Status403Forbidden, "The tenant is not authorized.");

        return null;
    }

    private static ObjectResult ScopeProblem(int status, string detail)
        => new(new ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status400BadRequest ? "Bad Request" : "Forbidden",
            Detail = detail,
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };

    /// <summary>Validates the same partition tenant already resolved by a generated action before gateway dispatch.</summary>
    public static bool ValidateTenant(Microsoft.AspNetCore.Http.HttpContext context, Type contractType, string kind, string tenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(contractType);
        // Hosting validation must hold a captured generation; manual fixtures install their own callback.
        if (!context.Items.TryGetValue(LeaseKey, out object? value)) return false;
        try
        {
            var lease = (RouteCatalogDispatchLease)value!;
            var metadata = (RouteCatalogEndpointAttribute)context.Items[MetadataKey]!;
            if (metadata.ContractType != contractType || metadata.Kind != kind)
                throw new RouteCatalogUnavailableException("The generated action disagrees with its compiled endpoint metadata.");
            string domain = Read(metadata.ContractType, "Domain");
            string discriminator = Read(metadata.ContractType, metadata.Kind == "command" ? "CommandType" : "QueryType");
            bool platformScope = TenantCanonicalizer.IsPlatformScope(tenant);
            if (!platformScope)
            {
                if (!TenantCanonicalizer.TryCanonicalize(tenant, out string canonicalTenant)
                    || !string.Equals(canonicalTenant, tenant, StringComparison.Ordinal))
                    return false;
            }
            var selected = lease.Snapshot.Resolve(metadata.Kind,
                platformScope || metadata.Kind == "query" && LegacyTenantStorageMap.IsSingleTenantRead(domain, discriminator)
                    ? TenantCanonicalizer.ReservedStorageTenant : tenant, domain, discriminator);
            if (platformScope)
            {
                PlatformAdmissionDeclaration? admission = selected.Declaration.PlatformAdmission;
                if (admission is null || !string.Equals(admission.Namespace, tenant, StringComparison.Ordinal)
                    || context.User.Identity?.IsAuthenticated != true
                    || context.User.FindAll("eventstore:permission").Count(claim =>
                        string.Equals(claim.Value, admission.RequiredHumanPermission, StringComparison.Ordinal)) != 1
                    || !TenantCanonicalizer.TryCanonicalizeGrants(
                        context.User.FindAll("eventstore:tenant").Select(claim => claim.Value), out _))
                    return false;
            }
            else if (selected.Declaration.PlatformAdmission is not null)
                return false;
            if (lease.Snapshot.Envelope.SchemaVersion == 3
                && !Authentication.RouteCatalogSurfacePolicy.Allows(lease.Snapshot.Envelope.Operations!.Single(o => o.RouteId == selected.Declaration.RouteId), context.User))
            {
                context.Items[EligibilityRefusedKey] = true;
                return false;
            }
            RouteContractDeclaration[] compiled = [.. CompiledDeclarations.GetOrAdd(metadata.ContractType.Assembly, static assembly => CompiledRouteCatalogDeclarations.Read([assembly]))
                .Where(d => d.Kind == metadata.Kind && d.Domain == domain && CommandDiscriminatorCompatibility.Matches(d, discriminator))];
            if (compiled.Length != 1 || RouteCatalogCodec.ComputeDeclarationDigest(compiled[0]) != selected.Target.DeclarationDigest)
                throw new RouteCatalogUnavailableException("The generated host's full compiled declaration digest differs from committed authority.");
            if (metadata.Kind == "command") _ = lease.Snapshot.Admission(selected.Declaration);
            else if (metadata.Kind != "query" || metadata.ContractType.GetProperty("ProjectionType", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string
                != selected.Declaration.ProjectionType)
                throw new RouteCatalogUnavailableException("Generated contract differs from the committed query declaration.");
            context.Items[ValidatedKey] = true;
            return true;
        }
        catch (Exception exception)
        {
            RouteCatalogRefusalDiagnostics.Report(context.Items[LoggerKey] as Microsoft.Extensions.Logging.ILogger,
                "generated-receiver", exception);
            return false;
        }
    }

    private static bool IsEarlyFailure(IActionResult? result)
        => result is ChallengeResult or ForbidResult
            || result is IStatusCodeActionResult { StatusCode: >= 400 };

    // Compiled owner declarations are fixed per assembly, so reflection and digesting run once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Assembly, IReadOnlyList<RouteContractDeclaration>> CompiledDeclarations = new();
    private static readonly object LeaseKey = new();
    private static readonly object MetadataKey = new();
    private static readonly object LoggerKey = new();
    private static readonly object ValidatedKey = new();
    private static readonly object EligibilityRefusedKey = new();

    private static string Read(Type type, string property) => type.GetProperty(property, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string
        ?? throw new RouteCatalogUnavailableException("Generated contract declaration is unavailable.");
}
