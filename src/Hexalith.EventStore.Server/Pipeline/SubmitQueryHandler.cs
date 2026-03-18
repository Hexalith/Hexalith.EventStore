
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Pipeline;
/// <summary>
/// Handles query submission: routes queries to projection actors via the query router.
/// Unlike commands, queries are synchronous — no status tracking or archiving.
/// </summary>
public partial class SubmitQueryHandler(
    IQueryRouter queryRouter,
    ILogger<SubmitQueryHandler> logger) : IRequestHandler<SubmitQuery, SubmitQueryResult>
{
    public async Task<SubmitQueryResult> Handle(SubmitQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Log.QueryReceived(logger, request.CorrelationId, request.QueryType, request.Tenant, request.Domain, request.AggregateId);

        QueryRouterResult routerResult = await queryRouter
            .RouteQueryAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (routerResult.NotFound)
        {
            throw new QueryNotFoundException(request.Tenant, request.Domain, request.AggregateId, request.QueryType);
        }

        if (!routerResult.Success)
        {
            Log.QueryFailed(logger, request.CorrelationId, request.QueryType, request.Tenant, request.Domain, request.AggregateId, routerResult.ErrorMessage);
            throw CreateQueryFailureException(request, routerResult.ErrorMessage);
        }

        if (routerResult.Payload is null)
        {
            Log.QueryCompletedWithoutPayload(logger, request.CorrelationId, request.QueryType, request.Tenant, request.Domain, request.AggregateId);
            throw new InvalidOperationException("Projection query completed without a payload.");
        }

        return new SubmitQueryResult(request.CorrelationId, routerResult.Payload.Value, routerResult.ProjectionType);
    }

    private static Exception CreateQueryFailureException(SubmitQuery request, string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsForbidden(errorMessage))
        {
            return new QueryExecutionFailedException(
                request.CorrelationId,
                request.Tenant,
                request.Domain,
                request.AggregateId,
                request.QueryType,
                403,
                errorMessage!);
        }

        if (IsNotFound(errorMessage))
        {
            return new QueryNotFoundException(request.Tenant, request.Domain, request.AggregateId, request.QueryType);
        }

        if (IsNotImplemented(errorMessage))
        {
            return new QueryExecutionFailedException(
                request.CorrelationId,
                request.Tenant,
                request.Domain,
                request.AggregateId,
                request.QueryType,
                501,
                errorMessage!);
        }

        return new InvalidOperationException("Projection query execution failed.");
    }

    private static bool IsForbidden(string? errorMessage)
        => string.Equals(errorMessage, "Forbidden", StringComparison.OrdinalIgnoreCase);

    private static bool IsNotFound(string? errorMessage)
        => !string.IsNullOrWhiteSpace(errorMessage)
            && errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase);

    private static bool IsNotImplemented(string? errorMessage)
        => !string.IsNullOrWhiteSpace(errorMessage)
            && (errorMessage.Contains("not implemented", StringComparison.OrdinalIgnoreCase)
                || errorMessage.Contains("not yet implemented", StringComparison.OrdinalIgnoreCase));

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1210,
            Level = LogLevel.Information,
            Message = "Query received: CorrelationId={CorrelationId}, QueryType={QueryType}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=QueryReceived")]
        public static partial void QueryReceived(
            ILogger logger,
            string correlationId,
            string queryType,
            string tenantId,
            string domain,
            string aggregateId);

        [LoggerMessage(
            EventId = 1211,
            Level = LogLevel.Warning,
            Message = "Query routing returned failure: CorrelationId={CorrelationId}, QueryType={QueryType}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ErrorMessage={ErrorMessage}, Stage=QueryFailed")]
        public static partial void QueryFailed(
            ILogger logger,
            string correlationId,
            string queryType,
            string tenantId,
            string domain,
            string aggregateId,
            string? errorMessage);

        [LoggerMessage(
            EventId = 1212,
            Level = LogLevel.Error,
            Message = "Query routing returned success without payload: CorrelationId={CorrelationId}, QueryType={QueryType}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=QueryCompletedWithoutPayload")]
        public static partial void QueryCompletedWithoutPayload(
            ILogger logger,
            string correlationId,
            string queryType,
            string tenantId,
            string domain,
            string aggregateId);
    }
}
