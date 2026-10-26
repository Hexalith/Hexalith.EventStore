namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>The existing aggregate short-name lookup protocol. A declaration bounds the resulting handler set.</summary>
public static class CommandDiscriminatorCompatibility
{
    /// <summary>Strips assembly qualification and namespace without repairing whitespace or changing case.</summary>
    public static string LookupKey(string discriminator)
    {
        ArgumentNullException.ThrowIfNull(discriminator);
        int comma = discriminator.IndexOf(',', StringComparison.Ordinal);
        string name = comma >= 0 ? discriminator[..comma] : discriminator;
        int dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }

    /// <summary>Matches only a declared actual handler's finite keys using its declared lookup protocol.</summary>
    public static bool Matches(RouteContractDeclaration declaration, string discriminator)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(discriminator);
        if (declaration.Execution is not { } execution)
        {
            return declaration.MessageType == discriminator;
        }

        string key = execution.AliasPolicy == "clr-short-name-v1" ? LookupKey(discriminator) : discriminator;
        return execution.Aliases.Contains(key, StringComparer.Ordinal);
    }
}
