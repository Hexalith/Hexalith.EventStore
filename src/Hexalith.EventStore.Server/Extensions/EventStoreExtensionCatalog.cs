using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Extensions;

/// <summary>The verified extensions loaded into this gateway, in declaration order.</summary>
public sealed class EventStoreExtensionCatalog
{
    internal EventStoreExtensionCatalog(IReadOnlyList<EventStoreLoadedExtension> extensions, IReadOnlyList<IDeclaredIdempotencyIntentAdapter> adapters)
    {
        Extensions = extensions;
        Adapters = adapters;
    }

    /// <summary>Gets the loaded extensions.</summary>
    public IReadOnlyList<EventStoreLoadedExtension> Extensions { get; }

    /// <summary>Gets the trusted intent adapters contributed by the loaded extensions.</summary>
    internal IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Adapters { get; }
}
