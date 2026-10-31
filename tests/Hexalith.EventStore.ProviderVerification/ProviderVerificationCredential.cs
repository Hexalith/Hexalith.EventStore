using System.Security.Cryptography;

namespace Hexalith.EventStore.ProviderVerification;

/// <summary>
/// Holds the single per-run opaque credential shared by normalized Pact requests and the provider host.
/// </summary>
internal sealed record ProviderVerificationCredential(string AccessToken)
{
    /// <summary>Gets whether the fixture identity carries its authenticated UI surface.</summary>
    public bool IncludeSurface { get; init; } = true;

    /// <summary>Creates a cryptographically random credential for one verification run.</summary>
    /// <param name="includeSurface">Whether the fixture caller carries its authenticated UI surface.</param>
    /// <returns>The generated credential.</returns>
    public static ProviderVerificationCredential Create(bool includeSurface = true)
        => new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))) { IncludeSurface = includeSurface };

    /// <inheritdoc />
    public override string ToString()
        => nameof(ProviderVerificationCredential);
}
