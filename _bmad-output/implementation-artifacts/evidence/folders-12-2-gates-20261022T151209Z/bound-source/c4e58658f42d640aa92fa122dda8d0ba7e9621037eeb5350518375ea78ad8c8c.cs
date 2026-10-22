namespace Hexalith.McpCli.Abstractions;

/// <summary>
/// Specifies how a module derives an operation's gateway wire type when one is not declared.
/// </summary>
public enum WireTypeConvention
{
    /// <summary>No wire type is derived.</summary>
    Explicit,

    /// <summary>Uses the full CLR type name, including its namespace.</summary>
    FullTypeName,

    /// <summary>Uses the operation's kebab-case name part.</summary>
    KebabCase,
}
