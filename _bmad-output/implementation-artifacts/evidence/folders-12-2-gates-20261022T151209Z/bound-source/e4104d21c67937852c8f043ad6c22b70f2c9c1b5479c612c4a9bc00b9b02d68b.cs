using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record RoleEscalationRejection(string TenantId, string UserId, TenantRole AttemptedRole) : IRejectionEvent;
