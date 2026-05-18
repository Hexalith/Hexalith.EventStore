
using System.Diagnostics;

using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Diagnostics;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Telemetry;

using MediatR;

namespace Hexalith.EventStore.Pipeline;
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

        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string correlationId = GetCorrelationId();
        string commandType = typeof(TRequest).Name;
        string? tenant = null;
        string? domain = null;
        string? aggregateId = null;
        string causationId = correlationId; // For original submissions, CausationId = CorrelationId
        string? sourceIp = httpContext?.Connection.RemoteIpAddress?.ToString();
        string? endpoint = httpContext?.Request.Path.Value;
        string? userId = httpContext?.User.FindFirst("sub")?.Value;
        DateTimeOffset receivedAtUtc = DateTimeOffset.UtcNow;

        if (request is SubmitCommand submitCommand) {
            tenant = submitCommand.Tenant;
            domain = submitCommand.Domain;
            aggregateId = submitCommand.AggregateId;
            commandType = submitCommand.CommandType;
        }

        using Activity? activity = EventStoreActivitySources.EventStore.StartActivity(
            EventStoreActivitySources.Submit,
            ActivityKind.Server);
        if (activity is not null) {
            _ = activity.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
            _ = activity.SetTag(EventStoreActivitySource.TagTenantId, tenant);
            _ = activity.SetTag(EventStoreActivitySource.TagDomain, domain);
            _ = activity.SetTag(EventStoreActivitySource.TagCommandType, commandType);
            _ = activity.SetTag("eventstore.user_id", userId);
            _ = activity.SetTag("eventstore.source_ip", sourceIp);
            _ = activity.SetTag("url.path", endpoint);
        }

        Log.PipelineEntry(logger, correlationId, causationId, commandType, tenant, domain, aggregateId, sourceIp, endpoint, userId, receivedAtUtc);

        long startTimestamp = Stopwatch.GetTimestamp();

        try {
            TResponse response = await next().ConfigureAwait(false);

            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            _ = (activity?.SetStatus(ActivityStatusCode.Ok));

            Log.PipelineExit(logger, correlationId, causationId, commandType, tenant, domain, aggregateId, elapsed.TotalMilliseconds);

            return response;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            string safeExceptionMessage = ProtectedDataDiagnosticRedactor.RedactException(ex, "pipeline");

            ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "pipeline");

            Log.PipelineError(logger, correlationId, causationId, commandType, tenant, domain, aggregateId, ex.GetType().Name, safeExceptionMessage, elapsed.TotalMilliseconds);

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
            Message = "MediatR pipeline entry: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, SourceIp={SourceIp}, Endpoint={Endpoint}, UserId={UserId}, ReceivedAtUtc={ReceivedAtUtc}, Stage=PipelineEntry")]
        public static partial void PipelineEntry(
            ILogger logger,
            string correlationId,
            string causationId,
            string commandType,
            string? tenant,
            string? domain,
            string? aggregateId,
            string? sourceIp,
            string? endpoint,
            string? userId,
            DateTimeOffset receivedAtUtc);

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
            Message = "MediatR pipeline error: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, SafeDiagnostic={SafeDiagnostic}, DurationMs={DurationMs}, Stage=PipelineError")]
        public static partial void PipelineError(
            ILogger logger,
            string correlationId,
            string causationId,
            string commandType,
            string? tenant,
            string? domain,
            string? aggregateId,
            string exceptionType,
            string safeDiagnostic,
            double durationMs);
    }
}
