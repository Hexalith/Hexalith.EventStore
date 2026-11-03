// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 11 and 15.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>A fixed-message transport failure with no original exception, provider body or identity.</summary>
internal sealed class AzureKeyVaultTransportException(PayloadBackendFailure failure) : Exception("The key transport failed.")
{
    /// <summary>Gets the closed transport classification.</summary>
    internal PayloadBackendFailure Failure { get; } = failure;
}
