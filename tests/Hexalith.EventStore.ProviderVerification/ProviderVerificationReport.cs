namespace Hexalith.EventStore.ProviderVerification;

internal sealed record ProviderVerificationReport(
    string Schema,
    string FinalVerdict,
    IReadOnlyList<string> ReasonCodes,
    int RequestedInteractionCount,
    int ReportedInteractionCount,
    int RequestedStateCount,
    int SetupEventCount,
    int TeardownEventCount,
    bool Complete,
    bool HostStarted,
    bool ReadyProbePassed,
    bool HostStopped,
    bool PortClosed,
    ProviderHostMetadata Host,
    ProviderVerificationTiming Timing,
    IdentityEvidence? Identity,
    IReadOnlyList<InputHash> InputHashes,
    IReadOnlyList<InteractionVerificationResult> Interactions);
