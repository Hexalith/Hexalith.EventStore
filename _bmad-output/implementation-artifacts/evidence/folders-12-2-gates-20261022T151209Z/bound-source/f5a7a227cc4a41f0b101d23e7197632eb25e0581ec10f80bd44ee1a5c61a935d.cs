namespace Hexalith.EventStore.DomainService;

/// <summary>
/// An untrusted wire assertion that the acting user is a global administrator, presented for verification.
/// </summary>
/// <param name="TenantId">The tenant of the command or query.</param>
/// <param name="Domain">The domain of the command or query.</param>
/// <param name="UserId">The acting user whose current authority must be verified.</param>
/// <param name="CorrelationId">The bounded correlation identifier, for diagnostics only.</param>
public sealed record DomainServiceAdministratorClaim(
    string TenantId,
    string Domain,
    string UserId,
    string CorrelationId);
