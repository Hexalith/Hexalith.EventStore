// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3, 5-6: fixed-cloud metadata challenge verification and safe diagnostics.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Checks the metadata authentication challenge before SDK parsing can construct an exception containing a foreign resource.</summary>
internal static class AzureKeyVaultMetadataChallenge
{
    /// <summary>Accepts only the deployment cloud's resource and Entra authority with an exact tenant identifier.</summary>
    internal static string Validate(string challenge, AzureKeyVaultPayloadProtectionOptions options)
    {
        if (!challenge.StartsWith("Bearer ", StringComparison.Ordinal) || challenge.Length > 2048)
        {
            throw new AzureKeyVaultTransportException(PayloadBackendFailure.ConsistencyMismatch);
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in challenge[7..].Split(','))
        {
            string value = part.Trim();
            int equals = value.IndexOf('=');
            if (equals < 1 || value.Length < equals + 3 || value[equals + 1] != '"' || value[^1] != '"' || !parameters.TryAdd(value[..equals], value[(equals + 2)..^1]))
            {
                throw new AzureKeyVaultTransportException(PayloadBackendFailure.ConsistencyMismatch);
            }
        }

        string scope = AzureKeyVaultOptionsValidator.Validate(options);
        string resource = scope[..^9];
        if (!parameters.TryGetValue("authorization", out string? authority) || !parameters.TryGetValue("resource", out string? suppliedResource) || suppliedResource != resource || parameters.Keys.Any(key => key is not ("authorization" or "resource" or "scope")) || (parameters.TryGetValue("scope", out string? suppliedScope) && suppliedScope != scope) || !Uri.TryCreate(authority, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || !Guid.TryParseExact(uri.AbsolutePath.TrimStart('/'), "D", out Guid tenant) || uri.AbsolutePath != "/" + tenant.ToString("D"))
        {
            throw new AzureKeyVaultTransportException(PayloadBackendFailure.ConsistencyMismatch);
        }

        string[] hosts = options.VaultUri.Host.EndsWith(".vault.azure.net", StringComparison.Ordinal) ? ["login.windows.net", "login.microsoftonline.com"] : options.VaultUri.Host.EndsWith(".vault.usgovcloudapi.net", StringComparison.Ordinal) ? ["login.microsoftonline.us"] : ["login.chinacloudapi.cn", "login.partner.microsoftonline.cn"];
        if (!hosts.Contains(uri.Host, StringComparer.Ordinal))
        {
            throw new AzureKeyVaultTransportException(PayloadBackendFailure.ConsistencyMismatch);
        }

        return $"Bearer authorization=\"{uri.AbsoluteUri}\", resource=\"{resource}\"";
    }
}
