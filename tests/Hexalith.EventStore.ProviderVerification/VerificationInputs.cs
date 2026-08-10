namespace Hexalith.EventStore.ProviderVerification;

internal sealed record VerificationInputs(
    string PactDirectory,
    IReadOnlyList<InteractionDefinition> Interactions,
    IReadOnlySet<string> ProviderStates,
    IReadOnlyList<InputHash> Hashes,
    IdentityEvidence Identity);
