using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record InsufficientPermissionsRejection(
    string TenantId,
    string ActorUserId,
    TenantRole? ActorRole,
    string CommandName) : IRejectionEvent;
