
using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors.Authorization;

/// <summary>
/// DAPR actor interface for application-managed tenant authorization.
/// </summary>
/// <remarks>
/// <para>Applications implement this interface to provide dynamic tenant access control
/// managed at runtime via actor state, replacing static JWT claims.</para>
/// <para><b>Implementation guidance:</b></para>
/// <list type="bullet">
/// <item>The actor ID is the tenant ID — each tenant has its own actor instance.</item>
/// <item>Keep activation cost low — avoid expensive I/O in OnActivateAsync.</item>
/// <item>If your backing store is unavailable, THROW an exception (do not return denied).
/// The proxy converts exceptions to 503, preserving fail-closed semantics.</item>
/// <item>DAPR idle timeout (default 60 min) deactivates unused actors automatically.</item>
/// </list>
/// </remarks>
public interface ITenantValidatorActor : IActor {
    /// <summary>
    /// Validates whether a user has access to this tenant.
    /// </summary>
    /// <param name="request">The tenant validation request containing user and tenant identifiers.</param>
    /// <returns>An <see cref="ActorValidationResponse"/> indicating whether access is authorized.</returns>
    Task<ActorValidationResponse> ValidateTenantAccessAsync(TenantValidationRequest request);
}
