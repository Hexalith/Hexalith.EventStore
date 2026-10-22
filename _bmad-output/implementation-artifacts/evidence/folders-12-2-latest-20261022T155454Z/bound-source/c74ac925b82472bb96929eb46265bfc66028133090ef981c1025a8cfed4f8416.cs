namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — policy controlling whether and how a key reference is stored alongside
/// crypto-shredding audit/admission records. Aliases are sensitive-by-default; raw alias text is
/// NEVER stored. When the policy permits a reference, a SHA-256 fingerprint of the alias is used.
/// </summary>
public enum KeyReferencePolicy {
    /// <summary>No key reference is stored. Default for surfaces where any alias would be sensitive.</summary>
    NoKeyReference = 0,

    /// <summary>A fingerprinted alias is stored. The raw alias is never persisted.</summary>
    AliasOnly = 1,

    /// <summary>A fingerprinted alias plus the provider-neutral scheme family is stored.</summary>
    AliasAndScheme = 2,
}
