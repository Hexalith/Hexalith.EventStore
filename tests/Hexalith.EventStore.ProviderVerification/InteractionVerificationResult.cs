namespace Hexalith.EventStore.ProviderVerification;

internal sealed record InteractionVerificationResult(
    int Index,
    string Description,
    string PactFile,
    string ProviderState,
    string ResultCode,
    long DurationMilliseconds,
    IReadOnlyList<ProviderStateEvent> StateEvents);
