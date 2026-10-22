namespace Hexalith.EventStore.Server.Actors;

/// <summary>Actor-local durable inventory staged with every trusted receipt and collision.</summary>
/// <param name="ReceiptKeys">Receipt state names in this aggregate partition.</param>
/// <param name="CollisionKeys">Collision state names in this aggregate partition.</param>
internal sealed record TrustedEffectEvidenceIndex(string[] ReceiptKeys, string[] CollisionKeys)
{
    /// <summary>Fixed actor-state name.</summary>
    public const string StateName = "effect_evidence_index";
}
