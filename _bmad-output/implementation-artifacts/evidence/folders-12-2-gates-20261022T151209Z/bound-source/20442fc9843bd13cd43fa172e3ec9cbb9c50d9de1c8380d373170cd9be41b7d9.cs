namespace Hexalith.EventStore.DomainService;

/// <summary>A bounded page of hashes for one streamed owner's contribution chunks.</summary>
internal sealed record DomainSharedProjectionChunkHashPage(
    long OwnerOrdinal,
    string AggregateId,
    int PageOrdinal,
    string[] Hashes);
