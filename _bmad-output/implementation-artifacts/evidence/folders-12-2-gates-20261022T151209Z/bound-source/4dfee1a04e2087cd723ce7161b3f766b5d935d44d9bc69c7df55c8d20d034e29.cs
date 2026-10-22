namespace Hexalith.EventStore.Client.Projections;

/// <summary>The fold failed below its parking limit; the delivery remains durably journaled for retry.</summary>
public sealed class SharedProjectionRetryableFoldException : Exception
{
    internal SharedProjectionRetryableFoldException(SharedProjectionFailureStatus failure)
        : base("The shared projection fold failed and its delivery remains journaled for retry.")
    {
        Failure = failure;
    }

    /// <summary>Gets the durable failed source position and attempt count.</summary>
    public SharedProjectionFailureStatus Failure { get; }
}
