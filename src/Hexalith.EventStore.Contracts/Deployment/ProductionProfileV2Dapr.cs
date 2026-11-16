namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Required Dapr component details, separate from the provider software version.</summary>
/// <param name="Type">The Dapr component type.</param>
/// <param name="ApiVersion">The Dapr component API version, not its software version.</param>
/// <param name="ActorStateStore">Whether this is an isolated actor state component.</param>
/// <param name="AppScopeIds">The scoped application identities.</param>
/// <param name="AclDigest">The applied ACL identity.</param>
/// <param name="ResiliencyDigest">The applied resiliency identity.</param>
/// <param name="DataMigrationDigest">The data migration identity.</param>
public sealed record ProductionProfileV2Dapr(
    string Type,
    string ApiVersion,
    bool ActorStateStore,
    IReadOnlyList<string> AppScopeIds,
    string AclDigest,
    string ResiliencyDigest,
    string DataMigrationDigest);
