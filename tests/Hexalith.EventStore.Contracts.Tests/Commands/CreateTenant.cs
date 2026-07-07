using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Contracts.Tests.Commands;

internal sealed record CreateTenant(string TenantId) : ICommandContract
{
    public static string CommandType => "create-tenant";

    public static string Domain => "tenants";

    public string AggregateId => TenantId;
}
