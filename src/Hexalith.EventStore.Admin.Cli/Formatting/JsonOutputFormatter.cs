using System.Text.Json;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Formats output as indented JSON with camelCase property names and enum-as-string serialization.
/// </summary>
public class JsonOutputFormatter : IOutputFormatter {
    /// <inheritdoc/>
    public string Format<T>(T item, IReadOnlyList<ColumnDefinition>? columns = null)
        => JsonSerializer.Serialize(item, JsonDefaults.Options);

    /// <inheritdoc/>
    public string FormatCollection<T>(IReadOnlyList<T> items, IReadOnlyList<ColumnDefinition>? columns = null)
        => JsonSerializer.Serialize(items, JsonDefaults.Options);
}
