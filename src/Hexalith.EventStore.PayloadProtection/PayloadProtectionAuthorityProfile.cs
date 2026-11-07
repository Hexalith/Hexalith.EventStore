// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement section 1.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Closed content-bound authority selection; configuration can never supply an arbitrary digest.</summary>
internal sealed class PayloadProtectionAuthorityProfile
{
    private PayloadProtectionAuthorityProfile(string digest) => Digest = digest;

    /// <summary>Gets the retained existing/Azure lifecycle authority.</summary>
    internal static PayloadProtectionAuthorityProfile Predecessor { get; } = new(PayloadProtectionStateKeys.AuthorityDigest);
    /// <summary>Gets the separately approved on-premises authority; this does not authorize migration of an existing root.</summary>
    internal static PayloadProtectionAuthorityProfile PlatformCustody { get; } = new("835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180");
    /// <summary>Gets the exact approved digest for state and admission comparison.</summary>
    internal string Digest { get; }
    /// <summary>Refuses any root or lease bound to another authority; no migration or rewriting occurs.</summary>
    internal void Require(string digest)
    {
        if (digest != Digest) { throw new PayloadProtectionFormatException(); }
    }

    /// <summary>Never formats authority selection as a readiness claim.</summary>
    public override string ToString() => nameof(PayloadProtectionAuthorityProfile);
}
