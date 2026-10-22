namespace Hexalith.EventStore.ProviderVerification;

internal sealed record ProviderVerificationTiming(
    VerificationPhaseTiming Run,
    VerificationPhaseTiming Startup,
    VerificationPhaseTiming Readiness,
    VerificationPhaseTiming Cleanup);
