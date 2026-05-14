namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public paging policy supplied with a gateway query request.
/// </summary>
/// <param name="PageSize">Optional requested page size. Omission uses <see cref="QueryPolicyLimits.DefaultPageSize"/>.</param>
/// <param name="Offset">Optional zero-based offset for offset paging.</param>
/// <param name="Cursor">Optional cursor token. Cursor paging is reserved and currently rejected by gateway validation.</param>
public sealed record QueryPagingOptions(
    int? PageSize = null,
    int? Offset = null,
    string? Cursor = null);
