namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable conflicting identities at one accepted source position, retained after explicit repair.</summary>
/// <param name="SourceStream">The source within the owning tenant and family scope.</param>
/// <param name="EnvelopePosition">The conflicting source position.</param>
/// <param name="AcceptedDigest">The identity already accepted by the fenced writer.</param>
/// <param name="ConflictingDigest">The rejected incoming identity.</param>
/// <param name="ObservedEpoch">The fence epoch that first recorded this conflict.</param>
/// <param name="ResolvedByOperationId">The later authoritative rebuild that explicitly resolved this evidence.</param>
public sealed record SharedProjectionDeliveryConflict(string SourceStream, long EnvelopePosition,
    string AcceptedDigest, string ConflictingDigest, long ObservedEpoch, string? ResolvedByOperationId = null)
{
    /// <summary>The future owner sequence whose missing prefix must be accepted before this gap can converge.</summary>
    public long? RequiredOwnerSequenceNumber { get; init; }

    /// <summary>The owner sequence accepted and folded with the exact previously rejected gap identity.</summary>
    public long? ResolvedByOwnerSequenceNumber { get; init; }

    /// <summary>Whether explicit covering rebuild or exact ordered catch-up has resolved this evidence.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsResolved => ResolvedByOperationId is not null || ResolvedByOwnerSequenceNumber is not null;
}
