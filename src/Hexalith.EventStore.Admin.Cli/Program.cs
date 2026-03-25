using System.CommandLine;

using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Commands;

GlobalOptionsBinding binding = GlobalOptionsBinding.Create();

RootCommand rootCommand = new("Hexalith EventStore administration CLI");
rootCommand.Options.Add(binding.UrlOption);
rootCommand.Options.Add(binding.TokenOption);
rootCommand.Options.Add(binding.FormatOption);
rootCommand.Options.Add(binding.OutputOption);

rootCommand.Subcommands.Add(HealthCommand.Create(binding));
rootCommand.Subcommands.Add(StubCommands.Create("stream", "Query, list, and inspect event streams"));
rootCommand.Subcommands.Add(StubCommands.Create("projection", "List, pause, resume, and reset projections"));
rootCommand.Subcommands.Add(StubCommands.Create("tenant", "List tenants, view quotas, and verify isolation"));
rootCommand.Subcommands.Add(StubCommands.Create("snapshot", "Manage aggregate snapshots"));
rootCommand.Subcommands.Add(StubCommands.Create("backup", "Trigger and manage backups"));
rootCommand.Subcommands.Add(StubCommands.Create("config", "Manage connection profiles and CLI configuration"));

try
{
    return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Unexpected error: {ex.Message}").ConfigureAwait(false);
    return ExitCodes.Error;
}
