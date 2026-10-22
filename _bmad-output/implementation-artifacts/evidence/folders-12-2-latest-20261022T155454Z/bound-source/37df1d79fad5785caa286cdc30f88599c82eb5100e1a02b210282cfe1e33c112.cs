namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>
/// Defines the shared fail-closed JWT bearer configuration used by public EventStore hosts.
/// </summary>
public record JwtBearerAuthenticationOptions
{
    /// <summary>
    /// Gets the optional OIDC discovery authority.
    /// </summary>
    public string? Authority { get; init; }

    /// <summary>
    /// Gets the primary accepted audience.
    /// </summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// Gets additional accepted audiences, in configuration order.
    /// </summary>
    public string[] ValidAudiences { get; init; } = [];

    /// <summary>
    /// Gets the explicit signing algorithms accepted in authority mode.
    /// </summary>
    public string[] AllowedAlgorithms { get; init; } = [];

    /// <summary>
    /// Gets the expected token issuer.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional symmetric signing key.
    /// </summary>
    public string? SigningKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether OIDC metadata must be retrieved over HTTPS.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether a non-Production, non-Development host may use symmetric validation.
    /// </summary>
    public bool AllowInsecureSymmetricKey { get; init; }
}
