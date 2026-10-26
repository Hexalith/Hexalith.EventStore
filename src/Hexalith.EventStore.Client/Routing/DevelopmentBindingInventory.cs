namespace Hexalith.EventStore.Client.Routing;

/// <summary>Whitelisted checked-in binding metadata, not an authoritative runtime route inventory.</summary>
/// <param name="TenantPattern">Configured tenant pattern, including the existing system binding when present.</param>
/// <param name="Domain">Configured domain.</param>
/// <param name="AppId">Configured target.</param>
/// <param name="CommandMethod">Configured command method.</param>
/// <param name="QueryMethod">The current gateway query dispatch method, independent of the command method.</param>
/// <param name="ContractVersion">Configured version.</param>
public sealed record DevelopmentBindingInventory(string TenantPattern, string Domain, string AppId, string CommandMethod, string QueryMethod, string ContractVersion);
