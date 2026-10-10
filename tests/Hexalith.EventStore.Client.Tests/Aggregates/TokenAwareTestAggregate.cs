using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Proves the token-aware state overload wins over its legacy overload.</summary>
internal sealed class TokenAwareTestAggregate : EventStoreAggregate<VersionTwoTestState>
{
    /// <summary>Gets the token passed to the selected Handle method.</summary>
    internal CancellationToken ObservedToken { get; private set; }

    /// <summary>Gets whether the old overload ran.</summary>
    internal bool LegacyCalled { get; private set; }

    /// <summary>Legacy overload that must not win discovery.</summary>
    public DomainResult Handle(TokenAwareTestCommand command, VersionTwoTestState? state)
    {
        LegacyCalled = true;
        return DomainResult.NoOp();
    }

    /// <summary>Preferred overload that receives the caller token.</summary>
    public DomainResult Handle(TokenAwareTestCommand command, VersionTwoTestState? state, CancellationToken cancellationToken)
    {
        ObservedToken = cancellationToken;
        return DomainResult.NoOp();
    }
}
