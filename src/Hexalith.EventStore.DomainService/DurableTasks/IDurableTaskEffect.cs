namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Trusted owner operations for one versioned Durable Task step.</summary>
public interface IDurableTaskEffect
{
    /// <summary>Looks up the step with current authority for its original actor.</summary>
    /// <remarks>Dependency failure must throw. A committed result includes the durable receipt identifier. Return
    /// <see cref="DurableTaskEffectLookup.Rejected"/> only when the owner holds no effect for the step key, and
    /// <see cref="DurableTaskEffectLookup.Unresolved"/> for a committed effect that the original actor can no longer read.
    /// After reporting Rejected, the owner must refuse every later execution for that step key.</remarks>
    Task<(DurableTaskEffectLookup Outcome, string? Receipt)> LookupAsync(DurableTaskEffectStep step, CancellationToken cancellationToken);

    /// <summary>Invokes the owner with the stable step key and returns its committed outcome.</summary>
    /// <remarks>The owner operation must reauthorize the original actor and enforce idempotency at commit. Return
    /// <see cref="DurableTaskEffectLookup.Committed"/> with the receipt identifier, <see cref="DurableTaskEffectLookup.Rejected"/>
    /// for an authoritative refusal that applied no effect, or <see cref="DurableTaskEffectLookup.Unresolved"/> for a partial
    /// effect. After reporting Rejected, the owner must refuse every later execution for that step key, so a late call from an
    /// earlier lease cannot commit under the terminal task. Dependency failure and a lost reply must throw. An owner that
    /// verifies a delegated step through <see cref="DurableTaskAdmission.ResolveStepAsync"/> refuses a binding that does not
    /// accept the presented fence by failing the call, without reporting Rejected.</remarks>
    Task<(DurableTaskEffectLookup Outcome, string? Receipt)> ExecuteAsync(DurableTaskEffectStep step, CancellationToken cancellationToken);
}
