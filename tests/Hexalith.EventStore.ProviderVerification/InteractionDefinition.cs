namespace Hexalith.EventStore.ProviderVerification;

internal sealed record InteractionDefinition(
    string Description,
    string ProviderState,
    string Method,
    string Path,
    string PactFile,
    string PactSha256);
