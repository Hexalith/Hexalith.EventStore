namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Validates that a command's tenant matches the actor's tenant identity.
/// SEC-2: Defense-in-depth tenant isolation at actor level.
/// </summary>
public interface ITenantValidator
{
    /// <summary>
    /// Validates that the command tenant matches the actor's tenant (extracted from actor ID).
    /// </summary>
    /// <param name="commandTenantId">The tenant ID from the command envelope.</param>
    /// <param name="actorId">The actor ID in format {tenant}:{domain}:{aggregateId}.</param>
    /// <exception cref="TenantMismatchException">Thrown when tenants do not match.</exception>
    /// <exception cref="InvalidOperationException">Thrown when actor ID format is malformed.</exception>
    void Validate(string commandTenantId, string actorId);
}
