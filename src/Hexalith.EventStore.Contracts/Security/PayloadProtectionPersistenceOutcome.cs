namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Describes whether the actor-owned event or snapshot save became durable.
/// Implements Story 8.1 sections 9 and 10.3 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public enum PayloadProtectionPersistenceOutcome {
    /// <summary>The exact protected event or snapshot save is confirmed durable.</summary>
    Persisted = 1,

    /// <summary>The protected event or snapshot save is confirmed not durable.</summary>
    NotPersisted = 2,

    /// <summary>The protected event or snapshot save may have become durable and requires reconciliation.</summary>
    Unknown = 3,
}
