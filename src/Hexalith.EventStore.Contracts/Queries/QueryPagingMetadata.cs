using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public paging metadata returned with query responses when paging policy applies.
/// </summary>
/// <param name="PageSize">The effective page size.</param>
/// <param name="Offset">The effective offset when offset paging applies.</param>
/// <param name="NextCursor">The next cursor when cursor paging applies and a next page exists.</param>
/// <param name="TotalCount">The total count when the projection can provide it authoritatively.</param>
/// <param name="HasMore">Whether the producer can authoritatively determine that another page exists.</param>
[DataContract]
public sealed record QueryPagingMetadata(
    [property: DataMember] int PageSize,
    [property: DataMember] int? Offset = null,
    [property: DataMember] string? NextCursor = null,
    [property: DataMember] long? TotalCount = null,
    [property: DataMember] bool? HasMore = null);
