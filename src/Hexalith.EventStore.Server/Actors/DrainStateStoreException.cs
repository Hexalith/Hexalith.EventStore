namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Wraps drain failures that occur at the actor state-store boundary.
/// </summary>
internal sealed class DrainStateStoreException : InvalidOperationException {
    public DrainStateStoreException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
