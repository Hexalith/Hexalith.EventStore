using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Authenticates the Dapr application channel with the startup <c>APP_API_TOKEN</c> (AD-28).
/// </summary>
/// <remarks>
/// The principal records only that the request crossed this application's sidecar. It names no workload, tenant,
/// or administrator, so it can never satisfy a workload-operation policy.
/// </remarks>
public sealed class DaprSidecarChannelAuthenticationHandler(
    IOptionsMonitor<DaprSidecarChannelAuthenticationOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IConfiguration configuration,
    IHostEnvironment environment)
    : AuthenticationHandler<DaprSidecarChannelAuthenticationOptions>(options, loggerFactory, encoder)
{
    /// <summary>Gets the channel claim value recorded for a verified token.</summary>
    public const string VerifiedChannel = "dapr-app-channel";

    /// <summary>Gets the channel claim value recorded in Development when no token is configured.</summary>
    public const string DevelopmentUnverifiedChannel = "development-unverified";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        DaprAppChannelTokenStatus status = DaprAppChannelToken.Verify(Request, configuration, environment);
        if (!DaprAppChannelToken.IsAdmitted(status))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                new WorkloadAuthenticationException(DaprAppChannelToken.ToReasonCode(status))));
        }

        var identity = new ClaimsIdentity(
            [new Claim(
                EventStoreWorkloadAuthenticationDefaults.ChannelClaimType,
                status == DaprAppChannelTokenStatus.Valid ? VerifiedChannel : DevelopmentUnverifiedChannel)],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    /// <inheritdoc />
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        AuthenticateResult result = await HandleAuthenticateOnceSafeAsync().ConfigureAwait(false);
        string reason = result.Failure is null
            ? WorkloadAuthenticationReasons.ChannelTokenMissing
            : WorkloadAssertionEvaluator.ClassifyFailure(result.Failure);
        const int statusCode = StatusCodes.Status401Unauthorized;
        Response.StatusCode = statusCode;
        WorkloadAuthenticationTelemetry.RecordDenial(Logger, Context, Scheme.Name, reason, statusCode);
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        WorkloadAuthenticationTelemetry.RecordDenial(
            Logger,
            Context,
            Scheme.Name,
            WorkloadAuthenticationReasons.OperationNotGranted,
            StatusCodes.Status403Forbidden);
        return Task.CompletedTask;
    }
}
