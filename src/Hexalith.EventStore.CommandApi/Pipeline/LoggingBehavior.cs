namespace Hexalith.EventStore.CommandApi.Pipeline;

using System.Diagnostics;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Telemetry;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Outermost MediatR pipeline behavior that logs structured entry/exit with correlation ID,
/// command metadata, and duration. Also creates OpenTelemetry activities for tracing.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    IHttpContextAccessor httpContextAccessor)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        string correlationId = GetCorrelationId();
        string commandType = typeof(TRequest).Name;
        string? tenant = null;
        string? domain = null;
        string? aggregateId = null;

        if (request is SubmitCommand submitCommand)
        {
            tenant = submitCommand.Tenant;
            domain = submitCommand.Domain;
            aggregateId = submitCommand.AggregateId;
            commandType = submitCommand.CommandType;
        }

        using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity("EventStore.CommandApi.Submit");
        if (activity is not null)
        {
            activity.SetTag("eventstore.correlation_id", correlationId);
            activity.SetTag("eventstore.tenant", tenant);
            activity.SetTag("eventstore.domain", domain);
            activity.SetTag("eventstore.command_type", commandType);
        }

        logger.LogInformation(
            "MediatR pipeline entry: CorrelationId={CorrelationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}",
            correlationId,
            commandType,
            tenant,
            domain,
            aggregateId);

        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            TResponse response = await next().ConfigureAwait(false);

            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            logger.LogInformation(
                "MediatR pipeline exit: CorrelationId={CorrelationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, DurationMs={DurationMs}",
                correlationId,
                commandType,
                tenant,
                domain,
                aggregateId,
                elapsed.TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            logger.LogError(
                "MediatR pipeline error: CorrelationId={CorrelationId}, CommandType={CommandType}, ExceptionType={ExceptionType}, Message={ExceptionMessage}, DurationMs={DurationMs}",
                correlationId,
                commandType,
                ex.GetType().Name,
                ex.Message,
                elapsed.TotalMilliseconds);

            throw;
        }
    }

    private string GetCorrelationId()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items[CorrelationIdMiddleware.HttpContextKey] is string correlationId)
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString();
    }
}
