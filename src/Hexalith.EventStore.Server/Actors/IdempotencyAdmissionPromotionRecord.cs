namespace Hexalith.EventStore.Server.Actors;

/// <summary>Guards a copied target record from execution until directory flip.</summary>
/// <param name="SchemaVersion">The promotion schema version.</param>
/// <param name="SourceActorId">The prior canonical source actor.</param>
/// <param name="Activated">Whether the directory has flipped and activated this target.</param>
public sealed record IdempotencyAdmissionPromotionRecord(
    int SchemaVersion,
    string SourceActorId,
    bool Activated)
{
    /// <summary>Gets the only promotion schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
