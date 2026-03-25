namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Column alignment direction.
/// </summary>
public enum Alignment
{
    /// <summary>Left-aligned text.</summary>
    Left,

    /// <summary>Right-aligned text.</summary>
    Right,
}

/// <summary>
/// Defines a column for table and CSV formatting.
/// </summary>
/// <param name="Header">Column header text.</param>
/// <param name="PropertyName">Name of the property to extract from data objects.</param>
/// <param name="MaxWidth">Maximum column width before truncation.</param>
/// <param name="Align">Column text alignment.</param>
public record ColumnDefinition(string Header, string PropertyName, int? MaxWidth = null, Alignment Align = Alignment.Left);
