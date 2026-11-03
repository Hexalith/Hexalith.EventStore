// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Capability readiness only, with no provider identities or production custody claim.</summary>
/// <param name = "Ready">Whether the bounded fresh probe passed.</param>
/// <param name = "Failure">The constructive failure, if any.</param>
/// <param name = "VersionFingerprint">The safe display fingerprint of the verified exact version.</param>
internal sealed record AzureKeyVaultReadinessResult(bool Ready, PayloadBackendFailure Failure, string? VersionFingerprint = null);
