namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record GlobalAdministratorAlreadyExistsRejection(string TenantId, string UserId) : IRejectionEvent;
