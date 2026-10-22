namespace Hexalith.EventStore.Client.Projections;

/// <summary>A prepared shared projection catch-up batch could not be proven complete and can be retried.</summary>
public sealed class SharedProjectionWriteUnavailableException : Exception
{
    /// <summary>Creates a retryable write failure with its underlying batch error.</summary>
    public SharedProjectionWriteUnavailableException(Exception innerException)
        : base("The shared projection write is unavailable.", innerException)
    {
    }
}
