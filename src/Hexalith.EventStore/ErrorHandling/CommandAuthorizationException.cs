namespace Hexalith.EventStore.ErrorHandling;

using Hexalith.EventStore.Contracts.Authorization;

public class CommandAuthorizationException : Exception {
    public CommandAuthorizationException(
        string tenantId,
        string? domain,
        string? commandType,
        string reason,
        AuthorizationFailureReason reasonCode = AuthorizationFailureReason.InsufficientPermission)
        : base($"Authorization failed for tenant '{tenantId}': {reason}") {
        TenantId = tenantId;
        Domain = domain;
        CommandType = commandType;
        Reason = reason;
        ReasonCode = reasonCode;
    }

    public CommandAuthorizationException()
        : base() {
        TenantId = string.Empty;
        Reason = string.Empty;
        ReasonCode = AuthorizationFailureReason.AuthorizationServiceUnavailable;
    }

    public CommandAuthorizationException(string message)
        : base(message) {
        TenantId = string.Empty;
        Reason = message;
        ReasonCode = AuthorizationFailureReason.AuthorizationServiceUnavailable;
    }

    public CommandAuthorizationException(string message, Exception innerException)
        : base(message, innerException) {
        TenantId = string.Empty;
        Reason = message;
        ReasonCode = AuthorizationFailureReason.AuthorizationServiceUnavailable;
    }

    public string TenantId { get; }

    public string? Domain { get; }

    public string? CommandType { get; }

    public string Reason { get; }

    public AuthorizationFailureReason ReasonCode { get; }
}
