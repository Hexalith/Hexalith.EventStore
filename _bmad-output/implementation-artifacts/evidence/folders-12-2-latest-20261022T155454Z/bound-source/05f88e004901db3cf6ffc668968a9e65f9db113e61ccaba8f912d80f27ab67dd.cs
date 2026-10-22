namespace Hexalith.EventStore.ProviderVerification;

internal sealed record PactInteraction(
    string Description,
    string State,
    string Method,
    string Path,
    string PactFile,
    string PactSha256);
