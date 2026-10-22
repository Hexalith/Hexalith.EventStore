namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record TenantNotFoundRejection(string TenantId) : IRejectionEvent;
