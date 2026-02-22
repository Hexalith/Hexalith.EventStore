
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.CommandApi.Authentication;
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
}

/// <summary>
/// Validates that <see cref="EventStoreAuthenticationOptions"/> is properly configured at startup.
/// Ensures either Authority (production) or SigningKey (development) is provided,
/// and that Issuer and Audience are always set.
/// </summary>
public class ValidateEventStoreAuthenticationOptions : IValidateOptions<EventStoreAuthenticationOptions> {
    public ValidateOptionsResult Validate(string? name, EventStoreAuthenticationOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(options.Authority) && string.IsNullOrEmpty(options.SigningKey)) {
            return ValidateOptionsResult.Fail(
                "Authentication:JwtBearer requires either 'Authority' (production OIDC) or 'SigningKey' (development symmetric key) to be configured.");
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
