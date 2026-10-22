namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record TenantAlreadyExistsRejection(string TenantId) : IRejectionEvent;
