namespace Hexalith.McpCli.Abstractions;

/// <summary>
/// Declares a Contracts assembly as one discoverable Hexalith module.
/// </summary>
/// <param name="name">The canonical lowercase kebab-case module name.</param>
/// <param name="description">A one-line description of the module.</param>
/// <param name="identifierKind">The validation kind of explicitly declared payload identifiers.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class HexalithModuleAttribute(string name, string description, IdentifierKind identifierKind) : Attribute
{
    /// <summary>Gets the canonical module name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the module description.</summary>
    public string Description { get; } = description;

    /// <summary>Gets the module's payload identifier kind.</summary>
    public IdentifierKind IdentifierKind { get; } = identifierKind;

    /// <summary>Gets or sets the fixed envelope tenant for every operation.</summary>
    public string? FixedTenant { get; set; }

    /// <summary>Gets or sets the fallback gateway wire-type convention.</summary>
    public WireTypeConvention WireTypeConvention { get; set; } = WireTypeConvention.Explicit;

    /// <summary>Gets or sets a type in this assembly exposing public static JsonSerializerOptions Options.</summary>
    public Type? SerializerOptionsProvider { get; set; }
}
