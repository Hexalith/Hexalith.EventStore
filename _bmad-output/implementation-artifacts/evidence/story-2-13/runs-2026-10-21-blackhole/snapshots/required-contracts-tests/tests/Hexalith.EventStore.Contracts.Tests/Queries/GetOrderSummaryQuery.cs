using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

internal class GetOrderSummaryQuery : IQueryContract
{
    public static string QueryType => "get-order-summary";

    public static string Domain => "reporting";

    public static string ProjectionType => "order-summary";
}
