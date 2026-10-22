using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

internal sealed record CreateCounter(string AggregateId) : ICommandContract
{
    public static string CommandType => "create-counter";

    public static string Domain => "counter";
}
