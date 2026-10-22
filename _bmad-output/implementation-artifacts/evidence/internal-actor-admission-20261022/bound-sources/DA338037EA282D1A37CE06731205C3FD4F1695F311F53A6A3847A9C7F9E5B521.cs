namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists one logical tenant/key directory selection under every protected alias.</summary>
/// <param name="SchemaVersion">The directory schema version.</param>
/// <param name="CanonicalActorId">The sole actor that currently owns authority.</param>
/// <param name="ActiveActorId">The active-key-version promotion target.</param>
/// <param name="Aliases">Every active or retained protected actor alias.</param>
/// <param name="PromotionPhase">The crash-resumable promotion phase.</param>
/// <param name="PromotionSourceActorId">The optional prior canonical actor.</param>
/// <param name="PromotionTargetActorId">The optional prepared target actor.</param>
public sealed record IdempotencyAdmissionDirectoryEntry(
    int SchemaVersion,
    string CanonicalActorId,
    string ActiveActorId,
    IReadOnlyList<string> Aliases,
    IdempotencyAdmissionPromotionPhase PromotionPhase,
    string? PromotionSourceActorId = null,
    string? PromotionTargetActorId = null)
{
    /// <summary>Gets the only directory schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
