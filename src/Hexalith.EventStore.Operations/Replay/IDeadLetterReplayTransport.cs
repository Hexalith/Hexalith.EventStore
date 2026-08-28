namespace Hexalith.EventStore.Operations.Replay;

/// <summary>
/// Delivers one retained structured CloudEvent through Dapr service invocation.
/// </summary>
public interface IDeadLetterReplayTransport
{
    /// <summary>Delivers the original bytes to the configured subscriber route.</summary>
    Task DeliverAsync(byte[] body, CancellationToken cancellationToken = default);
}
