namespace Hexalith.EventStore.ProviderVerification;

internal sealed record ProviderHostMetadata(
    string Server,
    string Pipeline,
    string Transport,
    string AddressFamily,
    string BindScope,
    string PortAllocation);
