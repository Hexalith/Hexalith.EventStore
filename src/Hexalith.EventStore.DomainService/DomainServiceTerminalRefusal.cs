using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService;

/// <summary>A refusal rule's only outcomes: no opinion, or one typed rejection.</summary>
public sealed class DomainServiceTerminalRefusal
{
    private DomainServiceTerminalRefusal(IRejectionEvent? rejection) => Rejection = rejection;

    /// <summary>Gets the single rejection, or null when the ordinary guards must continue.</summary>
    public IRejectionEvent? Rejection { get; }

    /// <summary>Gets the outcome that continues unchanged catalog resolution and admission.</summary>
    public static DomainServiceTerminalRefusal NoOpinion { get; } = new(null);

    /// <summary>Returns a terminal typed rejection; this never authorizes command execution.</summary>
    public static DomainServiceTerminalRefusal Reject(IRejectionEvent rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        return new(rejection);
    }
}
