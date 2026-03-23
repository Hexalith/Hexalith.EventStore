using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Host.Authentication;

/// <summary>
/// Configuration options for Admin.Server host JWT authentication.
/// </summary>
public record AdminServerAuthenticationOptions {
    public string? Authority { get; init; }

    public string Audience { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string? SigningKey { get; init; }

    public bool RequireHttpsMetadata { get; init; } = true;
}

/// <summary>
/// Validates Admin.Server host authentication configuration at startup.
/// </summary>
public class ValidateAdminServerAuthenticationOptions : IValidateOptions<AdminServerAuthenticationOptions> {
    public ValidateOptionsResult Validate(string? name, AdminServerAuthenticationOptions options) {
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