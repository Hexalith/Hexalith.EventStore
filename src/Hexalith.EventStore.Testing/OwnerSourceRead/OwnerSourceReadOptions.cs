namespace Hexalith.EventStore.Testing.OwnerSourceRead;

/// <summary>Explicit owner-hosted Development selection for the administrator bootstrap source proof.</summary>
public sealed class OwnerSourceReadOptions
{
    /// <summary>Gets or sets the existing owner-private IPC directory.</summary>
    public string Directory { get; set; } = string.Empty;
    /// <summary>Gets or sets the exact enrolled EventStore application identity.</summary>
    public string GatewayAppId { get; set; } = "eventstore";
    /// <summary>Gets or sets the finite helper mode; no other source operation is admitted.</summary>
    public string Mode { get; set; } = "administrator-bootstrap";
}
