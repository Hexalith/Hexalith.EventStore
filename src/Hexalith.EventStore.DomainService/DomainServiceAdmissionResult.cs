using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Result returned by a domain-service pre-commit admission stage.
/// </summary>
public sealed class DomainServiceAdmissionResult {
    private DomainServiceAdmissionResult(bool isAccepted, IReadOnlyList<IRejectionEvent> rejectionEvents) {
        IsAccepted = isAccepted;
        RejectionEvents = rejectionEvents;
    }

    /// <summary>Gets a value indicating whether the stage accepted the command.</summary>
    public bool IsAccepted { get; }

    /// <summary>Gets a value indicating whether the stage rejected the command.</summary>
    public bool IsRejected => !IsAccepted;

    /// <summary>Gets the typed rejection events returned by a rejecting stage.</summary>
    public IReadOnlyList<IRejectionEvent> RejectionEvents { get; }

    /// <summary>Creates an accepted admission result.</summary>
    /// <returns>An accepted result with no rejection events.</returns>
    public static DomainServiceAdmissionResult Accepted()
        => new(true, Array.Empty<IRejectionEvent>());

    /// <summary>Creates a rejected admission result carrying typed domain rejection events.</summary>
    /// <param name="rejectionEvents">One or more rejection events.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rejectionEvents"/> is empty.</exception>
    public static DomainServiceAdmissionResult Rejected(IReadOnlyList<IRejectionEvent> rejectionEvents) {
        ArgumentNullException.ThrowIfNull(rejectionEvents);
        if (rejectionEvents.Count == 0) {
            throw new ArgumentException("Admission rejection requires at least one rejection event.", nameof(rejectionEvents));
        }

        for (int i = 0; i < rejectionEvents.Count; i++) {
            if (rejectionEvents[i] is null) {
                throw new ArgumentException("Admission rejection events cannot contain null entries.", nameof(rejectionEvents));
            }
        }

        return new DomainServiceAdmissionResult(false, rejectionEvents);
    }
}
