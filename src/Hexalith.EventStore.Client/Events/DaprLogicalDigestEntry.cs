namespace Hexalith.EventStore.Client.Events;

/// <summary>Contains one addressed sequence and its admitted application logical digest.</summary>
/// <param name="SequenceNumber">The carried sequence number.</param>
/// <param name="ApplicationLogicalDigest">The carried application logical digest.</param>
internal sealed record DaprLogicalDigestEntry(long SequenceNumber, ReadOnlyMemory<byte> ApplicationLogicalDigest);
