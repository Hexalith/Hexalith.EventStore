using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Validates a workload assertion carried outside the request headers, such as signed publisher provenance in a
/// pub/sub message, with exactly the same shared JWT contract and receiver policy as a workload scheme.
/// </summary>
public sealed class WorkloadAssertionValidator(
    IOptionsMonitor<JwtBearerOptions> bearerOptions,
    IOptionsMonitor<WorkloadAuthenticationOptions> workloadOptions,
    WorkloadSecurityClock clock)
{
    /// <summary>Validates locally using installed public signing keys. Refuses metadata-network verifiers inside a journal-owner turn.</summary>
    public Task<WorkloadAssertionEvaluation> ValidateLocallyAsync(string schemeName, string? assertion,
        string requiredOperation, IReadOnlyCollection<string> allowedCallers, string audience, CancellationToken cancellationToken = default)
    {
        if (bearerOptions.Get(schemeName).ConfigurationManager is not null)
            return Task.FromResult(WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.VerifierUnconfigured));
        return ValidateCoreAsync(schemeName, assertion, requiredOperation, allowedCallers, audience, cancellationToken);
    }

    /// <summary>
    /// Validates one assertion and requires one operation.
    /// </summary>
    /// <param name="schemeName">The registered workload scheme whose contract and audience apply.</param>
    /// <param name="assertion">The assertion, or <see langword="null"/> when absent.</param>
    /// <param name="requiredOperation">The operation the assertion must grant.</param>
    /// <param name="allowedCallersOverride">An optional caller allow-list replacing the scheme's allow-list.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The evaluation.</returns>
    public Task<WorkloadAssertionEvaluation> ValidateAsync(
        string schemeName,
        string? assertion,
        string requiredOperation,
        IReadOnlyCollection<string>? allowedCallersOverride,
        CancellationToken cancellationToken = default)
        => ValidateCoreAsync(schemeName, assertion, requiredOperation, allowedCallersOverride, null, cancellationToken);

    /// <summary>Validates nested provenance for an audience derived from an already authenticated requesting workload.
    /// Signature, issuer, algorithm, lifetime and operation policy remain the registered scheme's contract.</summary>
    public Task<WorkloadAssertionEvaluation> ValidateForAudienceAsync(string schemeName, string? assertion,
        string requiredOperation, IReadOnlyCollection<string> allowedCallers, string authenticatedRequestingWorkload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedRequestingWorkload);
        return ValidateCoreAsync(schemeName, assertion, requiredOperation, allowedCallers, authenticatedRequestingWorkload, cancellationToken);
    }

    private async Task<WorkloadAssertionEvaluation> ValidateCoreAsync(string schemeName, string? assertion,
        string requiredOperation, IReadOnlyCollection<string>? allowedCallersOverride, string? audienceOverride, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredOperation);
        if (string.IsNullOrWhiteSpace(assertion))
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.AssertionMissing);
        }

        if (assertion.Length > EventStoreWorkloadAuthenticationDefaults.MaximumAssertionLength)
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.AssertionMalformed);
        }

        JwtBearerOptions options = bearerOptions.Get(schemeName);
        WorkloadAuthenticationOptions workload = workloadOptions.Get(schemeName);
        string? configurationFailure = allowedCallersOverride is null
            ? workload.GetConfigurationFailure()
            : allowedCallersOverride.Count == 0 ? "AllowedCallers" : workload.GetValidationFailure();
        if (!ConfigureWorkloadJwtBearerOptions.IsConfigured(options) || configurationFailure is not null)
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.VerifierUnconfigured);
        }

        TokenValidationParameters parameters = options.TokenValidationParameters.Clone();
        if (audienceOverride is not null)
        {
            parameters.ValidateAudience = true;
            parameters.ValidAudience = audienceOverride;
            parameters.ValidAudiences = [audienceOverride];
            parameters.AudienceValidator = null;
        }
        if (options.ConfigurationManager is not null)
        {
            OpenIdConnectConfiguration configuration;
            try
            {
                configuration = await options.ConfigurationManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.VerifierUnavailable);
            }

            if (!string.IsNullOrWhiteSpace(configuration.Issuer))
            {
                parameters.ValidIssuers = [.. parameters.ValidIssuers ?? [], configuration.Issuer];
            }

            parameters.IssuerSigningKeys = [.. parameters.IssuerSigningKeys ?? [], .. configuration.SigningKeys];
        }

        var handler = new JsonWebTokenHandler
        {
            MapInboundClaims = false,
            MaximumTokenSizeInBytes = EventStoreWorkloadAuthenticationDefaults.MaximumAssertionLength,
        };
        TokenValidationResult result = await handler.ValidateTokenAsync(assertion, parameters).ConfigureAwait(false);
        if (!result.IsValid)
        {
            if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                // The issuer may have rotated its keys since the metadata was cached.
                options.ConfigurationManager?.RequestRefresh();
            }

            return WorkloadAssertionEvaluation.Failure(WorkloadAssertionEvaluator.ClassifyFailure(result.Exception));
        }

        if (audienceOverride is not null && !new JsonWebToken(assertion).Audiences.SequenceEqual([audienceOverride], StringComparer.Ordinal))
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.BindingMismatch);

        WorkloadAssertionEvaluation evaluation = WorkloadAssertionEvaluator.Evaluate(
            new ClaimsPrincipal(result.ClaimsIdentity),
            schemeName,
            workload,
            allowedCallersOverride,
            StringValues.Empty,
            clock.GetUtcNow(),
            parameters.ClockSkew);
        if (!evaluation.Succeeded)
        {
            return evaluation;
        }

        return evaluation.Operations.Contains(requiredOperation, StringComparer.Ordinal)
            ? evaluation
            : WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.OperationNotGranted);
    }
}
