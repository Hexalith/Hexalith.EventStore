namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Opt-in technical authority configuration. An empty configuration never issues a token.</summary>
public sealed class WorkloadAuthorityOptions
{
    /// <summary>Gets the configuration section.</summary>
    public const string SectionName = "Authentication:WorkloadAuthority";
    /// <summary>Gets the maximum lifetime of an enrolled bound token.</summary>
    public const int MaximumLifetimeSeconds = 120;
    /// <summary>Gets or sets the explicit activation flag.</summary>
    public bool Enabled { get; set; }
    /// <summary>Gets or sets the exact HTTPS issuer, including the authority route prefix.</summary>
    public string Issuer { get; set; } = string.Empty;
    /// <summary>Gets or sets the mounted protected PKCS#12 signing certificate path.</summary>
    public string SigningCertificatePath { get; set; } = string.Empty;
    /// <summary>Gets or sets the certificate password supplied through protected configuration.</summary>
    public string? SigningCertificatePassword { get; set; }
    /// <summary>Gets or sets the authority-approved signing key identifier.</summary>
    public string KeyId { get; set; } = string.Empty;
    /// <summary>Gets or sets the token lifetime, bounded to 120 seconds.</summary>
    public int LifetimeSeconds { get; set; } = MaximumLifetimeSeconds;
    /// <summary>Gets or sets the current enrolled grants. Changes apply to the next issuance.</summary>
    public WorkloadAuthorityGrant[] Grants { get; set; } = [];
}
