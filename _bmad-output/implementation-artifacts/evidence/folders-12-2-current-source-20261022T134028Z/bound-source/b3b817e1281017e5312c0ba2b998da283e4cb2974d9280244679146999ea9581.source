namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Represents a terminal attempt to reuse a consumed idempotency key after its replay result expired.
/// </summary>
public sealed class IdempotencyKeyExpiredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyKeyExpiredException"/> class.
    /// </summary>
    /// <param name="correlationId">The current request correlation identifier.</param>
    public IdempotencyKeyExpiredException(string correlationId)
        : base("The idempotency key was consumed and its replay result has expired.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        CorrelationId = correlationId;
    }

    /// <summary>Gets the current request correlation identifier.</summary>
    public string CorrelationId { get; }
}
