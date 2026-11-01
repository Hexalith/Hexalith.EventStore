namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// A retryable checkpointed projection failure: nothing durable was decided, so the delivery must not be
/// acknowledged and authorization must treat the projection as unavailable.
/// </summary>
public sealed class CheckpointedProjectionUnavailableException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionUnavailableException"/> class.
    /// </summary>
    public CheckpointedProjectionUnavailableException()
        : this(CheckpointedProjectionUnavailableReason.ProviderUnavailable) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CheckpointedProjectionUnavailableException(string message)
        : base(message) => Reason = CheckpointedProjectionUnavailableReason.ProviderUnavailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying failure.</param>
    public CheckpointedProjectionUnavailableException(string message, Exception innerException)
        : base(message, innerException) => Reason = CheckpointedProjectionUnavailableReason.ProviderUnavailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckpointedProjectionUnavailableException"/> class.
    /// </summary>
    /// <param name="reason">The content-free failure reason.</param>
    /// <param name="innerException">The underlying failure, when any.</param>
    public CheckpointedProjectionUnavailableException(
        CheckpointedProjectionUnavailableReason reason,
        Exception? innerException = null)
        : base($"The checkpointed projection is unavailable ({reason}); the delivery remains retryable.", innerException)
        => Reason = reason;

    /// <summary>Gets the content-free failure reason.</summary>
    public CheckpointedProjectionUnavailableReason Reason { get; }
}
