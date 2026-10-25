using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Private turn-local authenticated primary invocation. Caller bodies cannot create this context.</summary>
internal static class SourcePublicationOperationContext
{
    private static readonly AsyncLocal<SourcePublicationAuthorityCall?> Value = new();
    internal static SourcePublicationAuthorityCall? Current => Value.Value;
    internal static SourcePublicationAuthorityCall? Set(SourcePublicationAuthorityCall? call)
    { var previous = Value.Value; Value.Value = call; return previous; }
}
