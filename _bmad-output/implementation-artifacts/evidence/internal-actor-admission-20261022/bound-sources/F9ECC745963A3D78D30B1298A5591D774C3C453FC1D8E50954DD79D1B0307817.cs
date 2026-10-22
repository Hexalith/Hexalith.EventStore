namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Wraps exceptions raised by the drain publish operation so they keep the publish-failed reason code.
/// </summary>
internal sealed class DrainPublishException : InvalidOperationException {
    public DrainPublishException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
