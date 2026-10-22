namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists a protected source-to-target promotion redirect.</summary>
/// <param name="SchemaVersion">The redirect schema version.</param>
/// <param name="TargetActorId">The acknowledged target actor identifier.</param>
public sealed record IdempotencyAdmissionRedirectRecord(
    int SchemaVersion,
    string TargetActorId)
{
    /// <summary>Gets the only redirect schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
