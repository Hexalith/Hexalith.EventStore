namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — scope of a crypto-shredding workflow request. Drives which fields of
/// <see cref="CryptoShreddingWorkflowIdentity"/> are required.
/// </summary>
public enum CryptoShreddingWorkflowScope {
    /// <summary>Affects every aggregate within the tenant.</summary>
    Tenant = 0,

    /// <summary>Affects every aggregate within the tenant/domain pair.</summary>
    Domain = 1,

    /// <summary>Affects a single aggregate.</summary>
    Aggregate = 2,

    /// <summary>Affects an aggregate stream (alias for <see cref="Aggregate"/> when callers prefer stream terminology).</summary>
    Stream = 3,

    /// <summary>Affects a contiguous sequence range within an aggregate.</summary>
    Range = 4,
}
