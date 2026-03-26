using System.CommandLine;

using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Commands;
using Hexalith.EventStore.Admin.Cli.Commands.Backup;
using Hexalith.EventStore.Admin.Cli.Commands.Projection;
using Hexalith.EventStore.Admin.Cli.Commands.Snapshot;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;

GlobalOptionsBinding binding = GlobalOptionsBinding.Create();

RootCommand rootCommand = new("Hexalith EventStore administration CLI");
rootCommand.Options.Add(binding.UrlOption);
rootCommand.Options.Add(binding.TokenOption);
rootCommand.Options.Add(binding.FormatOption);
rootCommand.Options.Add(binding.OutputOption);

rootCommand.Subcommands.Add(HealthCommand.Create(binding));
rootCommand.Subcommands.Add(StreamCommand.Create(binding));
rootCommand.Subcommands.Add(ProjectionCommand.Create(binding));
rootCommand.Subcommands.Add(TenantCommand.Create(binding));
rootCommand.Subcommands.Add(SnapshotCommand.Create(binding));
rootCommand.Subcommands.Add(BackupCommand.Create(binding));
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
