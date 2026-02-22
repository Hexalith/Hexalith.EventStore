using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

namespace Hexalith.EventStore.CommandApi.Pipeline;

/// <summary>
/// MediatR pipeline behavior that validates requests using FluentValidation.
/// </summary>
public partial class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger,
    IHttpContextAccessor httpContextAccessor)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull {
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (!validators.Any()) {
            return await next().ConfigureAwait(false);
        }

        string correlationId = GetCorrelationId();
        string commandType = typeof(TRequest).Name;
        string causationId = correlationId;

        string? tenant = null;
        string? domain = null;
        string? aggregateId = null;

        if (request is SubmitCommand submitCommand) {
            commandType = submitCommand.CommandType;
            tenant = submitCommand.Tenant;
            domain = submitCommand.Domain;
            aggregateId = submitCommand.AggregateId;
        }

        var context = new ValidationContext<TRequest>(request);

        ValidationResult[] validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0) {
            // SEC-5: Only log error count, NEVER log validation error details (may contain payload data)
            Log.ValidationFailed(logger, correlationId, causationId, commandType, tenant, domain, aggregateId, failures.Count);
            throw new ValidationException(failures);
        }

        Log.ValidationPassed(logger, correlationId, causationId, commandType, tenant, domain, aggregateId);
        return await next().ConfigureAwait(false);
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
            EventId = 1010,
            Level = LogLevel.Debug,
            Message = "Command validation passed: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, Stage=ValidationPassed")]
        public static partial void ValidationPassed(
            ILogger logger,
            string correlationId,
            string causationId,
            string commandType,
            string? tenant,
            string? domain,
            string? aggregateId);

        [LoggerMessage(
            EventId = 1011,
            Level = LogLevel.Warning,
            Message = "Command validation failed: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, Tenant={Tenant}, Domain={Domain}, AggregateId={AggregateId}, ValidationErrorCount={ValidationErrorCount}, Stage=ValidationFailed")]
        public static partial void ValidationFailed(
            ILogger logger,
            string correlationId,
            string causationId,
            string commandType,
            string? tenant,
            string? domain,
            string? aggregateId,
            int validationErrorCount);
    }
}
