namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Platform authority principal shown in the global-administrator review surface.
/// </summary>
/// <param name="UserId">The literal caller-supplied user identifier.</param>
public sealed record GlobalAdministratorSummary(string UserId);
