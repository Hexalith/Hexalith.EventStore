namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Represents a reviewed fold whose top-level shape collides with the command DTO.</summary>
public sealed class SnapshotCollisionTestState
{
    /// <summary>Gets or sets the domain counter.</summary>
    public int CurrentSequence { get; set; }

    /// <summary>Gets or sets an ordinary domain property that collides with the legacy DTO name.</summary>
    public string[] Events { get; set; } = [];

    /// <summary>Applies the canonical admitted event through the shared Apply table.</summary>
    public void Apply(SnapshotCaptureTestEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        CurrentSequence += payload.Amount;
    }
}
