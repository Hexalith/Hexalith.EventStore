namespace Hexalith.McpCli.Abstractions;

/// <summary>
/// Specifies how a module validates explicitly marked payload identifiers.
/// </summary>
public enum IdentifierKind
{
    /// <summary>Identifiers are ULID strings.</summary>
    Ulid,

    /// <summary>Identifiers are non-empty strings.</summary>
    String,
}
