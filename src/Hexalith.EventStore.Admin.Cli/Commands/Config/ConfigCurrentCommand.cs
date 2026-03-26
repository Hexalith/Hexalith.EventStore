using System.CommandLine;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Shows the currently active profile and resolved connection settings with source attribution.
/// </summary>
public static class ConfigCurrentCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Property", "Property"),
        new("Value", "Value"),
    ];

    /// <summary>
    /// Creates the config current subcommand.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Command command = new("current", "Show resolved connection settings and their sources");

        command.SetAction((parseResult, _) =>
        {
            GlobalOptions globals = binding.Resolve(parseResult);
            return Task.FromResult(Execute(binding, parseResult, globals.Format));
        });

        return command;
    }

    internal static int Execute(GlobalOptionsBinding binding, ParseResult parseResult, string format)
    {
        (GlobalOptions options, Dictionary<string, string> sources) = binding.ResolveWithSources(parseResult);

        string urlSource = sources.GetValueOrDefault("url", "default");
        string tokenSource = sources.GetValueOrDefault("token", "default");
        string formatSource = sources.GetValueOrDefault("format", "default");

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var jsonObj = new
            {
                activeProfile = options.Profile,
                url = new { value = options.Url, source = urlSource },
                token = new { value = options.Token, source = tokenSource },
                format = new { value = options.Format, source = formatSource },
            };
            string json = JsonSerializer.Serialize(jsonObj, JsonDefaults.Options);
            Console.WriteLine(json);
            return ExitCodes.Success;
        }

        List<KeyValueRow> rows =
        [
            new("Active Profile", options.Profile ?? "(none)"),
            new("URL", $"{options.Url} ({urlSource})"),
            new("Token", $"{Profiles.ProfileManager.MaskToken(options.Token)} ({tokenSource})"),
            new("Format", $"{options.Format} ({formatSource})"),
        ];

        IOutputFormatter formatter = OutputFormatterFactory.Create(format);
        string output = formatter.FormatCollection(rows, Columns);
        Console.WriteLine(output);
        return ExitCodes.Success;
    }

    internal record KeyValueRow(string Property, string Value);
}
