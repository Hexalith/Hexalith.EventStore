namespace Hexalith.EventStore.Server.Commands;

/// <summary>Represents live same-key reuse with a different canonical intent.</summary>
public sealed class IdempotencyConflictException : InvalidOperationException
{
    /// <summary>Initializes a support-safe canonical idempotency conflict.</summary>
    /// <param name="correlationId">The current request correlation identifier.</param>
    public IdempotencyConflictException(string correlationId)
        : base("The idempotency key is associated with a different canonical intent.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        CorrelationId = correlationId;
    }

    /// <summary>Gets the current request correlation identifier.</summary>
    public string CorrelationId { get; }
}
