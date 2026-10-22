namespace Hexalith.EventStore.DomainService;

/// <summary>Durable metadata for one bounded owner transport part.</summary>
internal sealed record DomainSharedProjectionTransportPart(
    long OwnerOrdinal,
    string AggregateId,
    int PartOrdinal,
    int FragmentCount,
    int ByteLength,
    string Digest);

/// <summary>One bounded private fragment of a serialized owner transport part.</summary>
internal sealed record DomainSharedProjectionTransportFragment(byte[] Data);
