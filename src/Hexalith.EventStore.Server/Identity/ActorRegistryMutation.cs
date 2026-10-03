namespace Hexalith.EventStore.Server.Identity;

/// <summary>Private exact-retry registry operation; contains purpose-keyed alias digests only.</summary>
/// <param name="RegistryNamespace">The canonical private global registry namespace.</param>
/// <param name="OperationId">The immutable logical operation identity.</param>
/// <param name="AliasDigest">The purpose-separated keyed login alias digest.</param>
/// <param name="ActorId">The stable opaque actor.</param>
/// <param name="ExpectedRevision">The expected current actor revision, zero for enrollment.</param>
/// <param name="Active">The requested capability status.</param>
/// <param name="ContinuityVerified">Whether alias administration has verified continuity.</param>
/// <param name="AliasActive">Whether the administered alias remains usable.</param>
/// <param name="RetiredAliasDigest">An exact previous alias to retire atomically during login replacement.</param>
/// <param name="ProvenanceId">The verified opaque operator provenance.</param>
public sealed record ActorRegistryMutation(string RegistryNamespace, string OperationId, string AliasDigest,
    string ActorId, long ExpectedRevision, bool Active, bool ContinuityVerified, string ProvenanceId, bool AliasActive = true, string? RetiredAliasDigest = null);
