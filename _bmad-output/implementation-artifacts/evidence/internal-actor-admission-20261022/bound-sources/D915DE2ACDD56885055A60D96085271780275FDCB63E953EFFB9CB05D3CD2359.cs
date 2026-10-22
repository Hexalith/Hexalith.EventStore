using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>An authenticated complete partition of a stable source prefix for one retention purpose.</summary>
/// <param name="Identity">The exact source scope.</param>
/// <param name="Purpose">The admitted retention purpose.</param>
/// <param name="Head">The inclusive current source head.</param>
/// <param name="ObservedAt">The source observation instant.</param>
/// <param name="Events">Readable attribution events retaining their original source positions.</param>
/// <param name="ExcludedSequences">Every excluded profile/non-attribution position, without its payload or type.</param>
/// <param name="ObservationId">The opaque authenticated observation identity.</param>
public sealed record RetainedIdentityHistoryStream(
    AggregateIdentity Identity,
    string Purpose,
    long Head,
    DateTimeOffset ObservedAt,
    IReadOnlyList<StreamReadEvent> Events,
    IReadOnlyList<long> ExcludedSequences,
    string ObservationId)
{
    /// <summary>Gets the current exact-source authorization revision bound to this observation.</summary>
    public string? AuthorityRevision { get; init; }

    /// <summary>Gets the exclusive earliest authority or retained-event expiry; transit never extends it.</summary>
    public DateTimeOffset ValidUntil { get; init; }

    /// <summary>Gets authenticated actor-free expired-transition proofs, disjoint from readable events and profile exclusions.</summary>
    /// <remarks>Legacy certificates omit this collection and retain strict missing-predecessor denial.</remarks>
    public IReadOnlyList<ExpiredIdentityHistoryCertificate> ExpiredEvents { get; init; } = [];
}
