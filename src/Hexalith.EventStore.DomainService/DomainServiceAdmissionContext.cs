using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Context passed to a domain-service pre-commit admission stage before the command is dispatched to the
/// keyed <see cref="Client.Handlers.IDomainProcessor"/>.
/// </summary>
public sealed class DomainServiceAdmissionContext {
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceAdmissionContext"/> class.
    /// </summary>
    /// <param name="request">The domain-service request being admitted.</param>
    public DomainServiceAdmissionContext(DomainServiceRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    /// <summary>Gets the full domain-service request.</summary>
    public DomainServiceRequest Request { get; }

    /// <summary>Gets the command envelope that will be dispatched when admission accepts it.</summary>
    public CommandEnvelope Command => Request.Command;

    /// <summary>Gets the current aggregate state supplied to the domain processor.</summary>
    public object? CurrentState => Request.CurrentState;
}
