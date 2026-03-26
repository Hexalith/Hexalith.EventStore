using System.CommandLine;
using System.Text.Json;

using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Admin.Cli.Profiles;

namespace Hexalith.EventStore.Admin.Cli.Commands.Config;

/// <summary>
/// Lists all saved connection profiles.
/// </summary>
public static class ProfileListCommand
{
    internal static readonly List<ColumnDefinition> Columns =
    [
        new("Name", "Name"),
        new("URL", "Url"),
        new("Token", "Token"),
        new("Format", "Format"),
        new("Active", "Active"),
    ];

    /// <summary>
    /// Creates the profile list subcommand.
    /// </summary>
    public static Command Create(GlobalOptionsBinding binding)
    {
        Command command = new("list", "List all saved connection profiles");

        command.SetAction((parseResult, _) =>
        {
            GlobalOptions globals = binding.Resolve(parseResult);
            return Task.FromResult(Execute(globals.Format));
        });

        return command;
    }

    internal static int Execute(string format, string? profilePath = null)
    {
        ProfileStore store = ProfileManager.Load(profilePath);

        if (store.Profiles.Count == 0)
        {
            Console.Error.WriteLine("No profiles configured. Use 'eventstore-admin config profile add <name> --api-url <url>' to create one.");
            return ExitCodes.Success;
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            string json = JsonSerializer.Serialize(store.Profiles, JsonDefaults.Options);
            Console.WriteLine(json);
            return ExitCodes.Success;
        }

        List<ProfileRow> rows = store.Profiles
            .Select(p => new ProfileRow(
                p.Key,
                p.Value.Url,
                ProfileManager.MaskToken(p.Value.Token),
                p.Value.Format ?? "(default)",
                string.Equals(p.Key, store.ActiveProfile, StringComparison.Ordinal) ? "*" : string.Empty))
            .ToList();

        IOutputFormatter formatter = OutputFormatterFactory.Create(format);
        string output = formatter.FormatCollection(rows, Columns);
        Console.WriteLine(output);
        return ExitCodes.Success;
    }

    internal record ProfileRow(string Name, string Url, string Token, string Format, string Active);
}
