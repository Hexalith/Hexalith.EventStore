namespace Hexalith.EventStore.Server.Commands;

/// <summary>Explicit trusted effect origin and command policy.</summary>
public sealed class TrustedEffectAuthorityOptions
{
    /// <summary>Gets or sets the allowed exact origin-purpose-command tuples.</summary>
    public TrustedEffectAuthorityRule[] Rules { get; set; } = [];
}
