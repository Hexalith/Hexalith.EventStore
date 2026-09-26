using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.DomainService;

namespace Consumer;

public static class PublishedApiSmoke
{
    public static readonly Type[] Resolved =
    [
        typeof(IAsyncDomainProjectionHandler),
        typeof(IReadModelStore),
        typeof(IReadModelBatchStore),
        typeof(ReadModelWritePolicy),
        typeof(IDomainQueryHandler),
        typeof(IQueryCursorCodec),
        typeof(QueryCursorScope),
    ];
}
