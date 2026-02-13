namespace Hexalith.EventStore.CommandApi.Authentication;

/// <summary>
/// Configuration options for JWT authentication.
/// Bound from the "Authentication:JwtBearer" configuration section.
/// </summary>
public record EventStoreAuthenticationOptions
{
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
