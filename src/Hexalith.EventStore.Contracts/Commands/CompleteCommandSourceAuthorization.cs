using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>Current exact source enrollment, supplied by the independent installation authority.</summary>
/// <param name="Identity">The enrolled exact stream.</param>
/// <param name="AuthorityRevision">The independently governed current epoch.</param>
/// <param name="ValidUntil">The exclusive enrollment expiry.</param>
public sealed record CompleteCommandSourceAuthorization(AggregateIdentity Identity, string AuthorityRevision, DateTimeOffset ValidUntil);
