using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.Authentication;

/// <summary>Validates asymmetric workload delegation against configured authority metadata.</summary>
public sealed class JwtTrustedEffectDelegationVerifier(IOptionsMonitor<JwtBearerOptions> bearerOptions)
    : ITrustedEffectDelegationVerifier
{
    /// <summary>Gets the required delegation audience.</summary>
    public const string Audience = "eventstore-gateway";

    /// <summary>Gets the maximum accepted delegation token size in bytes.</summary>
    public const int MaximumTokenSizeInBytes = 16_384;

    /// <summary>Gets the maximum accepted delegation lifetime, measured as <c>exp</c> minus <c>iat</c>.</summary>
    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Gets the symmetric clock skew applied to <c>exp</c> and <c>iat</c>.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public async Task VerifyAsync(
        TrustedEffectSubmission submission,
        TrustedEffectContext context,
        string semanticDigest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(context);
        JwtBearerOptions options = bearerOptions.Get(JwtBearerDefaults.AuthenticationScheme);
        if (options.ConfigurationManager is null)
        {
            throw new InvalidOperationException("Asymmetric delegation authority is not configured.");
        }

        ClaimsPrincipal principal;
        JwtSecurityToken jwt;
        try
        {
            (principal, jwt) = await ValidateTokenAsync(options, context.DelegationToken, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            // The authority may have rotated its signing key since the metadata was cached.
            options.ConfigurationManager.RequestRefresh();
            (principal, jwt) = await ValidateTokenAsync(options, context.DelegationToken, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!(jwt.Header.Alg.StartsWith("RS", StringComparison.Ordinal)
                || jwt.Header.Alg.StartsWith("PS", StringComparison.Ordinal)
                || jwt.Header.Alg.StartsWith("ES", StringComparison.Ordinal))
            || !jwt.Payload.ContainsKey("iat")
            || jwt.Payload.IssuedAt > DateTime.UtcNow.Add(ClockSkew)
            || jwt.ValidTo - jwt.Payload.IssuedAt > MaximumLifetime)
        {
            throw new InvalidOperationException("Workload delegation token is invalid.");
        }

        EffectIdentity identity = submission.Identity;
        if (!Has(principal, "effect_id", EffectIdentityCodec.ComputeEffectId(identity))
            || !Has(principal, "tenant", identity.Tenant)
            || !Has(principal, "source_domain", identity.SourceDomain)
            || !Has(principal, "source_aggregate", identity.SourceAggregate)
            || !Has(principal, "source_envelope_sequence", identity.SourceEnvelopeSequence.ToString(CultureInfo.InvariantCulture))
            || !Has(principal, "effect_kind", identity.EffectKind)
            || !Has(principal, "target_domain", identity.TargetDomain)
            || !Has(principal, "target_aggregate", identity.TargetAggregate)
            || !Has(principal, "effect_ordinal", identity.Ordinal.ToString(CultureInfo.InvariantCulture))
            || !Has(principal, "command_type", submission.CommandType)
            || !Has(principal, "command_digest", semanticDigest)
            || !Has(principal, "workload", context.Workload)
            || !Has(principal, "purpose", context.Purpose)
            || !Has(principal, "causation", context.CausationId))
        {
            throw new InvalidOperationException("Workload delegation bindings do not match the effect.");
        }
    }

    private static async Task<(ClaimsPrincipal Principal, JwtSecurityToken Token)> ValidateTokenAsync(
        JwtBearerOptions options,
        string delegationToken,
        CancellationToken cancellationToken)
    {
        BaseConfiguration configuration = await options.ConfigurationManager!
            .GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        if (configuration.SigningKeys.Count == 0
            || configuration.SigningKeys.Any(static key => key is SymmetricSecurityKey))
        {
            throw new InvalidOperationException("Delegation authority has no safe asymmetric signing keys.");
        }

        TokenValidationParameters parameters = options.TokenValidationParameters.Clone();
        parameters.ValidateIssuer = true;
        parameters.ValidIssuer = configuration.Issuer;
        parameters.ValidateAudience = true;
        parameters.ValidAudience = Audience;
        parameters.ValidAudiences = null;
        parameters.RequireSignedTokens = true;
        parameters.RequireExpirationTime = true;
        parameters.ValidateLifetime = true;
        parameters.ValidateIssuerSigningKey = true;
        parameters.IssuerSigningKeys = configuration.SigningKeys;
        parameters.IssuerSigningKey = null;
        parameters.ClockSkew = ClockSkew;

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false,
            MaximumTokenSizeInBytes = MaximumTokenSizeInBytes,
        };
        ClaimsPrincipal principal = handler.ValidateToken(
            delegationToken,
            parameters,
            out SecurityToken validatedToken);
        return validatedToken is JwtSecurityToken jwt
            ? (principal, jwt)
            : throw new InvalidOperationException("Workload delegation token is invalid.");
    }

    private static bool Has(ClaimsPrincipal principal, string name, string expected)
        => principal.FindAll(name).Count() == 1
            && string.Equals(principal.FindFirst(name)?.Value, expected, StringComparison.Ordinal);
}
