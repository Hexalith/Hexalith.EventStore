namespace Hexalith.EventStore.DomainService;

/// <summary>A fixed-size rolling origin-denial disposition; contains only bounded reason, counts and binding digests.</summary>
/// <param name="ReasonCode">The closed denial category.</param>
/// <param name="CommandBindingDigest">Digest of the addressed command metadata and payload digest.</param>
/// <param name="OriginBindingDigest">Digest of claimed origin metadata; never assertions or credentials.</param>
/// <param name="Count">Saturating number of recorded denials.</param>
/// <param name="RecordedAt">When this disposition was recorded.</param>
internal sealed record TrustedEffectOriginDenialRecord(string ReasonCode, string CommandBindingDigest,
    string OriginBindingDigest, long Count, DateTimeOffset RecordedAt);
