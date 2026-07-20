using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Coordinates protected command admission through the tenant/key actor.</summary>
public interface IIdempotencyAdmissionCoordinator
{
    /// <summary>Admits a command when it carries a trusted descriptor.</summary>
    Task<IdempotencyAdmissionSession?> AdmitAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a new reservation to pending before aggregate routing.</summary>
    Task BeginAsync(
        IdempotencyAdmissionSession session,
        CancellationToken cancellationToken = default);

    /// <summary>Validates the signed current-fence capability against an exact execution boundary.</summary>
    Task ValidateExecutionAsync(
        IdempotencyAdmissionSession session,
        SubmitCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>Finalizes the command result under the active fence.</summary>
    Task CompleteAsync(
        IdempotencyAdmissionSession session,
        CommandProcessingResult result,
        CancellationToken cancellationToken = default);

    /// <summary>Persists recoverable or unknown-outcome state under the active fence.</summary>
    Task MarkRecoveryAsync(
        IdempotencyAdmissionSession session,
        IdempotencyAdmissionState state,
        CancellationToken cancellationToken = default);
}
