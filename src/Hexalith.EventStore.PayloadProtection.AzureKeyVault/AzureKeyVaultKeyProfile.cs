// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using Azure.Security.KeyVault.Keys;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>A validated exact provider version. Metadata does not prove origin, RBAC, network posture or all-copy erasure.</summary>
internal sealed class AzureKeyVaultKeyProfile(string id, DateTimeOffset? notBefore = null, DateTimeOffset? expires = null)
{
    /// <summary>Gets the exact opaque version ID.</summary>
    internal string Id { get; } = id;

    /// <summary>Checks cached selection time validity independently of monotonic freshness.</summary>
    internal bool IsTimeValid(DateTimeOffset now) => (notBefore is null || notBefore <= now) && (expires is null || expires > now);
    /// <summary>Validates a recorded version independently of the current alias.</summary>
    internal static string Version(AzureKeyVaultPayloadProtectionOptions options, string id)
    {
        if (!Uri.TryCreate(id, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" || uri.Authority != options.VaultUri.Authority || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        string prefix = "/keys/" + options.KeyName + "/";
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        string version = uri.AbsolutePath[prefix.Length..];
        if (version.Length != 32 || version.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')) || id != options.VaultUri.GetLeftPart(UriPartial.Authority) + prefix + version)
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        return version;
    }

    /// <summary>Requires affirmative HSM, size, non-exportability and exact-operation metadata; no modulus-only profile inference.</summary>
    internal static AzureKeyVaultKeyProfile Validate(AzureKeyVaultPayloadProtectionOptions options, KeyVaultKey key, DateTimeOffset now, string? expectedId)
    {
        string id = key.Id?.AbsoluteUri ?? "";
        _ = Version(options, id);
        if ((expectedId is not null && id != expectedId) || key.Properties.Id?.AbsoluteUri != id || key.Properties.Name != options.KeyName || key.Properties.Version != Version(options, id) || key.KeyType != KeyType.RsaHsm || key.Properties.KeySize != 3072 || key.Properties.Exportable != false || string.IsNullOrWhiteSpace(key.Properties.HsmPlatform) || key.Properties.Enabled != true || key.Properties.NotBefore > now || key.Properties.ExpiresOn <= now || key.KeyOperations.Count != 2 || !key.KeyOperations.Contains(KeyOperation.WrapKey) || !key.KeyOperations.Contains(KeyOperation.UnwrapKey) || key.Key.N is not { Length: 384 } || (key.Key.N[0] & 0x80) == 0 || key.Key.D is not null || key.Key.P is not null || key.Key.Q is not null || key.Key.DP is not null || key.Key.DQ is not null || key.Key.QI is not null || key.Key.K is not null || key.Key.T is not null || key.Key.X is not null || key.Key.Y is not null || key.Key.CurveName is not null)
        {
            throw AzureKeyVaultOptionsValidator.Invalid();
        }

        return new(id, key.Properties.NotBefore, key.Properties.ExpiresOn);
    }

    /// <summary>Returns a safe type label.</summary>
    public override string ToString() => nameof(AzureKeyVaultKeyProfile);
}
