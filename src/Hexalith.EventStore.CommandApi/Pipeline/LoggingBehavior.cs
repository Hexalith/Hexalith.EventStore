namespace Hexalith.EventStore.CommandApi.Pipeline;

using System.Diagnostics;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Telemetry;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Telemetry;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Outermost MediatR pipeline behavior that logs structured entry/exit with correlation ID,
/// command metadata, and duration. Also creates OpenTelemetry activities for tracing.
/// </summary>
public partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    IHttpContextAccessor httpContextAccessor)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull {
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        string correlationId = GetCorrelationId();
        string commandType = typeof(TRequest).Name;
        string? tenant = null;
        string? domain = null;
        string? aggregateId = null;
        string causationId = correlationId; // For original submissions, CausationId = CorrelationId
        string? sourceIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        if (request is SubmitCommand submitCommand) {
            tenant = submitCommand.Tenant;
            domain = submitCommand.Domain;
            aggregateId = submitCommand.AggregateId;
            commandType = submitCommand.CommandType;
        }

        using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
            EventStoreActivitySources.Submit,
            ActivityKind.Server);
        if (activity is not null) {
            activity.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
            activity.SetTag(EventStoreActivitySource.TagTenantId, tenant);
            activity.SetTag(EventStoreActivitySource.TagDomain, domain);
            activity.SetTag(EventStoreActivitySource.TagCommandType, commandType);
        }

        Log.PipelineEntry(logger, correlationId, causationId, commandType, tenant, domain, aggregateId, sourceIp);

        long startTimestamp = Stopwatch.GetTimestamp();

        try {
            TResponse response = await next().ConfigureAwait(false);

            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            activity?.SetStatus(ActivityStatusCode.Ok);

            Log.PipelineExit(logger, correlationId, causationId, commandType, tenant, domain, aggregateId, elapsed.TotalMilliseconds);

            return response;
        }
        catch (Exception ex) {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            Log.PipelineError(logger, ex, correlationId, causationId, commandType, tenant, domain, aggregateId, ex.GetType().Name, ex.Message, elapsed.TotalMilliseconds);

            throw;
        }
    }

    private string GetCorrelationId() {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items[CorrelationIdMiddleware.HttpContextKey] is string correlationId) {
            return correlationId;
        }

        return Guid.NewGuid().ToString();
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1000,
            Level = LogLevel.Information,
            Message = "MediatR pipeline entry: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, SourceIp={SourceIp}, Stage=PipelineEntry")]
        public static partial void PipelineEntry(
            ILogger logger,
            string correlationId,
            string causationId,
            string commandType,
            string? tenant,
            string? domain,
            string? aggregateId,
            string? sourceIp);

        [LoggerMessage(
            EventId = 1001,
            Level = LogLevel.Information,
            Message = "MediatR pipeline exit: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, DurationMs={DurationMs}, Stage=PipelineExit")]
        public static partial void PipelineExit(
            ILogger logger,
            string correlationId,
            string causationId,
            string commandType,
            string? tenant,
            string? domain,
            string? aggregateId,
            double durationMs);

        [LoggerMessage(
            EventId = 1002,
            Level = LogLevel.Error,
            Message = "MediatR pipeline error: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Message={ExceptionMessage}, DurationMs={DurationMs}, Stage=PipelineError")]
        public static partial void PipelineError(
            ILogger logger,
            Exception ex,
            string correlationId,
            string causationId,
            string commandType,
            string? tenant,
            string? domain,
            string? aggregateId,
            string exceptionType,
            string exceptionMessage,
            double durationMs);
    }
}
