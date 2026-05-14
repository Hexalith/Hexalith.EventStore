using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.ErrorHandling;

public class CommandAuthorizationException : Exception {
    public CommandAuthorizationException(string tenantId, string? domain, string? commandType, string reason, string? reasonCode = null)
        : base($"Authorization failed for tenant '{tenantId}': {reason}") {
        TenantId = tenantId;
        Domain = domain;
        CommandType = commandType;
        Reason = reason;
        ReasonCode = reasonCode ?? AuthorizationReasonCodes.InsufficientPermission;
    }

    public CommandAuthorizationException()
        : base() {
        TenantId = string.Empty;
        Reason = string.Empty;
        ReasonCode = AuthorizationReasonCodes.InsufficientPermission;
    }

    public CommandAuthorizationException(string message)
        : base(message) {
        TenantId = string.Empty;
        Reason = message;
        ReasonCode = AuthorizationReasonCodes.InsufficientPermission;
    }

    public CommandAuthorizationException(string message, Exception innerException)
        : base(message, innerException) {
        TenantId = string.Empty;
        Reason = message;
        ReasonCode = AuthorizationReasonCodes.InsufficientPermission;
    }

    public string TenantId { get; }

    public string? Domain { get; }

    public string? CommandType { get; }

    public string Reason { get; }

    public string ReasonCode { get; }
}
