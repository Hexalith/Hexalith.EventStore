using System.Reflection;
using System.Text;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Formats output as a human-readable aligned table with header separator.
/// </summary>
public class TableOutputFormatter : IOutputFormatter
{
    /// <inheritdoc/>
    public string Format<T>(T item, IReadOnlyList<ColumnDefinition>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Single object renders as key/value pairs (Property | Value)
        IReadOnlyList<(string Header, Func<object, string> Accessor)> props = columns is not null
            ? BuildAccessorsFromColumns(columns, typeof(T))
            : BuildAccessorsFromType(typeof(T));

        List<(string Key, string Value)> rows = props
            .Select(p => (p.Header, p.Accessor(item)))
            .ToList();

        if (rows.Count == 0)
        {
            return "Property  Value\n--------  -----";
        }

        int keyWidth = rows.Max(r => r.Key.Length);
        int valueWidth = rows.Max(r => r.Value.Length);
        keyWidth = Math.Max(keyWidth, "Property".Length);
        valueWidth = Math.Max(valueWidth, "Value".Length);

        StringBuilder sb = new();
        sb.AppendLine($"{"Property".PadRight(keyWidth)}  {"Value".PadRight(valueWidth)}");
        sb.AppendLine($"{new string('-', keyWidth)}  {new string('-', valueWidth)}");

        foreach ((string key, string value) in rows)
        {
            sb.AppendLine($"{key.PadRight(keyWidth)}  {value.PadRight(valueWidth)}");
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <inheritdoc/>
    public string FormatCollection<T>(IReadOnlyList<T> items, IReadOnlyList<ColumnDefinition>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        IReadOnlyList<(string Header, Func<object, string> Accessor, ColumnDefinition? Def)> props = columns is not null
            ? BuildAccessorsWithDefs(columns, typeof(T))
            : BuildAccessorsFromType(typeof(T)).Select(p => (p.Header, p.Accessor, (ColumnDefinition?)null)).ToList();

        // Compute values for all rows up front
        List<string[]> rowValues = items
            .Where(item => item is not null)
            .Select(item => props.Select(p => p.Accessor(item!)).ToArray())
            .ToList();

        // Calculate column widths by scanning all values
        int[] widths = new int[props.Count];
        for (int i = 0; i < props.Count; i++)
        {
            widths[i] = props[i].Header.Length;
            foreach (string[] row in rowValues)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }

            // Apply max width constraint if defined
            if (props[i].Def?.MaxWidth is int maxWidth && maxWidth > 0)
            {
                widths[i] = Math.Min(widths[i], maxWidth);
            }
        }

        StringBuilder sb = new();

        // Header
        sb.AppendLine(FormatRow(props.Select(p => p.Header).ToArray(), widths, props));

        // Separator
        sb.AppendLine(string.Join("  ", widths.Select(w => new string('-', w))));

        // Data rows
        foreach (string[] row in rowValues)
        {
            sb.AppendLine(FormatRow(row, widths, props));
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static IReadOnlyList<(string Header, Func<object, string> Accessor, ColumnDefinition? Def)> BuildAccessorsWithDefs(
        IReadOnlyList<ColumnDefinition> columns,
        Type type)
    {
        List<(string, Func<object, string>, ColumnDefinition?)> result = [];
        foreach (ColumnDefinition col in columns)
        {
            PropertyInfo? prop = type.GetProperty(col.PropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null)
            {
                result.Add((col.Header, obj => FormatValue(prop.GetValue(obj)), col));
            }
        }

        return result;
    }

    private static IReadOnlyList<(string Header, Func<object, string> Accessor)> BuildAccessorsFromColumns(
        IReadOnlyList<ColumnDefinition> columns,
        Type type)
        => BuildAccessorsWithDefs(columns, type)
            .Select(p => (p.Header, p.Accessor))
            .ToList();

    private static IReadOnlyList<(string Header, Func<object, string> Accessor)> BuildAccessorsFromType(Type type)
    {
        List<(string, Func<object, string>)> result = [];
        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!IsScalarType(prop.PropertyType))
            {
                continue;
            }

            result.Add((prop.Name, obj => FormatValue(prop.GetValue(obj))));
        }

        return result;
    }

    private static string FormatRow(
        string[] values,
        int[] widths,
        IReadOnlyList<(string Header, Func<object, string> Accessor, ColumnDefinition? Def)> props)
    {
        string[] cells = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            string val = Truncate(values[i], widths[i]);
            Alignment align = props[i].Def?.Align ?? Alignment.Left;
            cells[i] = align == Alignment.Right
                ? val.PadLeft(widths[i])
                : val.PadRight(widths[i]);
        }

        return string.Join("  ", cells);
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => string.Empty,
            Enum e => e.ToString(),
            _ => value.ToString() ?? string.Empty,
        };

    private static bool IsScalarType(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsPrimitive || underlying.IsEnum)
        {
            return true;
        }

        return underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(Guid);
    }

    private static string Truncate(string value, int maxWidth)
    {
        if (maxWidth <= 0 || value.Length <= maxWidth)
        {
            return value;
        }

        return maxWidth <= 3
            ? new string('.', maxWidth)
            : string.Concat(value.AsSpan(0, maxWidth - 3), "...");
    }
}
