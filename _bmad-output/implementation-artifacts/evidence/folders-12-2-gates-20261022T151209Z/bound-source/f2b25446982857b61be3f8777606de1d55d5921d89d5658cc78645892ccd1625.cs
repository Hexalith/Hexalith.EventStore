namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record UserNotInTenantRejection(string TenantId, string UserId) : IRejectionEvent;
