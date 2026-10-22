namespace Hexalith.EventStore.Client.Security;

/// <summary>Explicit protected admission-key configuration with no insecure default.</summary>
public sealed class IdentityAdmissionOptions
{
    /// <summary>Gets or sets the gateway public verification key in PEM format.</summary>
    public string? VerificationKeyPem { get; set; }

    /// <summary>Gets or sets the approved current trust-policy revision.</summary>
    public long AuthorityRevision { get; set; }
}
