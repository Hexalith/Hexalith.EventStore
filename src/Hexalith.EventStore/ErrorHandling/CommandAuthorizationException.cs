namespace Hexalith.EventStore.ErrorHandling;

public class CommandAuthorizationException : Exception {
    public CommandAuthorizationException(string tenantId, string? domain, string? commandType, string reason)
        : base($"Authorization failed for tenant '{tenantId}': {reason}") {
        TenantId = tenantId;
        Domain = domain;
        CommandType = commandType;
        Reason = reason;
    }

    public CommandAuthorizationException()
        : base() {
        TenantId = string.Empty;
        Reason = string.Empty;
    }

    public CommandAuthorizationException(string message)
        : base(message) {
        TenantId = string.Empty;
        Reason = message;
    }

    public CommandAuthorizationException(string message, Exception innerException)
        : base(message, innerException) {
        TenantId = string.Empty;
        Reason = message;
    }

    public string TenantId { get; }

    public string? Domain { get; }

    public string? CommandType { get; }

    public string Reason { get; }
}
