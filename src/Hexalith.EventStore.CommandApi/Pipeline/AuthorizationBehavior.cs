namespace Hexalith.EventStore.CommandApi.Pipeline;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class AuthorizationBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (request is not SubmitCommand command)
        {
            return await next().ConfigureAwait(false);
        }

        HttpContext httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available in AuthorizationBehavior.");
        System.Security.Claims.ClaimsPrincipal user = httpContext.User
            ?? throw new InvalidOperationException("HttpContext.User is not available in AuthorizationBehavior.");

        if (user.Identity?.IsAuthenticated != true)
        {
            throw new CommandAuthorizationException(
                command.Tenant,
                null,
                null,
                "User is not authenticated.");
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? "unknown";

        // Domain authorization: only enforce if user has domain claims
        List<string> domainClaims = user.FindAll("eventstore:domain")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        string? sourceIp = httpContext.Connection.RemoteIpAddress?.ToString();

        if (domainClaims.Count > 0 && !domainClaims.Any(d => string.Equals(d, command.Domain, StringComparison.OrdinalIgnoreCase)))
        {
            LogAuthorizationFailure(correlationId, command.Tenant, command.Domain, command.CommandType, $"Not authorized for domain '{command.Domain}'.", sourceIp);
            throw new CommandAuthorizationException(
                command.Tenant,
                command.Domain,
                command.CommandType,
                $"Not authorized for domain '{command.Domain}'.");
        }

        // Permission authorization: only enforce if user has permission claims
        List<string> permissionClaims = user.FindAll("eventstore:permission")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (permissionClaims.Count > 0)
        {
            bool hasWildcard = permissionClaims.Any(p => string.Equals(p, AuthorizationConstants.WildcardPermission, StringComparison.OrdinalIgnoreCase));
            bool hasSpecific = permissionClaims.Any(p => string.Equals(p, command.CommandType, StringComparison.OrdinalIgnoreCase));
            if (!hasWildcard && !hasSpecific)
            {
                LogAuthorizationFailure(correlationId, command.Tenant, command.Domain, command.CommandType, $"Not authorized for command type '{command.CommandType}'.", sourceIp);
                throw new CommandAuthorizationException(
                    command.Tenant,
                    command.Domain,
                    command.CommandType,
                    $"Not authorized for command type '{command.CommandType}'.");
            }
        }

        logger.LogDebug(
            "Authorization succeeded for {CorrelationId}: tenant={Tenant}, domain={Domain}, commandType={CommandType}",
            correlationId,
            command.Tenant,
            command.Domain,
            command.CommandType);

        return await next().ConfigureAwait(false);
    }

    private void LogAuthorizationFailure(string correlationId, string tenant, string domain, string commandType, string reason, string? sourceIp)
    {
        logger.LogWarning(
            "Authorization failed: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, CommandType={CommandType}, Reason={Reason}, SourceIP={SourceIP}",
            correlationId,
            tenant,
            domain,
            commandType,
            reason,
            sourceIp);
    }
}
