namespace Hexalith.EventStore.DomainService;

/// <summary>Explicit domain-service intake enrollment for mandatory complete native source evidence.</summary>
/// <param name="PlatformWorkloadId">The sole exact authenticated platform issuer workload.</param><param name="RequiredDomains">Domains that must never fall back to an unproven request.</param>
public sealed record CompleteCommandSourceVerificationOptions(string PlatformWorkloadId, IReadOnlyList<string> RequiredDomains);
