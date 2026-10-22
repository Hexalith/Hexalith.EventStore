using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Query contract for listing global administrators from the fixed platform authority scope.
/// </summary>
[RestRoute(RestVerb.Get, "~/api/global-administrators", ApiScope = "tenants")]
[RestQueryBinding(RestQueryBindingSource.Constant, "global-administrators", RestQueryBindingSource.Constant, "global-administrators")]
public sealed class GetGlobalAdministratorsQuery : IQueryContract {
    public string? Cursor { get; init; }

    public int PageSize { get; init; }

    public static string QueryType => "get-global-administrators";

    public static string Domain => "global-administrators";

    public static string ProjectionType => "global-administrators";
}
