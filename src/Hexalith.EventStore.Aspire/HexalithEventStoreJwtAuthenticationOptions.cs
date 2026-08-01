namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Configures validation-only JWT bearer settings for an Aspire project resource.
/// </summary>
public sealed record HexalithEventStoreJwtAuthenticationOptions
{
    /// <summary>
    /// Gets the primary audience accepted by the resource.
    /// </summary>
    public string PrimaryAudience { get; init; } = string.Empty;

    /// <summary>
    /// Gets the additional valid audiences accepted by the resource.
    /// </summary>
    public IReadOnlyList<string> ValidAudiences { get; init; } = [];

    /// <summary>
    /// Gets the explicit HTTPS JWT bearer authority used when the AppHost is publishing.
    /// </summary>
    public string? ExternalAuthority { get; init; }

    /// <summary>
    /// Gets the explicit HTTPS token issuer used when the AppHost is publishing.
    /// </summary>
    public string? ExternalIssuer { get; init; }
}
