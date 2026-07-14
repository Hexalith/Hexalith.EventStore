namespace Hexalith.EventStore.DomainService;

/// <summary>Authoritative local identity used to bind named projection route catalogs.</summary>
public sealed class DomainProjectionIdentityOptions {
    /// <summary>Gets or sets the DAPR application id of this domain service.</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>Gets or sets the exact deployed service version.</summary>
    public string ServiceVersion { get; set; } = "v1";

    /// <summary>Validates that the local binding is complete.</summary>
    public void Validate() {
        ArgumentException.ThrowIfNullOrWhiteSpace(AppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ServiceVersion);
    }
}
