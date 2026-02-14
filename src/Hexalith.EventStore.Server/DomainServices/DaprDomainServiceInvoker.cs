namespace Hexalith.EventStore.Server.DomainServices;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.Logging;

/// <summary>
/// DAPR-based domain service invoker. Uses DaprClient.InvokeMethodAsync for service invocation (D7).
/// Created per-actor-call (same pattern as IdempotencyChecker, TenantValidator, EventStreamReader).
/// No custom retry logic — DAPR resiliency handles transient failures (enforcement rule #4).
/// </summary>
public class DaprDomainServiceInvoker(
    DaprClient daprClient,
    IDomainServiceResolver resolver,
    ILogger<DaprDomainServiceInvoker> logger) : IDomainServiceInvoker
{
    /// <inheritdoc/>
    public async Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Resolve domain service registration
        DomainServiceRegistration? registration = await resolver
            .ResolveAsync(command.TenantId, command.Domain, cancellationToken)
            .ConfigureAwait(false);

        if (registration is null)
        {
            throw new DomainServiceNotFoundException(command.TenantId, command.Domain);
        }

        logger.LogDebug(
            "Invoking domain service: AppId={AppId}, Method={MethodName}, Tenant={TenantId}, Domain={Domain}, CorrelationId={CorrelationId}",
            registration.AppId,
            registration.MethodName,
            command.TenantId,
            command.Domain,
            command.CorrelationId);

        // Invoke via DAPR service invocation (D7)
        // DAPR resiliency policies handle retries, circuit breaker, timeout (rule #4)
        var request = new DomainServiceRequest(command, currentState);

        DomainResult result = await daprClient
            .InvokeMethodAsync<DomainServiceRequest, DomainResult>(
                registration.AppId,
                registration.MethodName,
                request,
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Domain service completed: AppId={AppId}, ResultType={ResultType}, EventCount={EventCount}, Tenant={TenantId}, CorrelationId={CorrelationId}",
            registration.AppId,
            result.IsSuccess ? "Success" : result.IsRejection ? "Rejection" : "NoOp",
            result.Events.Count,
            command.TenantId,
            command.CorrelationId);

        return result;
    }
}
