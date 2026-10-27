using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.DomainService;

/// <summary>An offline-capable adapter whose complete policy is supplied by its owner.</summary>
public interface IDeclaredIdempotencyIntentAdapter : IIdempotencyIntentAdapter
{
    /// <summary>Gets the complete static owner declaration, including its domain and alias policy.</summary>
    CommandIntentDeclaration Declaration { get; }
}
