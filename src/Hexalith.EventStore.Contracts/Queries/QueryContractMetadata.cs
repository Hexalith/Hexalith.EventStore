
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Immutable container for resolved query contract metadata.
/// Produced by QueryContractResolver from IQueryContract implementations.
/// </summary>
/// <param name="QueryType">The query type name (kebab-case routing key).</param>
/// <param name="Domain">The owning domain name.</param>
/// <param name="ProjectionType">The projection type for ETag scope.</param>
public record QueryContractMetadata(
    string QueryType,
    string Domain,
    string ProjectionType);
