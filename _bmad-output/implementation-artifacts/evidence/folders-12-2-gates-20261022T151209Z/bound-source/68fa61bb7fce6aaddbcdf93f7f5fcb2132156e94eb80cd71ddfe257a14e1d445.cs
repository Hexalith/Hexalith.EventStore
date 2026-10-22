using System.Security.Cryptography;

namespace Hexalith.EventStore.ProviderVerification;

/// <summary>
/// Holds the single per-run opaque credential shared by normalized Pact requests and the provider host.
/// </summary>
internal sealed record ProviderVerificationCredential(string AccessToken)
{
    /// <summary>Creates a cryptographically random credential for one verification run.</summary>
    /// <returns>The generated credential.</returns>
    public static ProviderVerificationCredential Create()
        => new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    /// <inheritdoc />
    public override string ToString()
        => nameof(ProviderVerificationCredential);
}
