using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Explicit exact-source enrollment. Authority lookup must never call back into the originating aggregate.</summary>
public interface ICompleteCommandSourceAuthority
{
    /// <summary>Whether this exact addressed source requires complete-prefix authority. Enrolled scopes cannot silently fall back.</summary>
    bool Requires(AggregateIdentity identity);
    /// <summary>Independently verifies current enrollment and epoch; absent or revoked permission returns null.</summary>
    Task<CompleteCommandSourceAuthorization?> AuthorizeAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
