namespace Hexalith.Tenants.Contracts.Events;

public record TenantCreated(string TenantId, string Name, string? Description, DateTimeOffset CreatedAt) : IEventPayload;
