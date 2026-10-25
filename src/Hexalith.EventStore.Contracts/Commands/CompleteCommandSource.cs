using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>A platform-authenticated complete native prefix, including an explicit empty incarnation. This is distinct from effective-event evolution evidence.</summary>
/// <param name="Identity">The exact addressed source.</param>
/// <param name="AuthorityRevision">The current enrolled authority epoch.</param>
/// <param name="ObservedAt">The platform capture instant.</param>
/// <param name="ValidUntil">The exclusive authorization expiry.</param>
/// <param name="Proof">The complete immutable native prefix.</param>
/// <param name="Events">Every legacy JSON event in sequence order, with native stored-envelope digests.</param>
public sealed record CompleteCommandSource(AggregateIdentity Identity, string AuthorityRevision,
    DateTimeOffset ObservedAt, DateTimeOffset ValidUntil, NativeSourceProof Proof, IReadOnlyList<StreamReadEvent> Events)
{
    /// <summary>The maximum complete event prefix admitted by this carrier.</summary>
    public const int MaximumEvents = 10000;
    /// <summary>The maximum cumulative application event bytes.</summary>
    public const int MaximumPayloadBytes = 16777216;
}
