using System.Collections;
using System.Reflection;
using System.Text;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Formats output as CSV with header row and data rows.
/// </summary>
public class CsvOutputFormatter : IOutputFormatter
{
    /// <inheritdoc/>
    public string Format<T>(T item, IReadOnlyList<ColumnDefinition>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        IReadOnlyList<(string Header, Func<object, string> Accessor)> props = columns is not null
            ? BuildAccessorsFromColumns(columns, typeof(T))
            : BuildAccessorsFromType(typeof(T));

        StringBuilder sb = new();
        sb.AppendLine("Property,Value");
        foreach ((string header, Func<object, string> accessor) in props)
        {
            sb.Append(Escape(header));
            sb.Append(',');
            sb.AppendLine(Escape(accessor(item)));
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <inheritdoc/>
    public string FormatCollection<T>(IReadOnlyList<T> items, IReadOnlyList<ColumnDefinition>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        IReadOnlyList<(string Header, Func<object, string> Accessor)> props = columns is not null
            ? BuildAccessorsFromColumns(columns, typeof(T))
            : BuildAccessorsFromType(typeof(T));

        StringBuilder sb = new();

        // Header row
        sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Header))));

        // Data rows
        foreach (T item in items)
        {
            if (item is null)
            {
                continue;
            }

            sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Accessor(item)))));
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    private static IReadOnlyList<(string Header, Func<object, string> Accessor)> BuildAccessorsFromColumns(
        IReadOnlyList<ColumnDefinition> columns,
        Type type)
    {
        List<(string, Func<object, string>)> result = [];
        foreach (ColumnDefinition col in columns)
        {
            PropertyInfo? prop = type.GetProperty(col.PropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null)
            {
                result.Add((col.Header, obj => FormatValue(prop.GetValue(obj))));
            }
        }

        return result;
    }

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

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
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
}
