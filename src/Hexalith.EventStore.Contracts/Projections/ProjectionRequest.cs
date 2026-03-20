
namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Request sent to a domain service's /project endpoint with per-aggregate granularity.
/// Contains the aggregate identity and the events to project.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="Events">The events to project, in sequence order.</param>
public record ProjectionRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    ProjectionEventDto[] Events);
