using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

internal class GetCounterStatusQuery : IQueryContract
{
    public static string QueryType => "get-counter-status";

    public static string Domain => "counter";

    public static string ProjectionType => "counter";
}
