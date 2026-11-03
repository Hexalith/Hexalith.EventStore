// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>The closed ordinary-vault profile. These options grant no provisioning or Server activation authority.</summary>
public sealed class AzureKeyVaultPayloadProtectionOptions
{
    /// <summary>Gets the ordinary-vault HTTPS origin.</summary>
    public required Uri VaultUri { get; init; }
    /// <summary>Gets the single KEK name.</summary>
    public required string KeyName { get; init; }
    /// <summary>Gets the optional canonical user-assigned managed identity client ID.</summary>
    public string? ManagedIdentityClientId { get; init; }
    /// <summary>Gets the fixed RSA-HSM size.</summary>
    public int ExpectedKeySize { get; init; } = 3072;
    /// <summary>Gets the fixed wrap algorithm.</summary>
    public string WrapAlgorithm { get; init; } = "RSA-OAEP-256";
    /// <summary>Gets the monotonic maximum current-selection age.</summary>
    public TimeSpan KeyVersionRefreshInterval { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>Gets the per-request deadline, also bounded by the enclosing engine phase.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Returns a safe type label.</summary>
    public override string ToString() => nameof(AzureKeyVaultPayloadProtectionOptions);
}
