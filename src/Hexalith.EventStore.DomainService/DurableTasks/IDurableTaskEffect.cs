namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Trusted owner operations for one versioned Durable Task step.</summary>
public interface IDurableTaskEffect
{
    /// <summary>Looks up the step with current authority for its original actor.</summary>
    /// <remarks>Dependency failure must throw. A committed result includes the durable receipt identifier.</remarks>
    Task<(DurableTaskEffectLookup Outcome, string? Receipt)> LookupAsync(DurableTaskEffectStep step, CancellationToken cancellationToken);

    /// <summary>Invokes the owner with the stable step key and returns its committed receipt identifier.</summary>
    /// <remarks>The owner operation must reauthorize the original actor and enforce idempotency at commit.</remarks>
    Task<string> ExecuteAsync(DurableTaskEffectStep step, CancellationToken cancellationToken);
}
