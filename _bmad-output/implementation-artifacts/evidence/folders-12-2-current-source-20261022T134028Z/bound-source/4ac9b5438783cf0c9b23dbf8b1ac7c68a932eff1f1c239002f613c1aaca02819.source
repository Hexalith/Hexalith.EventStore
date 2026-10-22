using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Current owner admission; the client cannot choose the permitted lifecycle event contracts.</summary>
/// <param name="Identity">The exact admitted source.</param>
/// <param name="Purpose">The sole admitted retention purpose.</param>
/// <param name="AuthorityRevision">The current non-rollback authorization revision.</param>
/// <param name="ExpiresAt">The exclusive authority deadline.</param>
/// <param name="EventTypes">Closed owner-declared IIdentityHistoryEvent contracts.</param>
public sealed record RetainedIdentityHistoryGrant(AggregateIdentity Identity, string Purpose,
    string AuthorityRevision, DateTimeOffset ExpiresAt, IReadOnlyList<Type> EventTypes)
{
    /// <summary>Gets exact owner-declared non-attribution contracts permitted only as excluded positions.</summary>
    public IReadOnlyList<string> ExcludedEventTypeNames { get; init; } = [];
}
