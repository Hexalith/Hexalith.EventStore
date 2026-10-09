using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.EventStore.Server.Tests.Queries;
/// <summary>Exercises explicit logical cache classification without legacy execution.</summary>
public sealed class DaprLogicalQueryCachingActor : CachingProjectionActor
{
    /// <summary>Provides the fixture callback counts and fault controls.</summary>
    internal DaprLogicalQueryCachingActor(ActorHost host, IETagService etag, LogicalQueryRouteTable routes, Func<QueryEnvelope, CancellationToken, Task<QueryResult>> execute) : base(host, etag, NullLogger.Instance, routes, execute)
    {
    }

    protected override Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope) => throw new InvalidOperationException("Legacy execution is forbidden here.");
}
