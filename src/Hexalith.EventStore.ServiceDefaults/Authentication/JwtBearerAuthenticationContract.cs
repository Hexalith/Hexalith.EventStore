using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Applies the shared fail-closed JWT bearer validation contract.
/// </summary>
public static partial class JwtBearerAuthenticationContract
{
    private static readonly HashSet<string> SupportedAsymmetricAlgorithms = new(StringComparer.Ordinal)
    {
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,
    };

    /// <summary>
    /// Validates an authentication configuration without disclosing configured values.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <param name="environment">The current host environment.</param>
    /// <param name="sectionName">The configuration section name used in support-safe failures.</param>
    /// <param name="logger">An optional logger used for the approved non-Production symmetric-mode warning.</param>
    /// <returns>The validation result.</returns>
    public static ValidateOptionsResult Validate(
        JwtBearerAuthenticationOptions options,
        IHostEnvironment environment,
        string sectionName,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        bool hasAuthority = !string.IsNullOrWhiteSpace(options.Authority);
        bool hasSigningKey = !string.IsNullOrWhiteSpace(options.SigningKey);

        if (hasAuthority == hasSigningKey)
        {
            return ValidateOptionsResult.Fail(
                $"{sectionName} requires exactly one of Authority or SigningKey to be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return ValidateOptionsResult.Fail($"{sectionName}:Issuer must be configured.");
        }

        string[] audiences;
        try
        {
            audiences = ResolveAudiences(options);
        }
        catch (ArgumentException)
        {
            return ValidateOptionsResult.Fail(
                $"{sectionName} requires at least one non-blank Audience or ValidAudiences entry.");
        }

        if (audiences.Length == 0)
        {
            return ValidateOptionsResult.Fail(
                $"{sectionName} requires at least one non-blank Audience or ValidAudiences entry.");
        }

        if (hasAuthority)
        {
            if (!TryValidateAuthority(options.Authority!, environment.IsDevelopment(), out string failure))
            {
                return ValidateOptionsResult.Fail($"{sectionName}:Authority {failure}");
            }

            if (!environment.IsDevelopment() && !options.RequireHttpsMetadata)
            {
                return ValidateOptionsResult.Fail(
                    $"{sectionName}:RequireHttpsMetadata must be true outside Development.");
            }

            try
            {
                _ = ResolveAuthorityAlgorithms(options);
            }
            catch (ArgumentException)
            {
                return ValidateOptionsResult.Fail(
                    $"{sectionName}:AllowedAlgorithms must contain at least one non-blank supported asymmetric algorithm and must not contain symmetric or unknown algorithms.");
            }

            return ValidateOptionsResult.Success;
        }

        if (options.AllowedAlgorithms is null
            || options.AllowedAlgorithms.Any(static algorithm =>
            !string.Equals(algorithm?.Trim(), SecurityAlgorithms.HmacSha256, StringComparison.Ordinal)))
        {
            return ValidateOptionsResult.Fail(
                $"{sectionName}:AllowedAlgorithms may contain only HS256 in symmetric mode.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey!) < 32)
        {
            return ValidateOptionsResult.Fail(
                $"{sectionName}:SigningKey must be at least 32 bytes (256 bits) for HS256.");
        }

        if (environment.IsProduction())
        {
            return ValidateOptionsResult.Fail(
                $"{sectionName}:SigningKey is forbidden in Production, including when AllowInsecureSymmetricKey is true.");
        }

        if (!environment.IsDevelopment() && !options.AllowInsecureSymmetricKey)
        {
            return ValidateOptionsResult.Fail(
                $"{sectionName}:SigningKey is Development-only unless AllowInsecureSymmetricKey is explicitly enabled in a non-Production environment.");
        }

        if (!environment.IsDevelopment())
        {
            LogNonProductionSymmetricException(logger, environment.EnvironmentName, sectionName);
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Applies validation parameters and mode-specific key discovery to bearer options.
    /// </summary>
    /// <param name="target">The bearer options to configure.</param>
    /// <param name="source">The validated authentication options.</param>
    public static void Configure(JwtBearerOptions target, JwtBearerAuthenticationOptions source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        bool useAuthority = !string.IsNullOrWhiteSpace(source.Authority);
        string[] audiences = ResolveAudiences(source);

        target.MapInboundClaims = false;
        target.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuer = source.Issuer.Trim(),
            ValidAudiences = [.. audiences],
            ValidAlgorithms = useAuthority
                ? [.. ResolveAuthorityAlgorithms(source)]
                : [SecurityAlgorithms.HmacSha256],
        };

        if (useAuthority)
        {
            target.Authority = source.Authority!.Trim();
            target.RequireHttpsMetadata = source.RequireHttpsMetadata;
            return;
        }

        target.Authority = null;
        target.RequireHttpsMetadata = source.RequireHttpsMetadata;
        target.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(source.SigningKey!));
    }

    /// <summary>
    /// Resolves the primary and additional audience settings into a stable, duplicate-free sequence.
    /// </summary>
    /// <param name="options">The authentication options.</param>
    /// <returns>The configured audiences in first-seen order.</returns>
    public static string[] ResolveAudiences(JwtBearerAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ValidAudiences);

        var resolved = new List<string>(options.ValidAudiences.Length + 1);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(options.Audience))
        {
            Add(options.Audience);
        }

        foreach (string? audience in options.ValidAudiences)
        {
            if (string.IsNullOrWhiteSpace(audience))
            {
                throw new ArgumentException("Audience entries must not be blank.", nameof(options));
            }

            Add(audience);
        }

        return [.. resolved];

        void Add(string? audience)
        {
            string trimmed = audience!.Trim();
            if (unique.Add(trimmed))
            {
                resolved.Add(trimmed);
            }
        }
    }

    /// <summary>
    /// Resolves and validates the explicit authority-mode signing-algorithm allow-list.
    /// </summary>
    /// <param name="options">The authentication options.</param>
    /// <returns>A trimmed, duplicate-free defensive copy of the configured algorithms.</returns>
    public static string[] ResolveAuthorityAlgorithms(JwtBearerAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.AllowedAlgorithms);

        if (options.AllowedAlgorithms.Length == 0)
        {
            throw new ArgumentException(
                "At least one authority-mode signing algorithm must be configured.",
                nameof(options));
        }

        var resolved = new List<string>(options.AllowedAlgorithms.Length);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? algorithm in options.AllowedAlgorithms)
        {
            string trimmed = algorithm?.Trim() ?? string.Empty;
            if (trimmed.Length == 0 || !SupportedAsymmetricAlgorithms.Contains(trimmed))
            {
                throw new ArgumentException(
                    "Authority-mode algorithms must be supported asymmetric algorithms.",
                    nameof(options));
            }

            if (unique.Add(trimmed))
            {
                resolved.Add(trimmed);
            }
        }

        return [.. resolved];
    }

    private static bool TryValidateAuthority(string authority, bool isDevelopment, out string failure)
    {
        string trimmed = authority.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            failure = "must be an absolute URI.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !(isDevelopment && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            failure = isDevelopment
                ? "must use HTTP or HTTPS in Development."
                : "must use HTTPS outside Development.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            failure = "must not contain user information.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            failure = "must not contain a query string.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            failure = "must not contain a fragment.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    [LoggerMessage(
        EventId = 5301,
        Level = LogLevel.Warning,
        Message = "A non-Production symmetric JWT exception is active. Environment={EnvironmentName}, ConfigurationSection={ConfigurationSection}, Algorithm=HS256. Secret values are intentionally omitted.")]
    private static partial void LogNonProductionSymmetricException(
        ILogger? logger,
        string environmentName,
        string configurationSection);
}
