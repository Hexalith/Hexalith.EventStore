using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Client.Streams;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Private turn-local authenticated primary invocation. Caller bodies cannot create this context.</summary>
internal static class SourcePublicationOperationContext
{
    private static readonly AsyncLocal<SourcePublicationAuthorityCall?> Value = new();
    private static readonly AsyncLocal<AuthoritativeStreamReadDeadline?> Budget = new();
    /// <summary>Gets the finite observation budget for the authenticated owner turn.</summary>
    internal static AuthoritativeStreamReadDeadline? Deadline => Budget.Value;
    /// <summary>Installs an observation budget and returns the previous budget.</summary>
    internal static AuthoritativeStreamReadDeadline? SetDeadline(AuthoritativeStreamReadDeadline? deadline)
    { var previous = Budget.Value; Budget.Value = deadline; return previous; }
    /// <summary>Gets the authenticated primary request for this owner turn.</summary>
    internal static SourcePublicationAuthorityCall? Current => Value.Value;
    /// <summary>Installs the authenticated request and returns the previous context.</summary>
    internal static SourcePublicationAuthorityCall? Set(SourcePublicationAuthorityCall? call)
    { var previous = Value.Value; Value.Value = call; return previous; }
}
