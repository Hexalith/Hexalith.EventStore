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

        BaseConfiguration configuration = await options.ConfigurationManager
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
        parameters.ValidAudience = "eventstore-gateway";
        parameters.ValidAudiences = null;
        parameters.RequireSignedTokens = true;
        parameters.RequireExpirationTime = true;
        parameters.ValidateLifetime = true;
        parameters.ValidateIssuerSigningKey = true;
        parameters.IssuerSigningKeys = configuration.SigningKeys;
        parameters.IssuerSigningKey = null;
        parameters.ClockSkew = TimeSpan.Zero;

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false,
            MaximumTokenSizeInBytes = 16_384,
        };
        ClaimsPrincipal principal = handler.ValidateToken(
            context.DelegationToken,
            parameters,
            out SecurityToken validatedToken);
        if (validatedToken is not JwtSecurityToken jwt
            || !(jwt.Header.Alg.StartsWith("RS", StringComparison.Ordinal)
                || jwt.Header.Alg.StartsWith("PS", StringComparison.Ordinal)
                || jwt.Header.Alg.StartsWith("ES", StringComparison.Ordinal))
            || !jwt.Payload.ContainsKey("iat")
            || jwt.Payload.IssuedAt > DateTime.UtcNow)
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

    private static bool Has(ClaimsPrincipal principal, string name, string expected)
        => principal.FindAll(name).Count() == 1
            && string.Equals(principal.FindFirst(name)?.Value, expected, StringComparison.Ordinal);
}
