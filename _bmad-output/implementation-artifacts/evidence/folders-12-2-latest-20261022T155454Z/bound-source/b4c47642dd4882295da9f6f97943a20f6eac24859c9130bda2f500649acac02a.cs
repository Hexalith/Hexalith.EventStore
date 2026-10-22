namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record LastGlobalAdministratorRejection(string TenantId, string UserId) : IRejectionEvent;
