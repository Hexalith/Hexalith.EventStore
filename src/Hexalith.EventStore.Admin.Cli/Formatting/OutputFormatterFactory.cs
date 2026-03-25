namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Returns the correct <see cref="IOutputFormatter"/> based on the format string.
/// </summary>
public static class OutputFormatterFactory
{
    /// <summary>
    /// Creates a formatter for the specified format.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the format is not recognized.</exception>
    public static IOutputFormatter Create(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return format.ToLowerInvariant() switch
    {
        "json" => new JsonOutputFormatter(),
        "csv" => new CsvOutputFormatter(),
        "table" => new TableOutputFormatter(),
        _ => throw new ArgumentException($"Unknown output format: {format}", nameof(format)),
    };
    }
}
