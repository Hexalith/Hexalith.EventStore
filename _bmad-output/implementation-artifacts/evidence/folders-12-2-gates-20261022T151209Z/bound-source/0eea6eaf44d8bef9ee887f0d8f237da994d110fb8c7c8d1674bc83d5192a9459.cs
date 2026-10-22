using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Rebuilds administrator context at the domain-service boundary: wire administrator flags are removed and restored
/// only after every registered <see cref="IDomainServiceAdministratorVerifier"/> confirms current authority.
/// </summary>
public static partial class DomainServiceAdministratorAssertions
{
    /// <summary>Gets the command extension key that carries the untrusted administrator assertion.</summary>
    public const string GlobalAdminExtensionKey = "actor:globalAdmin";

    /// <summary>Gets the telemetry scheme name recorded when the administrator verifier is unavailable.</summary>
    public const string VerifierTelemetryScheme = "DomainServiceAdministratorVerifier";

    /// <summary>
    /// Returns the request with its administrator assertion removed or replaced by verified context.
    /// </summary>
    /// <param name="serviceProvider">The request service provider.</param>
    /// <param name="request">The inbound request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The request carrying only verified administrator context.</returns>
    public static async Task<DomainServiceRequest> RebuildAsync(
        IServiceProvider serviceProvider,
        DomainServiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);
        CommandEnvelope? command = request.Command;
        if (command?.Extensions is null
            || !command.Extensions.Keys.Any(static key => string.Equals(key, GlobalAdminExtensionKey, StringComparison.OrdinalIgnoreCase)))
        {
            return request;
        }

        bool asserted = command.Extensions.Any(static extension =>
            string.Equals(extension.Key, GlobalAdminExtensionKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(extension.Value, "true", StringComparison.Ordinal));
        var rebuilt = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> extension in command.Extensions)
        {
            if (!string.Equals(extension.Key, GlobalAdminExtensionKey, StringComparison.OrdinalIgnoreCase))
            {
                rebuilt[extension.Key] = extension.Value;
            }
        }

        bool verified = asserted && await VerifyAsync(
            serviceProvider,
            new DomainServiceAdministratorClaim(command.TenantId, command.Domain, command.UserId, command.CorrelationId),
            cancellationToken).ConfigureAwait(false);
        if (verified)
        {
            rebuilt[GlobalAdminExtensionKey] = "true";
        }
        else if (asserted)
        {
            LogAssertionRemoved(GetLogger(serviceProvider), command.Domain, command.CommandType, command.CorrelationId);
        }

        return request with { Command = command with { Extensions = rebuilt.Count > 0 ? rebuilt : null } };
    }

    /// <summary>
    /// Returns the query with its administrator assertion cleared unless current authority is verified.
    /// </summary>
    /// <param name="serviceProvider">The request service provider.</param>
    /// <param name="query">The inbound query.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The query carrying only verified administrator context.</returns>
    public static async Task<QueryEnvelope> RebuildAsync(
        IServiceProvider serviceProvider,
        QueryEnvelope query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(query);
        if (!query.IsGlobalAdmin)
        {
            return query;
        }

        bool verified = await VerifyAsync(
            serviceProvider,
            new DomainServiceAdministratorClaim(query.TenantId, query.Domain, query.UserId, query.CorrelationId),
            cancellationToken).ConfigureAwait(false);
        if (verified)
        {
            return query;
        }

        LogAssertionRemoved(GetLogger(serviceProvider), query.Domain, query.QueryType, query.CorrelationId);
        return query with { IsGlobalAdmin = false };
    }

    private static async Task<bool> VerifyAsync(
        IServiceProvider serviceProvider,
        DomainServiceAdministratorClaim claim,
        CancellationToken cancellationToken)
    {
        IDomainServiceAdministratorVerifier[] verifiers = [.. serviceProvider.GetServices<IDomainServiceAdministratorVerifier>()];
        if (verifiers.Length == 0 || string.IsNullOrWhiteSpace(claim.UserId))
        {
            return false;
        }

        foreach (IDomainServiceAdministratorVerifier verifier in verifiers)
        {
            bool confirmed;
            try
            {
                confirmed = await verifier.IsCurrentGlobalAdministratorAsync(claim, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Unknown authority is not "no authority" and not "authority": the request fails closed as unavailable.
                throw new DomainServiceAdministratorVerificationException(
                    WorkloadAuthenticationReasons.AdministratorVerifierUnavailable,
                    exception);
            }

            if (!confirmed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Records the bounded verifier-unavailable denial with its correlation and returns the retryable 503 result.
    /// </summary>
    /// <param name="context">The refused request.</param>
    /// <returns>An empty <c>503 Service Unavailable</c> result.</returns>
    internal static IResult VerifierUnavailable(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        WorkloadAuthenticationTelemetry.RecordDenial(
            GetLogger(context.RequestServices),
            context,
            VerifierTelemetryScheme,
            WorkloadAuthenticationReasons.AdministratorVerifierUnavailable,
            StatusCodes.Status503ServiceUnavailable);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private static ILogger GetLogger(IServiceProvider serviceProvider)
        => serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(DomainServiceAdministratorAssertions))
            ?? NullLogger.Instance;

    [LoggerMessage(
        EventId = 5521,
        Level = LogLevel.Warning,
        Message = "Unverified administrator assertion removed at the domain-service boundary: SecurityEvent=WireAdminAssertionRemoved, Domain={Domain}, MessageType={MessageType}, CorrelationId={CorrelationId}")]
    private static partial void LogAssertionRemoved(ILogger logger, string domain, string messageType, string correlationId);
}
