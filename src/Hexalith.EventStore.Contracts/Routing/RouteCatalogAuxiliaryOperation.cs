namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Inventory of an existing replay, reconstruction or metadata call; never a message declaration.</summary>
/// <param name="AppId">Exact receiver.</param>
/// <param name="Method">Existing canonical receiver method.</param>
/// <param name="Operation">Existing credential operation.</param>
/// <param name="Domain">Exact owning domain.</param>
public sealed record RouteCatalogAuxiliaryOperation(string AppId, string Method, string Operation, string Domain);
