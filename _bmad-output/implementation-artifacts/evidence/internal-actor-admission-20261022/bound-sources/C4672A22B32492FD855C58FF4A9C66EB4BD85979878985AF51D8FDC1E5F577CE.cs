namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Verifies, from the domain's own current authorization state, that an acting user really is a global administrator.
/// </summary>
/// <remarks>
/// <para>
/// Wire administrator flags (<c>actor:globalAdmin</c> on a command, <see cref="Contracts.Queries.QueryEnvelope.IsGlobalAdmin"/>
/// on a query) are untrusted at the domain-service boundary. The SDK removes them from every inbound
/// <c>/process</c> and <c>/query</c> request and restores them only when every registered verifier confirms the
/// acting user's current authority. With no verifier registered the flag is always removed.
/// </para>
/// <para>
/// A verifier that cannot reach its authorization state must throw rather than guess, so the request fails closed
/// before any domain work.
/// </para>
/// </remarks>
public interface IDomainServiceAdministratorVerifier
{
    /// <summary>
    /// Returns whether the acting user currently holds global-administrator authority.
    /// </summary>
    /// <param name="claim">The untrusted assertion to verify.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> only when current authority is confirmed.</returns>
    Task<bool> IsCurrentGlobalAdministratorAsync(DomainServiceAdministratorClaim claim, CancellationToken cancellationToken);
}
