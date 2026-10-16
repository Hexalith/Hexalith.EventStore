namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Returns bounded proof or typed failure with the actual metadata work performed.</summary>
/// <param name="Proof">The persisted proof, or null when unproven.</param>
/// <param name="ReasonCode">The bounded failure reason.</param>
/// <param name="MetadataKeyReads">The actual metadata-key reads.</param>
/// <param name="ReadBodyBytes">The actual read-body bytes, including failed reads.</param>
public sealed record ProjectionOwnerProofResult(ProjectionOwnerProof? Proof, string? ReasonCode,
    int MetadataKeyReads, long ReadBodyBytes);
