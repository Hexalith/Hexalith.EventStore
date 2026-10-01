namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Signals that reminder state could not be changed safely, for example after a compare-and-swap conflict or
/// when a tenant index is full. The refused write leaves durable state unchanged and the work unresolved.
/// Earlier Scheduler or submission side effects may already have occurred; recovery replays the work with
/// the same effect identity.
/// </summary>
internal sealed class ReminderFailClosedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ReminderFailClosedException"/> class.</summary>
    /// <param name="reasonCode">The bounded reason code.</param>
    public ReminderFailClosedException(string reasonCode)
        : base("Reminder state could not be changed safely: " + reasonCode + ".")
        => ReasonCode = reasonCode;

    /// <summary>Gets the bounded reason code.</summary>
    public string ReasonCode { get; }
}
