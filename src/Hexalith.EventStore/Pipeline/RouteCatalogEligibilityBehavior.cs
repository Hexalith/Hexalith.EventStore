using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using MediatR;

namespace Hexalith.EventStore.Pipeline;

/// <summary>Enforces owner eligibility after tenant/RBAC authorization, holding the generation lease through all gateway effects.</summary>
public sealed class RouteCatalogEligibilityBehavior<TRequest, TResponse>(IHttpContextAccessor contexts,
    IServiceProvider services) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);
        if (contexts.HttpContext is not { } context || services.GetService<IRouteCatalogAuthority>() is not { } authority
            || request is not SubmitCommand && request is not SubmitQuery) return await next().ConfigureAwait(false);
        var (kind, tenant, domain, discriminator) = request switch
        {
            SubmitCommand command => ("command", command.Tenant, command.Domain, command.CommandType),
            SubmitQuery query => ("query", query.Tenant, query.Domain, query.QueryType),
            _ => throw new InvalidOperationException(),
        };
        var lease = await authority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var release = lease.ConfigureAwait(false);
        if (lease.Snapshot.Envelope.SchemaVersion == 3)
        {
            var selected = lease.Snapshot.Resolve(kind, tenant, domain, discriminator);
            var operation = lease.Snapshot.Envelope.Operations!.Single(o => o.RouteId == selected.Declaration.RouteId);
            bool workload = context.User.HasClaim(c => c.Type == EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)
                && RouteCatalogSurfacePolicy.Surface(context.User) == "service" && operation.Eligibility != "step-up"
                && context.User.Identity?.IsAuthenticated == true;
            bool verifiedOrigin = request is SubmitCommand c && c.Extensions?.ContainsKey(EventStoreGatewayVerifiedOrigin.ActorExtensionKey) == true
                || request is SubmitQuery { IsDelegated: true, OriginalActorId: not null, AuthenticatedWorkloadId: not null };
            if (!(operation.Ingress == "internal" && workload) && !RouteCatalogSurfacePolicy.Allows(operation, context.User, verifiedOrigin))
                throw new CommandAuthorizationException(tenant, domain, discriminator, "The authenticated surface cannot invoke this operation.");
        }
        return await next().ConfigureAwait(false);
    }
}
