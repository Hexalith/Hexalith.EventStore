namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Represents a fail-closed collision between a message identifier and a different or unverifiable command identity.
/// </summary>
public sealed class CommandIdentityConflictException : InvalidOperationException
{
    /// <summary>Initializes a command identity conflict with support-safe identity metadata.</summary>
    public CommandIdentityConflictException(string messageId, string correlationId, string tenantId)
        : base("The command message identifier is already associated with a different or unverifiable command identity.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        MessageId = messageId;
        CorrelationId = correlationId;
        TenantId = tenantId;
    }

    /// <summary>Gets the conflicting incoming message identifier.</summary>
    public string MessageId { get; }

    /// <summary>Gets the request correlation identifier used for tracing.</summary>
    public string CorrelationId { get; }

    /// <summary>Gets the authorized tenant identifier.</summary>
    public string TenantId { get; }
}
