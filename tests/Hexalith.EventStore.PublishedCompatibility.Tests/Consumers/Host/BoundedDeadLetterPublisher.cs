using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

namespace PublishedConsumer;

/// <summary>Counts documented infrastructure bookkeeping separately from committed aggregate events.</summary>
public sealed class BoundedDeadLetterPublisher : IDeadLetterPublisher
{
    /// <summary>Gets the number of separate dead-letter bookkeeping observations.</summary>
    public int Count { get; private set; }

    /// <inheritdoc/>
    public Task<bool> PublishDeadLetterAsync(AggregateIdentity identity, DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        Count++;
        return Task.FromResult(true);
    }
}
