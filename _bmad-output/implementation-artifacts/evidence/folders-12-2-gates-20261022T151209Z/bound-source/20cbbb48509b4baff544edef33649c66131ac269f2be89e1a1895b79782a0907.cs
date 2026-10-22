namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Explicit catalog-bound source, owner-proof and recovery semantics for one route.</summary>
/// <param name="Version">The descriptor version.</param>
/// <param name="SourceCodecVersion">The supported stored-source codec.</param>
/// <param name="GuardEvidenceVersion">The supported completion evidence version.</param>
/// <param name="HandlerSemantics">The exact versioned handler semantics.</param>
/// <param name="SelectedOwnerProofProtocol">The read-only selected-owner proof protocol.</param>
/// <param name="PerEventBatchIdentity">The existing stable per-event batch derivation, absent for full replay.</param>
/// <param name="AppliedReceiptRecovery">The existing per-event progress recovery protocol, absent for full replay.</param>
public sealed record ProjectionSourceGuardCapability(
    int Version,
    int SourceCodecVersion,
    int GuardEvidenceVersion,
    string HandlerSemantics,
    string SelectedOwnerProofProtocol,
    string? PerEventBatchIdentity,
    string? AppliedReceiptRecovery)
{
    /// <summary>Gets an explicitly enrolled new-owner derivation; absence never authorizes initialization.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? InitialOwnerProtocol { get; init; }

    /// <summary>Gets explicitly negotiated canonical fallback support without changing legacy catalog category flags.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CanonicalReplayProtocol { get; init; }
    /// <summary>The optional route catalog capability identifier.</summary>
    public const string Capability = "projection-source-guard/v1";

    /// <summary>Validates a supported explicit descriptor without inferring capabilities from interfaces.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSupported => Version == 1 && SourceCodecVersion == 1 && GuardEvidenceVersion == 1
        && SelectedOwnerProofProtocol == "projection-owner-proof/v1"
        && (CanonicalReplayProtocol is null || CanonicalReplayProtocol == "staged-full-replay/v1"
            && HandlerSemantics == "single-event-v1")
        && (InitialOwnerProtocol is null || InitialOwnerProtocol == "aggregate-first-event/v1"
            && HandlerSemantics == "single-event-v1")
        && (HandlerSemantics == "full-replay-v1"
            ? PerEventBatchIdentity is null && AppliedReceiptRecovery is null
            : HandlerSemantics == "single-event-v1"
                ? PerEventBatchIdentity == "event-message-id/v1" && AppliedReceiptRecovery == "read-model-batch/v1"
                : HandlerSemantics == "mapped-incremental-replay-v1"
                    ? PerEventBatchIdentity == "event-message-id/v1" && AppliedReceiptRecovery == "mapped-append/v1"
                    : HandlerSemantics == "shared-epoch-v1"
                        && PerEventBatchIdentity == "source-sequence-position/v1" && AppliedReceiptRecovery == "shared-applied/v1");
}
