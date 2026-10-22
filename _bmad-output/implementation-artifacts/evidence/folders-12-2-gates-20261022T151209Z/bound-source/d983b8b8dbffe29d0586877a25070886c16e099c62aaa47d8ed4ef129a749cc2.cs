using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Payload-free frozen source and candidate references for one compatible legacy replay.</summary>
/// <param name="OperationId">The persisted operation identity reused during recovery.</param>
/// <param name="AppId">The originally resolved domain-service app.</param>
/// <param name="ServiceVersion">The originally resolved service version.</param>
/// <param name="HeadSequence">The inclusive frozen source head.</param>
/// <param name="HeadMessageId">The original EventStore head identity.</param>
public sealed record ProjectionLegacyDeliveryIntent(string OperationId, string AppId, string ServiceVersion,
    long HeadSequence, string HeadMessageId)
{
    /// <summary>Gets the stable discovered route, once bound before staging.</summary>
    public string? ProjectionType { get; init; }

    /// <summary>Gets the immutable complete-source fingerprint, once validated.</summary>
    public string? SourceFingerprint { get; init; }

    /// <summary>Gets the immutable prepared-state fingerprint; model bytes stay in the projection actor.</summary>
    public string? CandidateFingerprint { get; init; }

    /// <summary>Gets the unique active worker claim; stable source/candidate identities never change on reclaim.</summary>
    public string? InvocationToken { get; init; }

    /// <summary>Gets the bounded worker claim expiry, independently of the original candidate identity.</summary>
    public DateTimeOffset? LeaseExpiresAtUtc { get; init; }

    /// <summary>Gets the original persisted delivery reservation fence used for same-work recovery.</summary>
    public long? DeliveryFencingToken { get; init; }

    /// <summary>Gets the original activation revision; later appends must survive completed-work cleanup.</summary>
    public long FrozenActivationRevision { get; init; }

    /// <summary>Gets the original actor-proven source anchor, when supported; absent evidence never enables guard mode.</summary>
    public SourceProof? SourceProof { get; init; }
}
