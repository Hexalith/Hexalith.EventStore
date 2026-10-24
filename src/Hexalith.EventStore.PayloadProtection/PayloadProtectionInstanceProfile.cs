// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 13.1; amendment section 3.
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
