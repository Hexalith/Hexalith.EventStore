using Hexalith.EventStore.Contracts.Rest;

[assembly: RestApi("api/{tenant}/counter", "counter", RestTenantSource.Route)]
