// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 13.1; amendment section 3.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Trusted immutable host identity bound to the approved deployment; host wiring belongs to Story 8.7.</summary>
/// <param name="SourceSha">The full source revision.</param>
/// <param name="PackageSetSha256">The full approved package-set digest.</param>
/// <param name="BackendId">The explicitly enrolled backend.</param>
/// <param name="ReadFormats">The exact supported durable read formats.</param>
/// <param name="WriteModes">The exact supported write modes.</param>
internal sealed record PayloadProtectionInstanceProfile(string SourceSha, string PackageSetSha256, string BackendId,
    string[] ReadFormats, string[] WriteModes)
{
    /// <summary>Never formats deployment identities.</summary>
    public override string ToString() => nameof(PayloadProtectionInstanceProfile);
}
