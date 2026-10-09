namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact immutable manifest and attestation/protection result inputs. Signing, owner consumption and Product decisions remain independently authenticated.</summary>
/// <param name="BatchId">Exact BatchId.</param>
/// <param name="BatchKind">Exact BatchKind.</param>
/// <param name="BatchOrdinal">Exact BatchOrdinal.</param>
/// <param name="Targets">Exact Targets.</param>
/// <param name="ManifestDigest">Exact ManifestDigest.</param>
/// <param name="AttestationOrdinal">Exact AttestationOrdinal.</param>
/// <param name="CapabilityKeyVersion">Exact CapabilityKeyVersion.</param>
/// <param name="AttestationDigest">Exact AttestationDigest.</param>
/// <param name="ProtectionOutcome">Exact ProtectionOutcome.</param>
/// <param name="ProtectionReceiptId">Exact ProtectionReceiptId.</param>
/// <param name="TargetReceiptIds">Exact TargetReceiptIds.</param>
/// <param name="BlockSetRevision">Exact BlockSetRevision.</param>
public sealed record GovernanceBatchCommand(string BatchId, string BatchKind, long BatchOrdinal, IReadOnlyList<GovernanceProtectionTarget> Targets, string ManifestDigest, long AttestationOrdinal, string CapabilityKeyVersion, string AttestationDigest, string ProtectionOutcome, string ProtectionReceiptId, IReadOnlyList<string> TargetReceiptIds, long BlockSetRevision)
{
    /// <summary>Exact retained fourteen-field canonical signed artifact; mandatory for issue/replacement/dispatch/protection outcomes.</summary>
    public DeletionBatchCapabilityV1? Capability { get; init; }
    /// <summary>Exact original detached ES256 artifact independently verified against the current retained public anchor.</summary>
    public string DetachedJws { get; init; } = "";
    /// <summary>Shared canonical payload SHA-256 signing request identity, never a caller-generated alternative.</summary>
    public string SigningRequestId { get; init; } = "";
}
