// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement section 1.
// Approved Dapr Kubernetes cryptography supplement: 14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881; section 6.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Closed content-bound authority selection; configuration can never supply an arbitrary digest.</summary>
internal sealed class PayloadProtectionAuthorityProfile
{
    /// <summary>Gets the exact approved Story 8.12 supplement normative digest.</summary>
    internal const string DaprKubernetesCryptoSupplementDigest = "14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881";

    /// <summary>Gets SHA-256 of ASCII HXPA, byte 03, the predecessor effective digest and the supplement digest.</summary>
    internal const string DaprKubernetesCryptoDigest = "29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44";

    private PayloadProtectionAuthorityProfile(string digest, int keyRecordSchemaVersion)
    {
        Digest = digest;
        KeyRecordSchemaVersion = keyRecordSchemaVersion;
    }

    /// <summary>Gets the retained existing/Azure lifecycle authority.</summary>
    internal static PayloadProtectionAuthorityProfile Predecessor { get; } = new(PayloadProtectionStateKeys.AuthorityDigest, 1);
    /// <summary>Gets the separately approved on-premises authority; this does not authorize migration of an existing root.</summary>
    internal static PayloadProtectionAuthorityProfile PlatformCustody { get; } = new("835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180", 1);
    /// <summary>Gets the Story 8.12 profile; it does not chain from the on-premises authority and restamps no existing root.</summary>
    internal static PayloadProtectionAuthorityProfile DaprKubernetesCrypto { get; } = new(DaprKubernetesCryptoDigest, 2);
    /// <summary>Gets the exact approved digest for state and admission comparison.</summary>
    internal string Digest { get; }
    /// <summary>Gets the only wrapped-key record schema readable and writable under this authority.</summary>
    internal int KeyRecordSchemaVersion { get; }
    /// <summary>Refuses any root or lease bound to another authority; no migration or rewriting occurs.</summary>
    internal void Require(string digest)
    {
        if (digest != Digest) { throw new PayloadProtectionFormatException(); }
    }

    /// <summary>Never formats authority selection as a readiness claim.</summary>
    public override string ToString() => nameof(PayloadProtectionAuthorityProfile);
}
