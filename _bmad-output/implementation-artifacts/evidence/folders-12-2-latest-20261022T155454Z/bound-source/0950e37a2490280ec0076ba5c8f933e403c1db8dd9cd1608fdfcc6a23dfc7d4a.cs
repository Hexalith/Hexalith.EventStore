using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Events.Rejections;

public record TenantLifecycleStateAlreadySetRejection(
    string TenantId,
    TenantStatus CurrentStatus,
    TenantStatus RequestedStatus,
    string CommandName) : IRejectionEvent;
