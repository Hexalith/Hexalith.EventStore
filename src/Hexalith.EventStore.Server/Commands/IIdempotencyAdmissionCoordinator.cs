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

    /// <summary>Validates local execution capability and atomically moves a reservation to pending with actor authority verification.</summary>
    Task BeginAsync(
        IdempotencyAdmissionSession session,
        SubmitCommand command,
        CancellationToken cancellationToken = default)
        => BeginAsync(session, cancellationToken);

    /// <summary>Validates only the signed capability and exact command boundary before state mutation.</summary>
    Task ValidateExecutionCapabilityAsync(
        IdempotencyAdmissionSession session,
        SubmitCommand command,
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

    /// <summary>Validates local execution capability and atomically finalizes the command result with actor authority verification.</summary>
    Task CompleteAsync(
        IdempotencyAdmissionSession session,
        SubmitCommand command,
        CommandProcessingResult result,
        CancellationToken cancellationToken = default)
        => CompleteAsync(session, result, cancellationToken);

    /// <summary>Persists recoverable or unknown-outcome state under the active fence.</summary>
    Task MarkRecoveryAsync(
        IdempotencyAdmissionSession session,
        IdempotencyAdmissionState state,
        CancellationToken cancellationToken = default);
}
