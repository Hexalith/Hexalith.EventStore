using System.CommandLine;

namespace Hexalith.EventStore.Admin.Cli;

/// <summary>
/// Factory that creates all global <see cref="Option{T}"/> instances and resolves them from a parse result.
/// </summary>
public record GlobalOptionsBinding(
    Option<string> UrlOption,
    Option<string?> TokenOption,
    Option<string> FormatOption,
    Option<string?> OutputOption)
{
    /// <summary>
    /// Creates the global option definitions with environment-variable fallbacks and validation.
    /// </summary>
    public static GlobalOptionsBinding Create()
    {
        Option<string> urlOption = new("--url", "-u") { Description = "Admin API base URL", Recursive = true };
        urlOption.DefaultValueFactory = _ =>
            Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_URL") ?? "http://localhost:5002";

        Option<string?> tokenOption = new("--token", "-t") { Description = "JWT Bearer token or API key", Recursive = true };
        tokenOption.DefaultValueFactory = _ =>
            Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN");

        Option<string> formatOption = new("--format", "-f") { Description = "Output format", Recursive = true };
        formatOption.DefaultValueFactory = _ =>
            Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_FORMAT") ?? "table";
        formatOption.AcceptOnlyFromAmong("json", "csv", "table");

        Option<string?> outputOption = new("--output", "-o") { Description = "Redirect output to file", Recursive = true };

        return new GlobalOptionsBinding(urlOption, tokenOption, formatOption, outputOption);
    }

    /// <summary>
    /// Resolves parsed option values from a parse result.
    /// </summary>
    public GlobalOptions Resolve(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        return new(
            parseResult.GetValue(UrlOption)!,
            parseResult.GetValue(TokenOption),
            parseResult.GetValue(FormatOption)!,
            parseResult.GetValue(OutputOption));
    }
}
