namespace Hexalith.EventStore.Server.Commands;

/// <summary>Signals that local idempotency execution-capability verification failed before any actor state mutation.</summary>
internal sealed class IdempotencyCapabilityValidationException : InvalidOperationException
{
    /// <summary>Creates a new capability validation exception preserving the original error message.</summary>
    public IdempotencyCapabilityValidationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
