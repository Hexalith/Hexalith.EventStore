using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Events;

public record UserRoleChanged(string TenantId, string UserId, TenantRole OldRole, TenantRole NewRole) : IEventPayload;
