// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Rejects unknown fields, arbitrary origins and weaker key profiles before constructing clients.</summary>
internal static partial class AzureKeyVaultOptionsValidator
{
    private static readonly string[] Fields = ["VaultUri", "KeyName", "ManagedIdentityClientId", "ExpectedKeySize", "WrapAlgorithm", "KeyVersionRefreshInterval", "RequestTimeout"];
    /// <summary>Loads the exact closed provider subsection without accepting secrets or extension maps.</summary>
    internal static AzureKeyVaultPayloadProtectionOptions Load(IConfigurationSection section)
    {
        try
        {
            if (section.GetChildren().Any(child => !Fields.Contains(child.Key, StringComparer.Ordinal) || child.GetChildren().Any()))
            {
                throw Invalid();
            }

            var result = new AzureKeyVaultPayloadProtectionOptions
            {
                VaultUri = new Uri(section["VaultUri"] ?? "", UriKind.Absolute),
                KeyName = section["KeyName"] ?? "",
                ManagedIdentityClientId = section["ManagedIdentityClientId"],
                ExpectedKeySize = section["ExpectedKeySize"] is string size ? int.Parse(size, CultureInfo.InvariantCulture) : 3072,
                WrapAlgorithm = section["WrapAlgorithm"] ?? "RSA-OAEP-256",
                KeyVersionRefreshInterval = section["KeyVersionRefreshInterval"] is string refresh ? TimeSpan.Parse(refresh, CultureInfo.InvariantCulture) : TimeSpan.FromMinutes(5),
                RequestTimeout = section["RequestTimeout"] is string timeout ? TimeSpan.Parse(timeout, CultureInfo.InvariantCulture) : TimeSpan.FromSeconds(10),
            };
            Validate(result);
            return result;
        }
        catch
        {
            throw Invalid();
        }
    }

    /// <summary>Returns the one fixed token scope corresponding to the validated deployment-cloud origin.</summary>
    internal static string Validate(AzureKeyVaultPayloadProtectionOptions options)
    {
        Uri uri = options.VaultUri;
        if (!uri.IsAbsoluteUri || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/" || uri.Host != uri.IdnHost || !KeyNamePattern().IsMatch(options.KeyName) || options.ExpectedKeySize != 3072 || options.WrapAlgorithm != "RSA-OAEP-256" || options.RequestTimeout <= TimeSpan.Zero || options.RequestTimeout > TimeSpan.FromSeconds(30) || options.KeyVersionRefreshInterval <= TimeSpan.Zero || options.KeyVersionRefreshInterval > TimeSpan.FromHours(1) || (options.ManagedIdentityClientId is string id && (!Guid.TryParseExact(id, "D", out Guid parsed) || parsed.ToString("D") != id)))
        {
            throw Invalid();
        }

        foreach ((string suffix, string scope)in new[]
        {
            (".vault.azure.net", "https://vault.azure.net/.default"),
            (".vault.usgovcloudapi.net", "https://vault.usgovcloudapi.net/.default"),
            (".vault.azure.cn", "https://vault.azure.cn/.default")
        }

        )
        {
            if (uri.Host.EndsWith(suffix, StringComparison.Ordinal) && VaultNamePattern().IsMatch(uri.Host[..^suffix.Length]))
            {
                return scope;
            }
        }

        throw Invalid();
    }

    /// <summary>Returns a constant exception without private configuration or inner exceptions.</summary>
    internal static InvalidOperationException Invalid() => new("The Azure payload protection profile is invalid.");
    [GeneratedRegex("\\A[A-Za-z0-9-]{1,127}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyNamePattern();
    [GeneratedRegex("\\A[a-z][a-z0-9-]{1,22}[a-z0-9]\\z", RegexOptions.CultureInvariant)]
    private static partial Regex VaultNamePattern();
}
