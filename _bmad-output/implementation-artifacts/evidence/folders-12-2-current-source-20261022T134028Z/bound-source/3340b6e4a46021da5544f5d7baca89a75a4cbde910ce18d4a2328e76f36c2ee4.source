using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Put, "{tenantId}/configuration/{key}", ApiScope = "tenants")]
[HexalithCommand(
    "Create or overwrite one configuration setting, a key with a string value, on an active tenant. Allowed for a global administrator or a TenantOwner of the tenant. A tenant holds at most 100 keys, so adding a new key beyond that is rejected while overwriting an existing key still works; setting a key to the value it already has changes nothing. Every tenant member can read the values, so do not store secrets.",
    Example = """{"TenantId":"acme","Key":"billing.plan","Value":"enterprise"}""")]
public record SetTenantConfiguration(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant to configure.")]
    string TenantId,
    [property: Description("Configuration key, at most 256 characters, case-sensitive. An existing key is overwritten.")]
    string Key,
    [property: Description("Configuration value, at most 1024 characters; an empty string is allowed.")]
    string Value) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "set-tenant-configuration";

    public string AggregateId => TenantId;
}
