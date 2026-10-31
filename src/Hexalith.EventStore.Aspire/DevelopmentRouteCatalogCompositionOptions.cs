namespace Hexalith.EventStore.Aspire;

/// <summary>Explicit owner custody for a retained Development backend. This API never creates replacement digest material.</summary>
/// <remarks>The owner must supply its existing key ring, or material provisioned together with a genuinely new isolated business backend.
/// An absent routing cell or lifecycle record does not prove that business state is new. Keep this object and its custody outside logs and checkouts.</remarks>
public sealed record DevelopmentRouteCatalogCompositionOptions(
    string CustodyDirectory,
    string AuthorityComponentName,
    string AuthorityStateKey,
    string ActiveDigestKeyGeneration,
    IReadOnlyDictionary<string, string> DigestKeys,
    string SecretMapGeneration,
    string SecretStore = "secretstore",
    string LogicalSecretName = "eventstore-idempotency-ring");
