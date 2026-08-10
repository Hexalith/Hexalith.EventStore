namespace Hexalith.EventStore.ProviderVerification;

internal sealed record IdentityEvidence(
    string ExpectedSourceSha,
    string ObservedSourceSha,
    string ExpectedVersion,
    string ObservedVersion,
    string ExpectedBuildsSha,
    string ObservedBuildsSha,
    string ReleaseInventorySha256,
    string ObservedReleaseInventorySha256,
    string EvidenceManifestSha256,
    string DecisionRecordSha256,
    string SubjectSha256,
    int ApprovalCount,
    bool ApprovalAuthorized,
    bool RuntimeMatches,
    IReadOnlyList<string> ReasonCodes);
