namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Typed content-free replay value produced only after independently authenticated irreversible root deletion.</summary>
public sealed record Erased
{
    /// <summary>Gets the stable replay discriminator.</summary>
    public string State => nameof(Erased);
}
