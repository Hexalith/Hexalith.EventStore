namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>Explicit host-owned selection for a local Debug proof only.</summary>
public sealed class CatalogInvocationOptions
{
    /// <summary>Gets or sets the existing absolute owner-private IPC directory.</summary>
    public string Directory { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact selected metadata owners.</summary>
    public string[] MetadataAppIds { get; set; } = [];
    /// <summary>Gets or sets the single selected projection owner.</summary>
    public string ProjectionAppId { get; set; } = string.Empty;
}
