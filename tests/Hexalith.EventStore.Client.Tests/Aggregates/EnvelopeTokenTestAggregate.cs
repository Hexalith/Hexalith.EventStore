using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Proves the envelope and token Handle convention receives the caller token.</summary>
internal sealed class EnvelopeTokenTestAggregate : EventStoreAggregate<VersionTwoTestState>
{
    /// <summary>Gets the token passed to Handle.</summary>
    internal CancellationToken ObservedToken { get; private set; }

    /// <summary>Gets the command envelope passed to Handle.</summary>
    internal CommandEnvelope? ObservedEnvelope { get; private set; }

    /// <summary>Processes a command with both its envelope and caller token.</summary>
    public DomainResult Handle(TokenAwareTestCommand command, VersionTwoTestState? state,
        CommandEnvelope envelope, CancellationToken cancellationToken)
    {
        ObservedToken = cancellationToken;
        ObservedEnvelope = envelope;
        return DomainResult.NoOp();
    }
}
