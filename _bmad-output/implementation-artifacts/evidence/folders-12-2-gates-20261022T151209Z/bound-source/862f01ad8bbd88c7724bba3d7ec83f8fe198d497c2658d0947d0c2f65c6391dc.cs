using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public paging policy supplied with a gateway query request.
/// </summary>
/// <param name="PageSize">Optional requested page size. Omission uses <see cref="QueryPolicyLimits.DefaultPageSize"/>.</param>
/// <param name="Offset">Optional zero-based offset for offset paging.</param>
/// <param name="Cursor">Optional opaque cursor token. Cursor-only requests pass gateway validation for downstream query validation.</param>
[DataContract]
public sealed record QueryPagingOptions(
    [property: DataMember] int? PageSize = null,
    [property: DataMember] int? Offset = null,
    [property: DataMember] string? Cursor = null);
