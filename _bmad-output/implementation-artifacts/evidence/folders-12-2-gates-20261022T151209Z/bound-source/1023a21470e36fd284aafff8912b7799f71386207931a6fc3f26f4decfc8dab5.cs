namespace Hexalith.EventStore.Client.Projections;

/// <summary>A generation read failed before a shared projection fold could prepare mutations.</summary>
public sealed class SharedProjectionReadUnavailableException : Exception
{
    /// <summary>Creates a retryable read failure with its underlying store error.</summary>
    public SharedProjectionReadUnavailableException(Exception innerException)
        : base("The shared projection read is unavailable.", innerException)
    {
    }
}
