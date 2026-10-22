using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Injected occurrence installation. A missing installation denies epoch, reservation, sealed, and writer checks.</summary>
public interface IInteractionOccurrenceInstallation
{
    /// <summary>Reads the installed epoch. Whitespace or null means the registry stays disabled.</summary>
    /// <param name="tenantId">Exact tenant.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The installed epoch, or null when it is absent.</returns>
    Task<string?> GetEpochAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>Authorizes one named Reserve, RetainSealed, CompleteWriter, or Lookup call.</summary>
    /// <param name="identity">Exact occurrence.</param>
    /// <param name="operation">Named method.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only for an exact installed authorization.</returns>
    Task<bool> AuthorizeOperationAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken cancellationToken);

    /// <summary>Authorizes one exact reservation.</summary>
    /// <param name="request">Reservation request.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only when the installation accepts that request.</returns>
    Task<bool> AuthorizeReservationAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken);

    /// <summary>Confirms one sealed result under the current writer lease.</summary>
    /// <param name="reservation">Retained reservation.</param>
    /// <param name="sealedResult">Sealed carrier.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only for the exact installed seal.</returns>
    Task<bool> VerifySealedAsync(InteractionOccurrenceRecord reservation, InteractionOccurrenceSealedResult sealedResult, CancellationToken cancellationToken);

    /// <summary>Confirms exact writer completion. Unknown completion returns false.</summary>
    /// <param name="record">Retained record.</param>
    /// <param name="proofId">Independent writer proof.</param>
    /// <param name="persisted">Whether the source write persisted.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only for that exact proof.</returns>
    Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord record, string proofId, bool persisted, CancellationToken cancellationToken);

    /// <summary>Confirms the installed epoch, revision, and state digest. A restored divergent snapshot returns false.</summary>
    /// <param name="tenantId">Exact tenant.</param>
    /// <param name="epochId">Installed epoch.</param>
    /// <param name="revision">Claimed revision.</param>
    /// <param name="exactStateDigest">Claimed state digest.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>True only when the installation still matches that triple.</returns>
    Task<bool> MatchesAsync(string tenantId, string epochId, long revision, string exactStateDigest, CancellationToken cancellationToken);
}
