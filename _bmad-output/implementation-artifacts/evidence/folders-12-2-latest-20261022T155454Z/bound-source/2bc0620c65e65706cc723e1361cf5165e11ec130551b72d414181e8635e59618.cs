namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record TenantDisabledRejection(string TenantId) : IRejectionEvent;
