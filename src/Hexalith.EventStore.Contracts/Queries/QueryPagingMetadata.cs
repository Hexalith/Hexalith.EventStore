using System.Runtime.Serialization;
using System.Text.Json.Serialization;

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
[method: JsonConstructor]
public sealed record QueryPagingMetadata(
    [property: DataMember] int PageSize,
    [property: DataMember] int? Offset = null,
    [property: DataMember] string? NextCursor = null,
    [property: DataMember] long? TotalCount = null,
    [property: DataMember] bool? HasMore = null) {
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPagingMetadata"/> record using the original paging metadata shape.
    /// </summary>
    /// <param name="PageSize">The effective page size.</param>
    /// <param name="Offset">The effective offset when offset paging applies.</param>
    /// <param name="NextCursor">The next cursor when cursor paging applies and a next page exists.</param>
    /// <param name="TotalCount">The total count when the projection can provide it authoritatively.</param>
    public QueryPagingMetadata(int PageSize, int? Offset, string? NextCursor, long? TotalCount)
        : this(PageSize, Offset, NextCursor, TotalCount, HasMore: null) {
    }

    /// <summary>
    /// Deconstructs the original paging metadata contract shape.
    /// </summary>
    /// <param name="PageSize">The effective page size.</param>
    /// <param name="Offset">The effective offset when offset paging applies.</param>
    /// <param name="NextCursor">The next cursor when cursor paging applies and a next page exists.</param>
    /// <param name="TotalCount">The total count when the projection can provide it authoritatively.</param>
    public void Deconstruct(out int PageSize, out int? Offset, out string? NextCursor, out long? TotalCount) {
        PageSize = this.PageSize;
        Offset = this.Offset;
        NextCursor = this.NextCursor;
        TotalCount = this.TotalCount;
    }
}
