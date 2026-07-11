
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Dapr;
using Dapr.Actors.Client;

using Google.Protobuf;

using Grpc.Core;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Queries;
/// <summary>
/// Routes queries to the correct projection actor based on canonical identity derivation.
/// </summary>
public partial class QueryRouter : IQueryRouter {
    /// <summary>
    /// The DAPR actor type name for projection actors. Application developers register
    /// their concrete implementation with this name.
    /// </summary>
    public const string ProjectionActorTypeName = "ProjectionActor";

    private readonly IProjectionActorInvoker _invoker;
    private readonly ILogger<QueryRouter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRouter"/> class.
    /// </summary>
    /// <remarks>
    /// Production constructor used by DI. Wraps the supplied
    /// <see cref="IActorProxyFactory"/> in the default weak-path invoker so the DAPR
    /// <c>ActorProxy.InvokeMethodAsync</c> call carries the request-scope
    /// <see cref="CancellationToken"/> into the invocation operation.
    /// </remarks>
    public QueryRouter(IActorProxyFactory actorProxyFactory, ILogger<QueryRouter> logger)
        : this(new DefaultProjectionActorInvoker(actorProxyFactory), logger) {
    }

    /// <summary>
    /// Internal test constructor that accepts a substituted <see cref="IProjectionActorInvoker"/>.
    /// </summary>
    internal QueryRouter(IProjectionActorInvoker invoker, ILogger<QueryRouter> logger) {
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(logger);
        _invoker = invoker;
        _logger = logger;
    }

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

        Log.QueryRouting(_logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId);
        Log.QueryRoutingTierSelected(_logger, query.CorrelationId, tier, actorId);

        var envelope = new QueryEnvelope(
            query.Tenant,
            query.Domain,
            query.AggregateId,
            query.QueryType,
            query.Payload,
            query.CorrelationId,
            query.UserId,
            query.EntityId,
            query.IsGlobalAdmin,
            query.Paging);

        try {
            QueryResult? result = await _invoker.InvokeAsync(actorId, actorTypeName, envelope, cancellationToken).ConfigureAwait(false);

            if (result is null) {
                Log.QueryExecutionFailed(_logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId, QueryAdapterFailureReason.ActorResponseMismatch);
                return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.ActorResponseMismatch);
            }

            if (!result.Success) {
                Log.QueryExecutionFailed(_logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId, GetSafeLogErrorMessage(result.ErrorMessage));
                return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: result.ErrorMessage, Metadata: result.Metadata);
            }

            if (result.PayloadBytes is not { Length: > 0 }) {
                Log.QueryExecutionFailed(_logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId, QueryAdapterFailureReason.MissingPayload);
                return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.MissingPayload);
            }

            try {
                JsonElement payload = result.GetPayload();
                Log.QueryRouted(_logger, query.CorrelationId, actorId);
                return new QueryRouterResult(
                    Success: true,
                    Payload: payload,
                    NotFound: false,
                    ProjectionType: result.ProjectionType,
                    Metadata: result.Metadata);
            }
            catch (JsonException) {
                Log.QueryExecutionFailed(_logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId, QueryAdapterFailureReason.SerializationFailure);
                return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.SerializationFailure);
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) when (ContainsCancellationException(ex)) {
            ThrowFirstCancellationException(ex);
            throw;
        }
        catch (Dapr.Actors.ActorMethodInvocationException ex) when (IsProjectionActorNotFound(ex)) {
            Log.ProjectionActorNotFound(_logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, actorId);
            return new QueryRouterResult(Success: false, Payload: null, NotFound: true);
        }
        catch (Exception ex) when (IsProjectionActorNotFound(ex)) {
            Log.ProjectionActorNotFound(_logger, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, actorId);
            return new QueryRouterResult(Success: false, Payload: null, NotFound: true);
        }
        catch (Dapr.Actors.ActorMethodInvocationException ex) {
            Log.ActorInvocationFailed(_logger, ex, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId);
            return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.ActorException);
        }
        catch (Exception ex) {
            Log.ActorInvocationFailed(_logger, ex, query.CorrelationId, query.Tenant, query.Domain, query.AggregateId, query.QueryType, actorId);
            return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: QueryAdapterFailureReason.ActorException);
        }
    }

    private static bool IsProjectionActorNotFound(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        bool hasActorMissingDaprErrorCode = false;
        bool hasContradictoryDaprErrorCode = false;
        bool hasLegacyMarker = false;

        foreach (Exception current in EnumerateExceptionTree(exception)) {
            foreach (string errorCode in EnumerateDaprErrorCodes(current)) {
                if (IsDaprActorMissingErrorCode(errorCode)) {
                    hasActorMissingDaprErrorCode = true;
                }
                else if (HasSpecificDaprErrorCode(errorCode)) {
                    hasContradictoryDaprErrorCode = true;
                }
            }

            hasLegacyMarker |= ContainsLegacyActorNotFoundMarker(current.Message);
        }

        if (hasContradictoryDaprErrorCode) {
            return false;
        }

        if (hasActorMissingDaprErrorCode) {
            return true;
        }

        return hasLegacyMarker;

        static bool IsDaprActorMissingErrorCode(string? errorCode)
            => string.Equals(errorCode, "ERR_ACTOR_INSTANCE_MISSING", StringComparison.Ordinal)
                || string.Equals(errorCode, "ERR_ACTOR_RUNTIME_NOT_FOUND", StringComparison.Ordinal)
                || string.Equals(errorCode, "ERR_ACTOR_NO_ADDRESS", StringComparison.Ordinal);

        static bool HasSpecificDaprErrorCode(string? errorCode)
            => !string.IsNullOrWhiteSpace(errorCode)
                && !string.Equals(errorCode, "UNKNOWN", StringComparison.Ordinal);

        // Compatibility fallback for older DAPR/runtime shapes that exposed only English messages.
        static bool ContainsLegacyActorNotFoundMarker(string? message)
            => !string.IsNullOrWhiteSpace(message)
                && (message.Contains("actor type not registered", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("did not find address for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("could not find address for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("no address found for actor", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("actor not found", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateDaprErrorCodes(Exception exception) {
        if (exception is DaprApiException daprException and not Dapr.Actors.ActorMethodInvocationException
            && !string.IsNullOrWhiteSpace(daprException.ErrorCode)) {
            yield return daprException.ErrorCode;
        }

        if (exception is not RpcException rpcException) {
            yield break;
        }

        foreach (Metadata.Entry entry in rpcException.Trailers) {
            if (IsDaprErrorCodeMetadataKey(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value)) {
                yield return entry.Value;
                continue;
            }

            if (!string.Equals(entry.Key, "grpc-status-details-bin", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            Google.Rpc.Status? status = TryParseGrpcStatusDetails(entry.ValueBytes);
            if (status is null) {
                continue;
            }

            foreach (Google.Protobuf.WellKnownTypes.Any detail in status.Details) {
                if (!detail.Is(Google.Rpc.ErrorInfo.Descriptor)) {
                    continue;
                }

                Google.Rpc.ErrorInfo errorInfo = detail.Unpack<Google.Rpc.ErrorInfo>();
                if (!string.IsNullOrWhiteSpace(errorInfo.Reason)) {
                    yield return errorInfo.Reason;
                }

                foreach (KeyValuePair<string, string> metadata in errorInfo.Metadata) {
                    if (IsDaprErrorCodeMetadataKey(metadata.Key)
                        && !string.IsNullOrWhiteSpace(metadata.Value)) {
                        yield return metadata.Value;
                    }
                }
            }
        }

        static bool IsDaprErrorCodeMetadataKey(string key)
            => string.Equals(key, "error-code", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "error_code", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "errorcode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "dapr-error-code", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "dapr_error_code", StringComparison.OrdinalIgnoreCase);

        static Google.Rpc.Status? TryParseGrpcStatusDetails(byte[] valueBytes) {
            try {
                return Google.Rpc.Status.Parser.ParseFrom(valueBytes);
            }
            catch (InvalidProtocolBufferException) {
                return null;
            }
        }
    }

    private static bool ContainsCancellationException(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        foreach (Exception current in EnumerateExceptionTree(exception)) {
            if (current is OperationCanceledException) {
                return true;
            }
        }

        return false;
    }

    private static string? GetSafeLogErrorMessage(string? errorMessage)
        => IsSentinel(errorMessage, QueryAdapterFailureReason.InvalidCursor)
            ? QueryAdapterFailureReason.InvalidCursor
            : errorMessage;

    private static bool IsSentinel(string? errorMessage, string sentinel)
        => !string.IsNullOrEmpty(errorMessage)
            && (string.Equals(errorMessage, sentinel, StringComparison.Ordinal)
                || errorMessage.StartsWith(sentinel + ":", StringComparison.Ordinal));

    private static void ThrowFirstCancellationException(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        foreach (Exception current in EnumerateExceptionTree(exception)) {
            if (current is OperationCanceledException cancellationException) {
                ExceptionDispatchInfo.Capture(cancellationException).Throw();
            }
        }
    }

    private static IEnumerable<Exception> EnumerateExceptionTree(Exception exception) {
        const int MaxExceptionsToInspect = 32;

        var stack = new Stack<Exception>();
        var seen = new HashSet<Exception>();
        stack.Push(exception);

        int inspected = 0;
        while (stack.Count > 0 && inspected < MaxExceptionsToInspect) {
            Exception current = stack.Pop();
            if (!seen.Add(current)) {
                continue;
            }

            inspected++;
            yield return current;

            if (current is AggregateException aggregateException) {
                for (int i = aggregateException.InnerExceptions.Count - 1; i >= 0; i--) {
                    stack.Push(aggregateException.InnerExceptions[i]);
                }
            }

            if (current.InnerException is not null) {
                stack.Push(current.InnerException);
            }
        }
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
