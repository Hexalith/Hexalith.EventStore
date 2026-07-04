
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authentication;
/// <summary>
/// Configuration options for JWT authentication.
/// Bound from the "Authentication:JwtBearer" configuration section.
/// </summary>
public record EventStoreAuthenticationOptions {
    /// <summary>
    /// Gets the OIDC authority URL for production (e.g., "https://login.example.com").
    /// When set, uses OIDC discovery to fetch signing keys.
    /// </summary>
    public string? Authority { get; init; }

    /// <summary>
    /// Gets the expected audience claim value.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected issuer claim value.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the symmetric signing key for development/testing.
    /// Used when Authority is not set. Must be at least 256 bits (32 characters) for HS256.
    /// </summary>
    public string? SigningKey { get; init; }

    /// <summary>
    /// Gets whether HTTPS is required for OIDC metadata discovery.
    /// Should be true in production, false for local development.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the symmetric <see cref="SigningKey"/> path is permitted
    /// outside the Development environment. Defaults to <see langword="false"/>: non-Development
    /// hosts must use an OIDC <see cref="Authority"/>. Set to <see langword="true"/> only as an
    /// explicit break-glass opt-in for a trusted non-production symmetric-key deployment.
    /// </summary>
    public bool AllowInsecureSymmetricKey { get; init; }
}

/// <summary>
/// Validates that <see cref="EventStoreAuthenticationOptions"/> is properly configured at startup.
/// Ensures either Authority (production) or SigningKey (development) is provided,
/// and that Issuer and Audience are always set.
/// </summary>
public class ValidateEventStoreAuthenticationOptions(IHostEnvironment environment) : IValidateOptions<EventStoreAuthenticationOptions> {
    private readonly IHostEnvironment _environment = environment;

    public ValidateOptionsResult Validate(string? name, EventStoreAuthenticationOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.Authority) && string.IsNullOrEmpty(options.SigningKey)) {
            return ValidateOptionsResult.Fail(
                "Authentication:JwtBearer requires either 'Authority' (production OIDC) or 'SigningKey' (development symmetric key) to be configured.");
        }

        // Refuse the symmetric dev-key path outside Development unless explicitly opted in: a non-dev
        // host must validate tokens against an OIDC authority, never a shared secret that can mint
        // tokens for any tenant. The break-glass override exists for trusted non-production hosts.
        if (!_environment.IsDevelopment()
            && string.IsNullOrEmpty(options.Authority)
            && !options.AllowInsecureSymmetricKey) {
            return ValidateOptionsResult.Fail(
                "Authentication:JwtBearer:Authority (OIDC) is required outside the Development environment. "
                + "The symmetric 'SigningKey' path is Development-only; set "
                + "'Authentication:JwtBearer:AllowInsecureSymmetricKey' to true to knowingly override for a "
                + "trusted non-production deployment.");
        }

        if (string.IsNullOrEmpty(options.Issuer)) {
            return ValidateOptionsResult.Fail(
                "Authentication:JwtBearer:Issuer must be configured.");
        }

        if (string.IsNullOrEmpty(options.Audience)) {
            return ValidateOptionsResult.Fail(
                "Authentication:JwtBearer:Audience must be configured.");
        }

        if (!string.IsNullOrEmpty(options.SigningKey) && options.SigningKey.Length < 32) {
            return ValidateOptionsResult.Fail(
                "Authentication:JwtBearer:SigningKey must be at least 32 characters (256 bits) for HS256.");
        }

        return ValidateOptionsResult.Success;
    }
}
