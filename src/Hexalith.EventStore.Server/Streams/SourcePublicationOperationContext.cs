using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Client.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Private turn-local authenticated primary invocation. Caller bodies cannot create this context.</summary>
internal static class SourcePublicationOperationContext
{
    private static readonly AsyncLocal<SourcePublicationAuthorityCall?> Value = new();
    private static readonly AsyncLocal<AuthoritativeStreamReadDeadline?> Budget = new();
    internal static AuthoritativeStreamReadDeadline? Deadline => Budget.Value;
    internal static AuthoritativeStreamReadDeadline? SetDeadline(AuthoritativeStreamReadDeadline? deadline)
    { var previous = Budget.Value; Budget.Value = deadline; return previous; }
    internal static SourcePublicationAuthorityCall? Current => Value.Value;
    internal static SourcePublicationAuthorityCall? Set(SourcePublicationAuthorityCall? call)
    { var previous = Value.Value; Value.Value = call; return previous; }
}
