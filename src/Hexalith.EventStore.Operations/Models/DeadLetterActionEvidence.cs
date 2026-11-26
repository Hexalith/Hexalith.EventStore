namespace Hexalith.EventStore.Operations.Models;

/// <summary>Durable attribution and outcome of an isolated operator decision.</summary>
/// <param name="Result">The persisted action result.</param>
/// <param name="TenantId">The authorized action tenant.</param>
/// <param name="MessageIds">The canonical affected message set.</param>
/// <param name="Subject">The authenticated human actor.</param>
/// <param name="Operation">The bounded operation kind.</param>
internal sealed record DeadLetterActionEvidence(DeadLetterActorActionResult Result, string TenantId,
    IReadOnlyList<string> MessageIds, string Subject, string Operation);
