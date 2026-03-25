namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Formats data objects for CLI output in various formats (JSON, CSV, table).
/// </summary>
public interface IOutputFormatter
{
    /// <summary>
    /// Formats a single object.
    /// </summary>
    string Format<T>(T item, IReadOnlyList<ColumnDefinition>? columns = null);

    /// <summary>
    /// Formats a collection of objects.
    /// </summary>
    string FormatCollection<T>(IReadOnlyList<T> items, IReadOnlyList<ColumnDefinition>? columns = null);
}
