using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

// Declares this Contracts assembly as the McpCli "tenants" module. Every operation is submitted under the
// fixed platform tenant "system"; managed tenants are aggregates, never envelope tenants. Payload
// identifiers are opaque strings.
[assembly: HexalithModule(
    "tenants",
    "Manage tenants: their lifecycle, user memberships and roles, configuration settings, and audit trail. Calls are always submitted under the fixed platform tenant 'system'; operations that take a TenantId act on that tenant, while tenants.list-tenants and tenants.get-user-tenants read across tenants. A global administrator is a platform operator allowed to manage every tenant.",
    IdentifierKind.String,
    FixedTenant = TenantIdentity.DefaultTenantId)]
