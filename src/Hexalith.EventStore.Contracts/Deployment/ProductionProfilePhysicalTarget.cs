namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Explicit public database target including both actor table names.</summary>
/// <param name="Id">The required id.</param>
/// <param name="ProviderId">The required provider id.</param>
/// <param name="Host">The required host.</param>
/// <param name="Port">The required port.</param>
/// <param name="Database">The required database.</param>
/// <param name="TableName">The required table name.</param>
/// <param name="MetadataTableName">The required metadata table name.</param>
public sealed record ProductionProfilePhysicalTarget(
    string Id,
    string ProviderId,
    string Host,
    int Port,
    string Database,
    string TableName,
    string MetadataTableName);
