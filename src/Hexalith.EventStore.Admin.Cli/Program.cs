using System.CommandLine;
using System.Reflection;

using Hexalith.EventStore.Admin.Cli;
using Hexalith.EventStore.Admin.Cli.Commands;
using Hexalith.EventStore.Admin.Cli.Commands.Backup;
using Hexalith.EventStore.Admin.Cli.Commands.Projection;
using Hexalith.EventStore.Admin.Cli.Commands.Snapshot;
using Hexalith.EventStore.Admin.Cli.Commands.Stream;
using Hexalith.EventStore.Admin.Cli.Commands.Config;
using Hexalith.EventStore.Admin.Cli.Commands.Tenant;
using Hexalith.EventStore.Admin.Cli.Profiles;

if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
{
    string version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";
    Console.WriteLine(version);
    return ExitCodes.Success;
}

GlobalOptionsBinding binding = GlobalOptionsBinding.Create();

RootCommand rootCommand = new("Hexalith EventStore administration CLI");
rootCommand.Options.Add(binding.UrlOption);
rootCommand.Options.Add(binding.TokenOption);
rootCommand.Options.Add(binding.FormatOption);
rootCommand.Options.Add(binding.OutputOption);
rootCommand.Options.Add(binding.ProfileOption);

rootCommand.Subcommands.Add(HealthCommand.Create(binding));
rootCommand.Subcommands.Add(StreamCommand.Create(binding));
rootCommand.Subcommands.Add(ProjectionCommand.Create(binding));
rootCommand.Subcommands.Add(TenantCommand.Create(binding));
rootCommand.Subcommands.Add(SnapshotCommand.Create(binding));
rootCommand.Subcommands.Add(BackupCommand.Create(binding));
rootCommand.Subcommands.Add(ConfigCommand.Create(binding));

try
{
    return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
}
catch (ProfileNotFoundException ex)
{
    await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
    return ExitCodes.Error;
}
catch (ProfileStoreVersionException ex)
{
    await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
    return ExitCodes.Error;
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Unexpected error: {ex.Message}").ConfigureAwait(false);
    return ExitCodes.Error;
}
