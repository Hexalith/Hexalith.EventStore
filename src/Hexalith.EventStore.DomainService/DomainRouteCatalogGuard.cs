using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Validates receiver-side generation and targets after existing workload/channel authorization.</summary>
internal sealed class DomainRouteCatalogGuard(IRouteCatalogAuthority authority, IOptions<RouteCatalogHostOptions> options) : IDomainRouteCatalogGuard
{
    /// <inheritdoc/>
    public async Task<RouteCatalogDispatchLease?> AcquireAsync(EndpointFilterInvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RouteCatalogDispatchLease lease = await authority.AcquireAsync(context.HttpContext.RequestAborted).ConfigureAwait(false);
        try
        {
            var snapshot = lease.Snapshot;
            if (snapshot.Envelope.SchemaVersion == 3
                && Hexalith.EventStore.ServiceDefaults.Authentication.RouteCatalogSurfacePolicy.Surface(context.HttpContext.User) != "service")
                throw new RouteCatalogUnavailableException("The domain receiver requires an authenticated service surface in addition to its workload policy.");
            var headers = context.HttpContext.Request.Headers;
            if (!RouteCatalogPeerBinding.Matches(headers, snapshot))
                throw new RouteCatalogUnavailableException("The incoming call does not bind this committed generation.");
            string appId = options.Value.Identity?.AppId ?? throw new RouteCatalogUnavailableException("The receiver has no catalog host identity.");
            string method = context.HttpContext.Request.Path.Value!.TrimStart('/');
            if (context.HttpContext.GetEndpoint() is Microsoft.AspNetCore.Routing.RouteEndpoint endpoint
                && endpoint.Metadata.GetMetadata<DomainRouteCatalogAuxiliaryMetadata>() is { } auxiliary)
            {
                var owner = auxiliary.Observe(endpoint);
                object request = context.Arguments.SingleOrDefault(argument => argument?.GetType() == auxiliary.RequestType)
                    ?? throw new RouteCatalogUnavailableException("The auxiliary request has no typed owner scope.");
                if (context.HttpContext.Request.Method != "POST" || endpoint.RoutePattern.RawText != "/" + method
                    || !snapshot.AuxiliaryOperations.Any(operation => operation.AppId == appId && operation.Method == method
                        && operation.Operation == auxiliary.Route.Operation && operation.Domain == auxiliary.Domain && operation.DomainAuxiliary == owner))
                    throw new RouteCatalogUnavailableException("The auxiliary endpoint code or scope differs from its committed owner binding.");
                var scope = auxiliary.Scope(request);
                ValidateAuxiliaryTenant(snapshot, appId, scope.Domain, scope.Tenant, ownerAuxiliary: true);
                return lease;
            }
            if (!EventStoreDomainServiceRoutes.Operational.Any(route => route.Route == "/" + method))
                throw new RouteCatalogUnavailableException("The receiver method has no canonical operation policy.");
            string operation = EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/" + method).Operation;
            switch (context.Arguments.FirstOrDefault(a => a is DomainServiceRequest or QueryEnvelope or ProjectionDispatchRequest or ProjectionRequest
                or AggregateReconstructionRequest or ProjectionRebuildTransportPartRequest or DomainSharedProjectionRebuildRequest
                or ProjectionOwnerProofRequest or ProjectionAppliedReceiptRequest or AdminOperationalIndexMetadata.Request))
            {
                case DomainServiceRequest request:
                    var command = snapshot.Resolve("command", request.Command.TenantId, request.Command.Domain, request.Command.CommandType);
                    if (command.Target.AppId != appId || command.Target.Method != method)
                        throw new RouteCatalogUnavailableException("The command was sent to a different target.");
                    if (DomainServiceVersion.Resolve(request.Command, command.Target.ContractVersion) != command.Target.ContractVersion)
                        throw new RouteCatalogUnavailableException("A command version override disagrees with the committed target.");
                    _ = snapshot.Admission(command.Declaration);
                    RefuseStepUp(snapshot, command.Declaration.RouteId);
                    break;
                case QueryEnvelope query:
                    var selected = snapshot.Resolve("query", query.TenantId, query.Domain, query.QueryType);
                    if (selected.Target.AppId != appId || selected.Target.Method != method || selected.Declaration.Execution!.TargetKind != "domain-service")
                        throw new RouteCatalogUnavailableException("The query was sent to a different target.");
                    if (selected.Declaration.Execution.RequiresDelegatedContext && query.CallerAuthority is null)
                        throw new RouteCatalogUnavailableException("This query requires verified delegated caller authority at the receiver.");
                    RefuseStepUp(snapshot, selected.Declaration.RouteId);
                    break;
                case ProjectionDispatchRequest dispatch:
                    ValidateDispatch(snapshot, appId, method, dispatch);
                    break;
                case ProjectionRebuildTransportPartRequest part:
                    ValidateDispatch(snapshot, appId, method, part.Header);
                    break;
                case DomainSharedProjectionRebuildRequest shared:
                    ValidateProjectionScope(snapshot, appId, method, shared.Identity.TenantId, shared.Identity.Domain,
                        shared.Identity.CatalogFingerprint, [shared.Identity.ProjectionType]);
                    break;
                case ProjectionOwnerProofRequest proof:
                    ValidateOwnerScope(snapshot, appId, method, proof);
                    break;
                case ProjectionAppliedReceiptRequest receipt:
                    ValidateOwnerScope(snapshot, appId, method, receipt.Scope);
                    break;
                case AggregateReconstructionRequest reconstruction:
                    RouteCatalogProjectionPolicy.Auxiliary(snapshot, reconstruction.Domain, appId, method, operation);
                    ValidateAuxiliaryTenant(snapshot, appId, reconstruction.Domain, reconstruction.TenantId);
                    break;
                case AdminOperationalIndexMetadata.Request metadata:
                    if (metadata.Domains is not { Count: > 0 })
                        throw new RouteCatalogUnavailableException("Metadata requires an exact inventoried domain set.");
                    foreach (string domain in metadata.Domains)
                        RouteCatalogProjectionPolicy.Auxiliary(snapshot, domain, appId, method, operation);
                    break;
                case ProjectionRequest projection:
                    if (!context.HttpContext.RequestServices.GetServices<IDomainProjectionHandler>().Any(handler => handler.Domain == projection.Domain)
                        && !snapshot.Envelope.Routes.Any(target => target.AppId == appId && target.Method == method
                            && snapshot.Envelope.Declarations.Any(declaration => declaration.RouteId == target.RouteId
                                && declaration.Kind == "projection" && declaration.Domain == projection.Domain)))
                    {
                        RouteCatalogProjectionPolicy.Auxiliary(snapshot, projection.Domain, appId, method, operation);
                        ValidateAuxiliaryTenant(snapshot, appId, projection.Domain, projection.TenantId);
                        _ = RouteCatalogProjectionPolicy.Outputs(snapshot, projection.TenantId, projection.Domain, appId, "project/v2");
                        throw new DomainRouteCatalogMissingLegacyReceiverException();
                    }
                    _ = RouteCatalogProjectionPolicy.Outputs(snapshot, projection.TenantId, projection.Domain, appId, method);
                    break;
                default:
                    throw new RouteCatalogUnavailableException("The receiver request shape has no catalog scope binding.");
            }
            return lease;
        }
        catch
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void RefuseStepUp(CommittedRouteCatalogSnapshot snapshot, string routeId)
    {
        if (snapshot.Envelope.SchemaVersion == 3 && snapshot.Envelope.Operations!.Single(o => o.RouteId == routeId).Eligibility == "step-up")
            throw new RouteCatalogUnavailableException("Step-up operations are unavailable in this catalog protocol.");
    }

    private static void ValidateDispatch(CommittedRouteCatalogSnapshot snapshot, string appId, string method, ProjectionDispatchRequest dispatch)
    {
        ValidateProjectionScope(snapshot, appId, method, dispatch.Request.TenantId, dispatch.Request.Domain,
            dispatch.CatalogFingerprint, dispatch.ProjectionTypes);
    }

    private static void ValidateOwnerScope(CommittedRouteCatalogSnapshot snapshot, string appId, string method, ProjectionOwnerProofRequest scope)
        => ValidateProjectionScope(snapshot, appId, method, scope.TenantId, scope.Domain, scope.CatalogFingerprint, [scope.ProjectionName]);

    private static void ValidateProjectionScope(CommittedRouteCatalogSnapshot snapshot, string appId, string method,
        string tenant, string domain, string fingerprint, IReadOnlyList<string> projections)
    {
        if (!snapshot.ProjectionCapabilities.Any(c => c.AppId == appId && c.Domain == domain
            && c.Fingerprint == fingerprint && projections.All(t => c.ProjectionTypes.Contains(t, StringComparer.Ordinal))))
            throw new RouteCatalogUnavailableException("The named projection capability is not committed for this domain and generation.");
        // Auxiliary projection protocols still select the exact callable handlers and partition scope.
        _ = RouteCatalogProjectionPolicy.Outputs(snapshot, tenant, domain, appId, "project/v2", projections);
        if (method != "project/v2") RouteCatalogProjectionPolicy.Auxiliary(snapshot, domain, appId, method,
            EventStoreDomainServiceRoutes.Operational.Single(route => route.Route == "/" + method).Operation);
    }

    private static void ValidateAuxiliaryTenant(CommittedRouteCatalogSnapshot snapshot, string appId, string domain, string tenant, bool ownerAuxiliary = false)
    {
        var scoped = snapshot.Envelope.Routes.Where(r => r.AppId == appId
            && snapshot.Envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Domain == domain && d.Kind == "command" && d.Execution!.TargetKind == "domain-service")).ToArray();
        if (scoped.Length == 0)
            throw new RouteCatalogUnavailableException("The auxiliary owner has no committed command scope.");
        if (!scoped.Any(r => r.ResolverTenantPattern is null or "*" || r.ResolverTenantPattern == tenant))
        {
            if (ownerAuxiliary) throw new DomainRouteCatalogForbiddenAuxiliaryRequestException();
            throw new RouteCatalogUnavailableException("The auxiliary request tenant is outside the committed domain scope.");
        }
    }
}
