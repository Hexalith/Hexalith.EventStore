using Hexalith.EventStore.Contracts.Rest;

// Opts this external-facing API host into REST controller generation for the "counter" domain.
// RestTenantSource.Route requires a {tenant} (or {tenantId}) route parameter and fails closed otherwise.
[assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]
