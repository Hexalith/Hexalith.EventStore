namespace Hexalith.EventStore.ProviderVerification;

internal sealed record ProviderStateEvent(
    string State,
    string Action,
    string ResultCode,
    long DurationMilliseconds);
