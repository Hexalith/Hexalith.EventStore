namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>A conditional authenticated successor binding preserving the exact namespace, inventory and original journals.</summary>
/// <param name="ExpectedEpoch">The predecessor authority epoch.</param><param name="ExpectedEnrollmentDigest">The exact predecessor enrollment compare digest.</param>
/// <param name="Successor">The independently approved current successor enrollment.</param>
public sealed record SourcePublicationAuthorityEnrollmentChange(string ExpectedEpoch, string ExpectedEnrollmentDigest,
    SourcePublicationAuthorityEnrollment Successor);
