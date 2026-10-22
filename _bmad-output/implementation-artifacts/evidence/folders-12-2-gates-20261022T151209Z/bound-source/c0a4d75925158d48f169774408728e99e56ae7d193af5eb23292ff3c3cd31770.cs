using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Platform configuration locating an existing selected owner row; never sent in proof DTOs.</summary>
/// <param name="StoreName">The owning Dapr read-model component.</param>
/// <param name="SelectionSuffix">The registered suffix after the canonical aggregate identity.</param>
public sealed record ReadModelOwnerProofBinding(string StoreName, string SelectionSuffix)
{
    /// <summary>Gets an explicitly registered private selection-key locator. The source and receipt scopes remain exact identities.</summary>
    public Func<ProjectionOwnerProofRequest, string>? SelectionKey { get; init; }
}
