using System.Globalization;
using System.Security.Claims;

using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Applies receiver policy to a workload assertion whose issuer, audience, signature, and lifetime were already
/// validated by the shared JWT contract, and rebuilds the minimal workload principal.
/// </summary>
public static class WorkloadAssertionEvaluator
{
    private static readonly char[] OperationSeparators = [' ', '\t', '\r', '\n'];

    /// <summary>
    /// Evaluates a validated assertion principal.
    /// </summary>
    /// <param name="validated">The principal produced by token validation.</param>
    /// <param name="schemeName">The authenticating scheme, used as the identity authentication type.</param>
    /// <param name="options">The receiver settings.</param>
    /// <param name="allowedCallersOverride">An optional caller allow-list replacing <see cref="WorkloadAuthenticationOptions.AllowedCallers"/>.</param>
    /// <param name="daprCallerHeader">The sidecar-established caller attribution, when present.</param>
    /// <param name="now">The current time.</param>
    /// <param name="clockSkew">The allowed clock skew.</param>
    /// <returns>The evaluation.</returns>
    public static WorkloadAssertionEvaluation Evaluate(
        ClaimsPrincipal validated,
        string schemeName,
        WorkloadAuthenticationOptions options,
        IReadOnlyCollection<string>? allowedCallersOverride,
        StringValues daprCallerHeader,
        DateTimeOffset now,
        TimeSpan clockSkew)
    {
        ArgumentNullException.ThrowIfNull(validated);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);
        ArgumentNullException.ThrowIfNull(options);

        if (!TryReadSingleNumericDate(validated, "iat", out long issuedAt)
            || !TryReadSingleNumericDate(validated, "exp", out long expiresAt))
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.AssertionStale);
        }

        if (DateTimeOffset.FromUnixTimeSeconds(issuedAt) > now.Add(clockSkew))
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.AssertionNotYetValid);
        }

        if (expiresAt <= issuedAt || expiresAt - issuedAt > options.MaximumLifetimeSeconds)
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.AssertionStale);
        }

        string[] callers = [.. validated.FindAll(EventStoreWorkloadAuthenticationDefaults.CallerClaimType)
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.Ordinal)];
        if (callers.Length == 0 || string.IsNullOrWhiteSpace(callers[0]))
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.CallerMissing);
        }

        if (callers.Length > 1)
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.CallerAmbiguous);
        }

        string caller = callers[0];
        IEnumerable<string> allowedCallers = allowedCallersOverride ?? (IEnumerable<string>)options.AllowedCallers;
        if (!allowedCallers.Any(allowed => string.Equals(allowed, caller, StringComparison.Ordinal)))
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.CallerNotAllowed);
        }

        // The sidecar attributes the calling application; when present it must agree with the signed caller.
        // It never authenticates the caller on its own.
        if (daprCallerHeader.Count > 0
            && (daprCallerHeader.Count != 1 || !string.Equals(daprCallerHeader[0], caller, StringComparison.Ordinal)))
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.CallerConflict);
        }

        string[] operations = [.. validated.FindAll(EventStoreWorkloadAuthenticationDefaults.OperationClaimType)
            .SelectMany(static claim => claim.Value.Split(OperationSeparators, StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)];
        if (operations.Length == 0)
        {
            return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.OperationMissing);
        }

        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (IGrouping<string, Claim> binding in validated.Claims
            .Where(static claim => claim.Type.StartsWith(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal))
            .GroupBy(static claim => claim.Type, StringComparer.Ordinal))
        {
            string[] values = [.. binding.Select(static claim => claim.Value).Distinct(StringComparer.Ordinal)];
            if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0]))
            {
                return WorkloadAssertionEvaluation.Failure(WorkloadAuthenticationReasons.BindingAmbiguous);
            }

            bindings[binding.Key] = values[0];
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "workload:" + caller),
            new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, caller),
            new(EventStoreWorkloadAuthenticationDefaults.LegacyCallerClaimType, caller),
        };
        claims.AddRange(operations.Select(static operation =>
            new Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, operation)));
        claims.AddRange(bindings.Select(static binding => new Claim(binding.Key, binding.Value)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            schemeName,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role));
        return WorkloadAssertionEvaluation.Success(principal, caller, operations, bindings);
    }

    /// <summary>
    /// Maps a validation failure to its bounded reason code.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The reason code.</returns>
    public static string ClassifyFailure(Exception? exception) => exception switch
    {
        WorkloadAuthenticationException workload => workload.ReasonCode,
        SecurityTokenExpiredException => WorkloadAuthenticationReasons.AssertionExpired,
        SecurityTokenNotYetValidException => WorkloadAuthenticationReasons.AssertionNotYetValid,
        SecurityTokenNoExpirationException => WorkloadAuthenticationReasons.AssertionStale,
        SecurityTokenInvalidLifetimeException => WorkloadAuthenticationReasons.AssertionStale,
        SecurityTokenInvalidAudienceException => WorkloadAuthenticationReasons.AudienceInvalid,
        SecurityTokenInvalidIssuerException => WorkloadAuthenticationReasons.IssuerInvalid,
        SecurityTokenInvalidAlgorithmException => WorkloadAuthenticationReasons.AlgorithmInvalid,
        SecurityTokenInvalidSignatureException => WorkloadAuthenticationReasons.SignatureInvalid,
        SecurityTokenMalformedException => WorkloadAuthenticationReasons.AssertionMalformed,
        SecurityTokenException => WorkloadAuthenticationReasons.AssertionInvalid,
        ArgumentException => WorkloadAuthenticationReasons.AssertionMalformed,
        HttpRequestException or IOException or InvalidOperationException or TimeoutException
            => WorkloadAuthenticationReasons.VerifierUnavailable,
        _ => WorkloadAuthenticationReasons.AssertionInvalid,
    };

    private static bool TryReadSingleNumericDate(ClaimsPrincipal principal, string claimType, out long value)
    {
        value = 0;
        string[] values = [.. principal.FindAll(claimType).Select(static claim => claim.Value).Distinct(StringComparer.Ordinal)];
        return values.Length == 1
            && long.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }
}
