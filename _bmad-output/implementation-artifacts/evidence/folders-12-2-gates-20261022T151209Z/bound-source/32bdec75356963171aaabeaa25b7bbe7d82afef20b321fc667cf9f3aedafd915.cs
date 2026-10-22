namespace Hexalith.EventStore.ProviderVerification;

internal sealed record VerificationPhaseTiming(
    string ResultCode,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    long DurationMilliseconds);
