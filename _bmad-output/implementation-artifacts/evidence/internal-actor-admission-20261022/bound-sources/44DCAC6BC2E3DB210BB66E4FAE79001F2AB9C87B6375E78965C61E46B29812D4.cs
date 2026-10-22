namespace Hexalith.EventStore.Server.Identity;

/// <summary>Gateway private signing key; no default, never installed in domain verifiers.</summary>
public sealed class IdentityAdmissionSigningOptions
{
    /// <summary>Gets or sets the private RSA signing key from protected secret configuration.</summary>
    public string? PrivateKeyPem { get; set; }
}
