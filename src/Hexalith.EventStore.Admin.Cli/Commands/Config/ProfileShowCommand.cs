using System.CommandLine;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Displays details of a single named connection profile.
/// </summary>
public static class ProfileShowCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Property", "Property"),
        new("Value", "Value"),
    ];

    /// <summary>
    /// Creates the profile show subcommand.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Argument<string> nameArg = new("name") { Description = "Profile name" };

        Command command = new("show", "Show details of a named connection profile");
        command.Arguments.Add(nameArg);

        command.SetAction((parseResult, _) =>
        {
            string name = parseResult.GetValue(nameArg)!;
            GlobalOptions globals = binding.Resolve(parseResult);
            return Task.FromResult(Execute(name, globals.Format));
        });

        return command;
    }

    internal static int Execute(string name, string format, string? profilePath = null)
    {
        ProfileStore store = ProfileManager.Load(profilePath);

        if (!store.Profiles.TryGetValue(name, out ConnectionProfile? profile))
        {
            Console.Error.WriteLine($"Profile '{name}' not found.");
            return ExitCodes.Error;
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            string json = JsonSerializer.Serialize(profile, JsonDefaults.Options);
            Console.WriteLine(json);
            return ExitCodes.Success;
        }

        bool isActive = string.Equals(name, store.ActiveProfile, StringComparison.Ordinal);

        List<KeyValueRow> rows =
        [
            new("Name", name),
            new("URL", profile.Url),
            new("Token", ProfileManager.MaskToken(profile.Token)),
            new("Format", profile.Format ?? "(default)"),
            new("Active", isActive ? "yes" : "no"),
        ];

        IOutputFormatter formatter = OutputFormatterFactory.Create(format);
        string output = formatter.FormatCollection(rows, Columns);
        Console.WriteLine(output);
        return ExitCodes.Success;
    }

    internal record KeyValueRow(string Property, string Value);
}
