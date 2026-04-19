
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Queries;
/// <summary>
/// Routes queries to the correct projection actor based on canonical identity derivation.
/// </summary>
public partial class QueryRouter(
    IActorProxyFactory actorProxyFactory,
    ILogger<QueryRouter> logger) : IQueryRouter {
    /// <summary>
    /// The DAPR actor type name for projection actors. Application developers register
    /// their concrete implementation with this name.
    /// </summary>
    public const string ProjectionActorTypeName = "ProjectionActor";

    /// <inheritdoc/>
    public async Task<QueryRouterResult> RouteQueryAsync(
        SubmitQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        string routingQueryType = string.IsNullOrWhiteSpace(query.ProjectionType)
            ? query.QueryType
            : query.ProjectionType;

        string actorId = QueryActorIdHelper.DeriveActorId(routingQueryType, query.Tenant, query.EntityId, query.Payload);
        string actorTypeName = string.IsNullOrWhiteSpace(query.ProjectionActorType)
            ? ProjectionActorTypeName
            : query.ProjectionActorType;
        int tier = query.EntityId is not null && query.EntityId.Length > 0 ? 1
            : query.Payload.Length > 0 ? 2
            : 3;

        Log.QueryRouting(logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId);
        Log.QueryRoutingTierSelected(logger, query.CorrelationId, tier, actorId);

        var envelope = new QueryEnvelope(
            query.Tenant,
            query.Domain,
            query.AggregateId,
            query.QueryType,
            query.Payload,
            query.CorrelationId,
            query.UserId,
            query.EntityId);

        try {
            IProjectionActor proxy = actorProxyFactory.CreateActorProxy<IProjectionActor>(
                new ActorId(actorId),
                actorTypeName);

            QueryResult result = await proxy.QueryAsync(envelope).ConfigureAwait(false);

            if (!result.Success) {
                Log.QueryExecutionFailed(logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId, result.ErrorMessage);
                return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: result.ErrorMessage);
            }

            Log.QueryRouted(logger, query.CorrelationId, actorId);

            return new QueryRouterResult(Success: true, Payload: result.GetPayload(), NotFound: false, ProjectionType: result.ProjectionType);
        }
        catch (ActorMethodInvocationException ex) when (IsProjectionActorNotFound(ex)) {
            Log.ProjectionActorNotFound(logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, actorId);
            return new QueryRouterResult(Success: false, Payload: null, NotFound: true);
        }
        catch (Exception ex) when (IsProjectionActorNotFound(ex)) {
            Log.ProjectionActorNotFound(logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, actorId);
            return new QueryRouterResult(Success: false, Payload: null, NotFound: true);
        }
        catch (ActorMethodInvocationException ex) {
            Log.ActorInvocationFailed(logger, ex, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId);
            throw;
        }
        catch (Exception ex) {
            Log.ActorInvocationFailed(logger, ex, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId);
            throw;
        }
    }

    private static bool IsProjectionActorNotFound(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        return ContainsNotFoundMarker(exception.Message)
            || ContainsNotFoundMarker(exception.InnerException?.Message);

        static bool ContainsNotFoundMarker(string? message)
            => !string.IsNullOrWhiteSpace(message)
                && (message.Contains("actor type not registered", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("did not find address for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("could not find address for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("no address found for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("actor not found", StringComparison.OrdinalIgnoreCase));
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1200,
            Level = LogLevel.Debug,
            Message = "Routing query to projection actor: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, QueryType={QueryType}, ActorId={ActorId}, Stage=QueryRouting")]
        public static partial void QueryRouting(
            ILogger logger,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            string queryType,
            string actorId);

        [LoggerMessage(
            EventId = 1201,
            Level = LogLevel.Debug,
            Message = "Query routed to projection actor: CorrelationId={CorrelationId}, ActorId={ActorId}, Stage=QueryRouted")]
        public static partial void QueryRouted(
            ILogger logger,
            string correlationId,
            string actorId);

        [LoggerMessage(
            EventId = 1204,
            Level = LogLevel.Warning,
            Message = "Projection actor returned an unsuccessful query result: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, QueryType={QueryType}, ActorId={ActorId}, ErrorMessage={ErrorMessage}, Stage=QueryExecutionFailed")]
        public static partial void QueryExecutionFailed(
            ILogger logger,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            string queryType,
            string actorId,
            string? errorMessage);

        [LoggerMessage(
            EventId = 1205,
            Level = LogLevel.Debug,
            Message = "Query routing tier selected: CorrelationId={CorrelationId}, Tier={Tier}, ActorId={ActorId}, Stage=TierSelected")]
        public static partial void QueryRoutingTierSelected(
            ILogger logger,
            string correlationId,
            int tier,
            string actorId);

        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Error,
            Message = "Projection actor invocation failed: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, QueryType={QueryType}, ActorId={ActorId}, Stage=ActorInvocationFailed")]
        public static partial void ActorInvocationFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            string queryType,
            string actorId);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Warning,
            Message = "Projection actor not found: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ActorId={ActorId}, Stage=ProjectionActorNotFound")]
        public static partial void ProjectionActorNotFound(
            ILogger logger,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            string actorId);
    }
}
