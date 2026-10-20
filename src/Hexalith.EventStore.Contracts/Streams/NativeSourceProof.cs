namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Content-free native incarnation and committed-prefix evidence returned by the existing actor inspection.</summary>
/// <param name="CodecVersion">The native proof codec version.</param>
/// <param name="TenantId">The exact source Tenant.</param>
/// <param name="Domain">The exact source domain.</param>
/// <param name="AggregateId">The exact source aggregate.</param>
/// <param name="SourceIncarnation">The native persisted stream incarnation.</param>
/// <param name="HeadSequence">The committed aggregate-local head.</param>
/// <param name="HeadMessageId">The native event identity at that head, absent at zero.</param>
/// <param name="CumulativeDigest">The native persisted full-prefix digest.</param>
/// <param name="RetainedFloor">The native retained prefix floor.</param>
/// <param name="Erased">Whether the native source is erased.</param>
public sealed record NativeSourceProof(int CodecVersion, string TenantId, string Domain, string AggregateId,
    string SourceIncarnation, long HeadSequence, string? HeadMessageId, string CumulativeDigest,
    long RetainedFloor, bool Erased);
