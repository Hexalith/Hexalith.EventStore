using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Enforces the application-channel token, a single workload assertion, and receiver policy around the shared
/// JWT validation contract, then replaces the token principal with the minimal workload principal.
/// </summary>
public sealed class WorkloadJwtBearerEvents(
    IOptionsMonitor<WorkloadAuthenticationOptions> workloadOptions,
    IConfiguration configuration,
    IHostEnvironment environment,
    WorkloadSecurityClock clock,
    ILogger<WorkloadJwtBearerEvents> logger) : JwtBearerEvents
{
    /// <inheritdoc />
    public override Task MessageReceived(MessageReceivedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? reason = GetMessageRejection(context);
        if (reason is not null)
        {
            context.Fail(new WorkloadAuthenticationException(reason));
            return Task.CompletedTask;
        }

        context.Token = context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName][0];
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task TokenValidated(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Principal is null)
        {
            context.Fail(new WorkloadAuthenticationException(WorkloadAuthenticationReasons.AssertionInvalid));
            return Task.CompletedTask;
        }

        WorkloadAssertionEvaluation evaluation = WorkloadAssertionEvaluator.Evaluate(
            context.Principal,
            context.Scheme.Name,
            workloadOptions.Get(context.Scheme.Name),
            allowedCallersOverride: null,
            context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName],
            clock.GetUtcNow(),
            context.Options.TokenValidationParameters.ClockSkew);
        if (!evaluation.Succeeded)
        {
            context.Fail(new WorkloadAuthenticationException(evaluation.ReasonCode!));
            return Task.CompletedTask;
        }

        context.Principal = evaluation.Principal;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Fail(new WorkloadAuthenticationException(
            WorkloadAssertionEvaluator.ClassifyFailure(context.Exception),
            context.Exception));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task Challenge(JwtBearerChallengeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HandleResponse();
        string reason = context.AuthenticateFailure is null
            ? WorkloadAuthenticationReasons.AssertionMissing
            : WorkloadAssertionEvaluator.ClassifyFailure(context.AuthenticateFailure);
        // Only unreachable verification infrastructure is retryable; every other denial is a bounded 401.
        int statusCode = reason == WorkloadAuthenticationReasons.VerifierUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status401Unauthorized;
        context.Response.StatusCode = statusCode;
        WorkloadAuthenticationTelemetry.RecordDenial(logger, context.HttpContext, context.Scheme.Name, reason, statusCode);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task Forbidden(ForbiddenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        WorkloadAuthenticationTelemetry.RecordDenial(
            logger,
            context.HttpContext,
            context.Scheme.Name,
            WorkloadAuthenticationReasons.OperationNotGranted,
            StatusCodes.Status403Forbidden);
        return Task.CompletedTask;
    }

    private string? GetMessageRejection(MessageReceivedContext context)
    {
        if (!ConfigureWorkloadJwtBearerOptions.IsConfigured(context.Options)
            || workloadOptions.Get(context.Scheme.Name).GetConfigurationFailure() is not null)
        {
            return WorkloadAuthenticationReasons.VerifierUnconfigured;
        }

        DaprAppChannelTokenStatus channel = DaprAppChannelToken.Verify(context.Request, configuration, environment);
        if (!DaprAppChannelToken.IsAdmitted(channel))
        {
            return DaprAppChannelToken.ToReasonCode(channel);
        }

        StringValues assertion = context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName];
        if (assertion.Count > 1)
        {
            return WorkloadAuthenticationReasons.AssertionDuplicate;
        }

        if (assertion.Count == 0 || string.IsNullOrWhiteSpace(assertion[0]))
        {
            return WorkloadAuthenticationReasons.AssertionMissing;
        }

        if (assertion[0]!.Length > EventStoreWorkloadAuthenticationDefaults.MaximumAssertionLength)
        {
            return WorkloadAuthenticationReasons.AssertionMalformed;
        }

        // A human bearer next to a workload assertion is ambiguous: neither credential is trusted.
        return context.Request.Headers.ContainsKey(HeaderNames.Authorization)
            ? WorkloadAuthenticationReasons.CredentialConflict
            : null;
    }
}
