namespace Hexalith.Tenants.Contracts.Events;

public record TenantUpdated(string TenantId, string Name, string? Description, DateTimeOffset UpdatedAt) : IEventPayload;
