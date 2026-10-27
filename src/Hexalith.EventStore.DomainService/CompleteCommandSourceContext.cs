using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.DomainService;

/// <summary>The request-local complete source admitted by the SDK. Consumer verifier adapters must require this context.</summary>
public static class CompleteCommandSourceContext
{
    private static readonly AsyncLocal<CompleteCommandSource?> Value = new();
    /// <summary>Gets the authenticated complete prefix for this processing invocation, or null when none was admitted.</summary>
    public static CompleteCommandSource? Current => Value.Value;
    /// <summary>Installs the admitted source for this SDK invocation and returns the previous context.</summary>
    internal static CompleteCommandSource? Set(CompleteCommandSource? value)
    {
        var previous = Value.Value; Value.Value = value; return previous;
    }
}
